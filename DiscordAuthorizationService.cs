namespace CakeyBotCore.Services.Authorization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CakeyBotCore.Extensions;
using CakeyBotCore.Models.Internal;
using CakeyBotCore.Persistence;
using CakeyBotCore.Services.Authorization.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

internal sealed class DiscordAuthorizationService : IDiscordAuthorizationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public DiscordAuthorizationService(IHttpClientFactory httpClientFactory, IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _httpClientFactory = httpClientFactory;
        _dbContextFactory = dbContextFactory;
        EphemeralBearerTokens = new ConcurrentDictionary<ulong, (DateTime Created, string Token)>();
    }

    public IDictionary<ulong, (DateTime Created, string Token)> EphemeralBearerTokens { get; }

    // this is an example of making an oauth req. we have more but I trimmed them for this example
    public async ValueTask<DiscordCommandPermissions> EditCommandPermissionsAsync(
        ulong applicationId,
        ulong guildId,
        ulong commandId,
        DiscordCommandPermissions permissions,
        PermissionOAuth authData, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage
        {
            RequestUri = new($"https://discord.com/api/v10/applications/{applicationId}/guilds/{guildId}/commands/{commandId}/permissions"),
            Method = HttpMethod.Put,
            Content = new StringContent(permissions.SerializeToSnakeCaseFromType(), System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {authData.BearerToken}");

        using var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        try
        {
            return await response.Content
                .ReadFromJsonAsync(DiscordJsonSerializerContext.Default.DiscordCommandPermissions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException) // no perms
        {
            return null;
        }
    }

    // ...

    public async ValueTask<PermissionOAuth> GetUserPermissionOAuth(ulong userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var databaseContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var _ = databaseContext.ConfigureAwait(false);

        try
        {
            // @ in variable name appears to be a knock-on effect of some source generation?
            // not sure why override was chosen as a variable name, since it's keyword
            // but I digress 

            // basically, if we have a bearer token for the user, return it
            if (EphemeralBearerTokens.TryGetValue(userId, out var @override) && @override.Created.AddHours(1) > DateTime.Now)
            {
                return new PermissionOAuth
                {
                    BearerToken = @override.Token,
                    ExpiresAt = new DateTimeOffset(@override.Created, default).AddHours(1).ToUnixTimeSeconds(),
                };
            }

            // otherwise, try to get the user's oauth data from the database
            var data = await databaseContext.PermissionOAuth
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            // then refresh the token if it's expired
            var (newData, updated) = await RefreshPermissionOAuthAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (updated)
            {
                data.UserId = userId;
                data.BearerToken = newData.BearerToken;
                data.RefreshToken = newData.RefreshToken;
                data.ExpiresAt = newData.ExpiresAt;

                await databaseContext
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return data;
        }
        catch (Exception exception)
        {
            Log.Debug("Failed to get or updated user OAuth data for {user} because {exception}", userId, exception);
            return null;
        }
    }

    public async ValueTask<(PermissionOAuth Data, bool Updated)> RefreshPermissionOAuthAsync(PermissionOAuth data, bool force = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // this checks if the token is expired, if it's still valid we can keep it
        if (DateTime.Parse(data.ExpiresAt.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) > DateTime.Now && !force)
        {
            return (data, false);
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id",      Config.Instance.Bot.BotID.ToString() },
            { "client_secret",  Config.Instance.Bot.Secret },
            { "grant_type",     "refresh_token" },
            { "refresh_token",  data.RefreshToken },
        });

        using var httpClient = _httpClientFactory.CreateClient();

        using var responseMessage = await httpClient
            .PostAsync($"https://discord.com/api/v10/oauth2/token", content, cancellationToken)
            .ConfigureAwait(false);

        responseMessage.EnsureSuccessStatusCode();

        var accessTokenResponse = await responseMessage.Content
            .ReadFromJsonAsync(DiscordJsonSerializerContext.Default.DiscordAccessTokenResponse, cancellationToken)
            .ConfigureAwait(false);

        data.BearerToken = accessTokenResponse.AccessToken;
        data.ExpiresAt = (ulong)DateTime.UtcNow.AddSeconds(accessTokenResponse.ExpiresIn).DateTimeToUnixTimestamp();
        data.RefreshToken = accessTokenResponse.RefreshToken;

        return (data, true);
    }
}

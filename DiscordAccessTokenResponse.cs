namespace CakeyBotCore.Services.Authorization.Models;

using System.Text.Json.Serialization;

public sealed record class DiscordAccessTokenResponse
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; }

	[JsonPropertyName("token_type")]
	public string TokenType { get; set; }

	[JsonPropertyName("expires_in")]
	public double ExpiresIn { get; set; }

	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; set; }

	[JsonPropertyName("scope")]
	public string Scope { get; set; }
}

public async Task<PermissionOAuth?> GetAuthDataAsync()
{
    if (Config.Instance.Bot.IsWhiteLabel)
    {
        var eb = new EmbedBuilder()
            .WithColorType(EmbedColor.Error)
            .WithTitle(strings.user_oauth_invalid_title)
            .WithDescription(strings.wl_blocked);
        await RespondAsync(embed: eb.Build(), ephemeral: true);
        return null;
    }

    var model = await AuthorizationService.GetUserPermissionOAuth(Context.User.Id); // this is an IDiscordAuthorizationService
    if (model is null)
    {
        var eb = new EmbedBuilder()/*..*/;
        var cb = new ComponentBuilder()
            .WithButton(new ButtonBuilder()
                .WithLabel(strings.user_oauth_invalid_authorize)/*...*/);
        if (Context.Client.CurrentUser.Id != 288163958022471680) // the app is not supported by the web dashboard
        {
            cb.WithButton(new ButtonBuilder()
                .WithLabel(strings.user_oauth_ephemeral)/**/);
        }

        await RespondAsync(embed: eb.Build(), components: cb.Build(), ephemeral: true);

    }

    return model;
}

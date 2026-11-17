namespace ADManager.Api.Auth;

public static class SessionAuthConstants
{
    public const string Scheme = "SessionCookie";
    public const string CookieName = "admgr.sid";
    public const string AdminRole = "Admin";
    public const string ViewerRole = "Viewer";
    public const string ApiKeyScheme = "ApiKey";
    public const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>Comma-joined form for the MVC <c>[Authorize(AuthenticationSchemes = ...)]</c> attribute, which splits on commas. Do not pass to <c>AddAuthenticationSchemes(params string[])</c> — use <see cref="AllSchemes"/> there instead.</summary>
    public const string AnyScheme = $"{Scheme},{ApiKeyScheme}";

    /// <summary>Array form for fluent APIs such as <c>AuthorizationPolicyBuilder.AddAuthenticationSchemes(params string[])</c>, which do not split comma-joined strings.</summary>
    public static readonly string[] AllSchemes = [Scheme, ApiKeyScheme];
}

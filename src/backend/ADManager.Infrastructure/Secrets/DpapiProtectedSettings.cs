namespace ADManager.Infrastructure.Secrets;

public sealed class DpapiProtectedSettings
{
    public string ServiceAccountEncrypted { get; set; } = string.Empty;
    public string SessionSigningKeyEncrypted { get; set; } = string.Empty;
    public string? DiscordWebhookUrlEncrypted { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? ApiKeyOwner { get; set; }
    public string? ApiKeyRole { get; set; }
}

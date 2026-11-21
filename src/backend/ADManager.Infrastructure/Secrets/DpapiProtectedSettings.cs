namespace ADManager.Infrastructure.Secrets;

public sealed class DpapiProtectedSettings
{
    public string ServiceAccountEncrypted { get; set; } = string.Empty;
    public string SessionSigningKeyEncrypted { get; set; } = string.Empty;
    /// <summary>DPAPI-encrypted Request Tracking alert hook URL (from RT set_alert_hook).</summary>
    public string? AlertHookUrlEncrypted { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? ApiKeyOwner { get; set; }
    public string? ApiKeyRole { get; set; }
}

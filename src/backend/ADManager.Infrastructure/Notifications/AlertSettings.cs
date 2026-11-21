namespace ADManager.Infrastructure.Notifications;

/// <summary>Bound from the "Alerts" configuration section.</summary>
public sealed class AlertSettings
{
    /// <summary>Site name reported to the hub as "tenant"; defaults to the machine name.</summary>
    public string? Tenant { get; set; }
}

namespace ADManager.Application.Abstractions;

public sealed class AdSettings
{
    public string AdminGroupName { get; set; } = "Corp.IT.Admin";
    public string ViewerGroupName { get; set; } = string.Empty;
    public int PasswordExpiryWarningDays { get; set; } = 90;
}

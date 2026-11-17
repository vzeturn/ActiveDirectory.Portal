namespace ADManager.Infrastructure.Sessions;

public sealed class SessionSettings
{
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromHours(8);
}

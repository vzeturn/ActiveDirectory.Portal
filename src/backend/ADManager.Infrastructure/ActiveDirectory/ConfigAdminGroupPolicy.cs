using ADManager.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ADManager.Infrastructure.ActiveDirectory;

public sealed class ConfigAdminGroupPolicy : IAdminGroupPolicy
{
    private readonly AdSettings _settings;

    public ConfigAdminGroupPolicy(IOptions<AdSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool IsAdmin(IReadOnlyList<string> groups) =>
        groups.Any(g => string.Equals(g, _settings.AdminGroupName, StringComparison.OrdinalIgnoreCase));

    public bool IsViewer(IReadOnlyList<string> groups) =>
        !string.IsNullOrWhiteSpace(_settings.ViewerGroupName)
        && groups.Any(g => string.Equals(g, _settings.ViewerGroupName, StringComparison.OrdinalIgnoreCase));
}

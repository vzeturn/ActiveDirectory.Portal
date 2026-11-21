using ADManager.Application.Abstractions;

namespace ADManager.Application.Tests.Fakes;

public sealed class FakeSecretsProvider : ISecretsProvider
{
    public string? AlertHookUrl { get; set; }

    public Task<ServiceAccountSecret> GetServiceAccountAsync(CancellationToken ct = default) =>
        Task.FromResult(new ServiceAccountSecret("svc", "pw", "dc", "corp.local", "DC=corp,DC=local", "OU=Users,DC=corp,DC=local"));

    public Task<string> GetSessionSigningKeyAsync(CancellationToken ct = default) => Task.FromResult("signing-key");
    public Task<string?> GetAlertHookUrlAsync(CancellationToken ct = default) => Task.FromResult(AlertHookUrl);
    public Task<string?> GetApiKeyAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<string?> GetApiKeyOwnerAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<string?> GetApiKeyRoleAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
}

namespace ADManager.Application.Abstractions;

public interface ISecretsProvider
{
    Task<ServiceAccountSecret> GetServiceAccountAsync(CancellationToken ct = default);
    Task<string> GetSessionSigningKeyAsync(CancellationToken ct = default);
    Task<string?> GetAlertHookUrlAsync(CancellationToken ct = default);
    Task<string?> GetApiKeyAsync(CancellationToken ct = default);
    Task<string?> GetApiKeyOwnerAsync(CancellationToken ct = default);
    Task<string?> GetApiKeyRoleAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
}

public sealed record ServiceAccountSecret(
    string Username,
    string Password,
    string Server,
    string Domain,
    string BaseDN,
    string OuBaseDN);

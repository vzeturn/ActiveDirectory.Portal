using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ADManager.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ADManager.Infrastructure.Secrets;

public sealed class DpapiSecretsProvider : ISecretsProvider
{
    private readonly DpapiProtectedSettings _settings;
    private ServiceAccountSecret? _serviceAccount;
    private string? _sessionSigningKey;
    private string? _discordWebhookUrl;
    private string? _apiKey;

    public DpapiSecretsProvider(IOptions<DpapiProtectedSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<ServiceAccountSecret> GetServiceAccountAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(_serviceAccount!);
    }

    public Task<string> GetSessionSigningKeyAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(_sessionSigningKey!);
    }

    public Task<string?> GetDiscordWebhookUrlAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(_discordWebhookUrl);
    }

    public Task<string?> GetApiKeyAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(_apiKey);
    }

    public Task<string?> GetApiKeyOwnerAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(string.IsNullOrWhiteSpace(_settings.ApiKeyOwner) ? null : _settings.ApiKeyOwner);
    }

    public Task<string?> GetApiKeyRoleAsync(CancellationToken ct = default)
    {
        EnsureLoaded();
        return Task.FromResult(string.IsNullOrWhiteSpace(_settings.ApiKeyRole) ? null : _settings.ApiKeyRole);
    }

    public Task RefreshAsync(CancellationToken ct = default)
    {
        EnsureLoaded(force: true);
        return Task.CompletedTask;
    }

    private void EnsureLoaded(bool force = false)
    {
        if (_serviceAccount is not null && !force) return;

        var serviceAccountJson = Unprotect(_settings.ServiceAccountEncrypted);
        _serviceAccount = ParseServiceAccount(serviceAccountJson);
        _sessionSigningKey = Unprotect(_settings.SessionSigningKeyEncrypted);
        _discordWebhookUrl = string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrlEncrypted)
            ? null
            : Unprotect(_settings.DiscordWebhookUrlEncrypted);
        _apiKey = string.IsNullOrWhiteSpace(_settings.ApiKeyEncrypted)
            ? null
            : Unprotect(_settings.ApiKeyEncrypted);
    }

    private static string Unprotect(string base64Ciphertext)
    {
        var ciphertext = Convert.FromBase64String(base64Ciphertext);
        var plaintext = ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static ServiceAccountSecret ParseServiceAccount(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new ServiceAccountSecret(
            root.GetProperty("username").GetString()!,
            root.GetProperty("password").GetString()!,
            root.GetProperty("server").GetString()!,
            root.GetProperty("domain").GetString()!,
            root.GetProperty("baseDn").GetString()!,
            root.GetProperty("ouBaseDn").GetString()!);
    }
}

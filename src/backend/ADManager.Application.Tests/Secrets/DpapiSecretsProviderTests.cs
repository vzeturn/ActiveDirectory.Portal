using System.Security.Cryptography;
using System.Text;
using ADManager.Infrastructure.Secrets;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Secrets;

public sealed class DpapiSecretsProviderTests
{
    private static string Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    [Fact]
    public async Task GetServiceAccountAsync_DecryptsAndParsesJson()
    {
        var serviceAccountJson = """{"username":"svc-ad","password":"test-password","server":"dc01.corp.local","domain":"corp.local","baseDn":"DC=corp,DC=local","ouBaseDn":"OU=Users,DC=corp,DC=local"}""";
        var settings = new DpapiProtectedSettings
        {
            ServiceAccountEncrypted = Protect(serviceAccountJson),
            SessionSigningKeyEncrypted = Protect("super-secret-signing-key"),
        };
        var provider = new DpapiSecretsProvider(Options.Create(settings));

        var account = await provider.GetServiceAccountAsync();

        Assert.Equal("svc-ad", account.Username);
        Assert.Equal("corp.local", account.Domain);
    }

    [Fact]
    public async Task GetSessionSigningKeyAsync_DecryptsValue()
    {
        var settings = new DpapiProtectedSettings
        {
            ServiceAccountEncrypted = Protect("""{"username":"a","password":"b","server":"c","domain":"d","baseDn":"e","ouBaseDn":"f"}"""),
            SessionSigningKeyEncrypted = Protect("my-signing-key"),
        };
        var provider = new DpapiSecretsProvider(Options.Create(settings));

        var key = await provider.GetSessionSigningKeyAsync();

        Assert.Equal("my-signing-key", key);
    }

    [Fact]
    public async Task GetAlertHookUrlAsync_NullWhenNotConfigured()
    {
        var settings = new DpapiProtectedSettings
        {
            ServiceAccountEncrypted = Protect("""{"username":"a","password":"b","server":"c","domain":"d","baseDn":"e","ouBaseDn":"f"}"""),
            SessionSigningKeyEncrypted = Protect("key"),
            AlertHookUrlEncrypted = null,
        };
        var provider = new DpapiSecretsProvider(Options.Create(settings));

        var url = await provider.GetAlertHookUrlAsync();

        Assert.Null(url);
    }
}

using ADManager.Application.Abstractions;
using ADManager.Infrastructure.Auditing;
using ADManager.Infrastructure.Secrets;
using ADManager.Infrastructure.Sessions;
using ADManager.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ADManager.Application.Tests.Program;

public sealed class ServiceRegistrationTests
{
    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Secrets:ServiceAccountEncrypted"] = "ad-manager/service-account",
            ["Secrets:SessionSigningKeyEncrypted"] = "ad-manager/session-signing",
            ["Secrets:DiscordWebhookUrlEncrypted"] = "ad-manager/discord-webhook",
            ["ActiveDirectory:AdminGroupName"] = "Corp.IT.Admin",
            ["Storage:DatabasePath"] = Path.Combine(Path.GetTempPath(), "ad-manager-test.db")
        });
        return builder;
    }

    [Fact]
    public void RegisterServices_RegistersSqliteSessionServiceAsISessionService()
    {
        var builder = CreateBuilder();

        global::ServiceRegistration.RegisterServices(builder);

        var descriptor = builder.Services.Single(d => d.ServiceType == typeof(ISessionService));
        Assert.Equal(typeof(SqliteSessionService), descriptor.ImplementationType);
    }

    [Fact]
    public void RegisterServices_RegistersSqliteAuditLoggerAsIAuditLogger()
    {
        var builder = CreateBuilder();

        global::ServiceRegistration.RegisterServices(builder);

        var descriptor = builder.Services.Single(d => d.ServiceType == typeof(IAuditLogger));
        Assert.Equal(typeof(SqliteAuditLogger), descriptor.ImplementationType);
    }

    [Fact]
    public void RegisterServices_RegistersDpapiSecretsProviderAsISecretsProvider()
    {
        var builder = CreateBuilder();

        global::ServiceRegistration.RegisterServices(builder);

        var descriptor = builder.Services.Single(d => d.ServiceType == typeof(ISecretsProvider));
        Assert.Equal(typeof(DpapiSecretsProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void RegisterServices_DoesNotRegisterInMemorySessionServiceOrJsonLinesAuditLogger()
    {
        var builder = CreateBuilder();

        global::ServiceRegistration.RegisterServices(builder);

        Assert.DoesNotContain(builder.Services, d => d.ImplementationType?.Name == "InMemorySessionService");
        Assert.DoesNotContain(builder.Services, d => d.ImplementationType?.Name == "JsonLinesAuditLogger");
    }
}

using System.Threading.RateLimiting;
using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Api.HealthChecks;
using ADManager.Api.Middleware;
using ADManager.Api.Observability;
using ADManager.Api.Tools;
using ADManager.Application.Abstractions;
using ADManager.Application.UseCases;
using ADManager.Infrastructure.ActiveDirectory;
using Microsoft.Extensions.Caching.Memory;
using ADManager.Infrastructure.Auditing;
using ADManager.Infrastructure.Notifications;
using ADManager.Infrastructure.Passwords;
using ADManager.Infrastructure.Secrets;
using ADManager.Infrastructure.Sessions;
using ADManager.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

// ---- One-off operator CLI: `dotnet ADManager.Api.dll encrypt-secret-b64 "<base64-plaintext>"` ----
// Base64-encoded to sidestep PowerShell/Windows native-exe argv parsing, which silently
// strips embedded double quotes from a quoted argument (corrupting any JSON payload).
if (args.Length == 2 && args[0] == "encrypt-secret-b64")
{
    var plaintext = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
    Console.WriteLine(SecretEncryptionTool.Encrypt(plaintext));
    return;
}

// ---- Legacy plaintext form: `dotnet ADManager.Api.dll encrypt-secret "<plaintext>"` ----
// Kept for manual/interactive use where the operator controls quoting directly.
if (args.Length == 2 && args[0] == "encrypt-secret")
{
    Console.WriteLine(SecretEncryptionTool.Encrypt(args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: false);

SentrySetup.AddSentry(builder);
ServiceRegistration.RegisterServices(builder);
var app = builder.Build();

// ---- Operator CLI: `dotnet ADManager.Api.dll send-test-error` ----
// Sends one test error to Sentry (Sentry:Dsn). Runs before secrets load so it also works
// on a server whose AD configuration is broken.
if (args.Length >= 1 && args[0] == "send-test-error")
{
    Environment.ExitCode = await SentryTestCommand.RunAsync(app.Services);
    return;
}

// ---- Ensure the SQLite schema exists and eager-load secrets at startup ----
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var schemaInitializer = scope.ServiceProvider.GetRequiredService<SqliteSchemaInitializer>();
    var connectionFactory = scope.ServiceProvider.GetRequiredService<SqliteConnectionFactory>();
    schemaInitializer.EnsureCreated(connectionFactory);
    logger.LogInformation("SQLite schema verified at startup.");

    var secrets = scope.ServiceProvider.GetRequiredService<ISecretsProvider>();
    try
    {
        await secrets.RefreshAsync(CancellationToken.None);
        logger.LogInformation("Secrets loaded from local DPAPI-protected configuration at startup.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to load secrets at startup. The application cannot serve AD requests.");
        throw;
    }
}

// ---- Operator CLI: `dotnet ADManager.Api.dll send-test-alert` ----
// Posts one warning alert to the configured RT alert hook (Secrets:AlertHookUrlEncrypted)
// so an operator can prove the hub opens an incident request from this server.
if (args.Length >= 1 && args[0] == "send-test-alert")
{
    Environment.ExitCode = await AlertTestCommand.RunAsync(app.Services);
    return;
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AppCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/api/health/detail", async (
    IActiveDirectoryGateway gateway,
    ISecretsProvider secrets,
    IAuditLogger audit,
    CancellationToken ct) =>
{
    var adReachable = await gateway.CanBindAsync(ct);
    var secretsLoaded = true;
    try
    {
        await secrets.GetServiceAccountAsync(ct);
    }
    catch
    {
        secretsLoaded = false;
    }

    var alertHookConfigured = !string.IsNullOrWhiteSpace(await secrets.GetAlertHookUrlAsync(ct));

    var auditWritable = true;
    try
    {
        await audit.QueryAsync(new AuditQuery(null, null, null, null, 1, 1), ct);
    }
    catch
    {
        auditWritable = false;
    }

    return Results.Ok(ApiResponse<HealthDetail>.Ok(new HealthDetail(adReachable, secretsLoaded, auditWritable, alertHookConfigured)));
})
.RequireAuthorization(policy => policy
    .AddAuthenticationSchemes(SessionAuthConstants.AllSchemes)
    .RequireRole(SessionAuthConstants.AdminRole));

// Public browser settings (the Sentry DSN is designed to be public; it only allows sending events).
app.MapGet("/api/client-config", (SentrySettings sentry) => Results.Ok(ApiResponse<ClientConfig>.Ok(new ClientConfig(
    sentry.IsEnabled ? sentry.Dsn : null,
    sentry.Environment,
    Math.Clamp(sentry.TracesSampleRate, 0, 1)))))
    .AllowAnonymous();

app.MapFallbackToFile("index.html");

app.Run();

public static class ServiceRegistration
{
    public static void RegisterServices(WebApplicationBuilder builder)
    {
        // ---- Options -------------------------------------------------------------
        builder.Services.Configure<AdSettings>(builder.Configuration.GetSection("ActiveDirectory"));
        builder.Services.Configure<DpapiProtectedSettings>(builder.Configuration.GetSection("Secrets"));
        builder.Services.Configure<SqliteStorageSettings>(builder.Configuration.GetSection("Storage"));
        builder.Services.Configure<SessionSettings>(builder.Configuration.GetSection("Sessions"));
        builder.Services.Configure<AlertSettings>(builder.Configuration.GetSection("Alerts"));

        // ---- Storage --------------------------------------------------------------
        builder.Services.AddSingleton<SqliteConnectionFactory>();
        builder.Services.AddSingleton<SqliteSchemaInitializer>();

        // ---- DataProtection (file-system key ring, DPAPI-protected, spec §2.2) ----
        var keysPath = builder.Configuration["DataProtection:KeysPath"] ?? @"C:\ProgramData\ad-manager\keys";
        Directory.CreateDirectory(keysPath);
        builder.Services.AddDataProtection()
            .SetApplicationName("ADManager")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .ProtectKeysWithDpapi();

        // ---- Infrastructure -----------------------------------------------------
        builder.Services.AddSingleton<ISecretsProvider, DpapiSecretsProvider>();
        builder.Services.AddSingleton<ISessionService, SqliteSessionService>();
        builder.Services.AddSingleton<IAuditLogger, SqliteAuditLogger>();
        builder.Services.AddSingleton<IPasswordGenerator, PatternPasswordGenerator>();
        builder.Services.AddSingleton<IAdminGroupPolicy, ConfigAdminGroupPolicy>();
        builder.Services.AddSingleton<ISavedSearchRepository, SqliteSavedSearchRepository>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<AdCacheGenerationStore>();
        builder.Services.AddScoped<ActiveDirectoryGateway>();
        builder.Services.AddScoped<IActiveDirectoryGateway>(sp =>
            new CachedActiveDirectoryGateway(
                sp.GetRequiredService<ActiveDirectoryGateway>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<AdCacheGenerationStore>()));
        builder.Services.AddHttpClient<INotificationSender, RtAlertHookSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        // ---- Application use cases ----------------------------------------------
        builder.Services.AddScoped<LoginUseCase>();
        builder.Services.AddScoped<LookupUserUseCase>();
        builder.Services.AddScoped<LookupGroupUseCase>();
        builder.Services.AddScoped<ToggleAccountUseCase>();
        builder.Services.AddScoped<BulkAccountOpUseCase>();
        builder.Services.AddScoped<ResetPasswordUseCase>();
        builder.Services.AddScoped<BulkResetPasswordUseCase>();
        builder.Services.AddScoped<LdapsResetPasswordUseCase>();
        builder.Services.AddScoped<ManageGroupMembershipUseCase>();
        builder.Services.AddScoped<BulkGroupAssignmentUseCase>();
        builder.Services.AddScoped<ManageGroupUseCase>();
        builder.Services.AddScoped(sp =>
            new DashboardSummaryUseCase(
                sp.GetRequiredService<IActiveDirectoryGateway>(),
                sp.GetRequiredService<IAuditLogger>(),
                sp.GetRequiredService<IOptions<AdSettings>>().Value.PasswordExpiryWarningDays));

        // ---- Auth ---------------------------------------------------------------
        builder.Services
            .AddAuthentication(SessionAuthConstants.Scheme)
            .AddScheme<AuthenticationSchemeOptions, SessionCookieAuthenticationHandler>(
                SessionAuthConstants.Scheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                SessionAuthConstants.ApiKeyScheme, _ => { });
        builder.Services.AddAuthorization();

        // ---- Rate limiting --------------------------------------------------------
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });

        // ---- CORS (same-origin in production; dev origins from config) ----------
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? Array.Empty<string>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AppCors", policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        });

        // ---- Health checks ------------------------------------------------------
        builder.Services.AddHealthChecks()
            .AddCheck<ActiveDirectoryHealthCheck>("active-directory", tags: new[] { "ready" })
            .AddCheck<SecretsHealthCheck>("secrets", tags: new[] { "ready" });

        builder.Services.AddControllers();
    }
}

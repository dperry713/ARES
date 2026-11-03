using System.Threading.RateLimiting;
using Ares.AttackGraph.Services;
using Ares.Api.Auth;
using Ares.Approvals.Services;
using Ares.Engagements;
using Ares.Evidence.Services;
using Ares.Execution.Services;
using Ares.Identity.Auth;
using Ares.Identity.Services;
using Ares.Infrastructure.Audit;
using Ares.Infrastructure.Journal;
using Ares.Policy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Ares.Api.Platform;

/// <summary>
/// Composition root for the platform. Everything security-relevant is wired
/// explicitly here — there is no ambient fallback that could run unauthenticated
/// or unbejuraled in production.
/// </summary>
public static class AresPlatform
{
    public const string DataDirectoryConfigKey = "Ares:DataDirectory";
    public const string RateLimitConfigKey = "Ares:RateLimit:PermitPerMinute";

    public static WebApplicationBuilder AddAresPlatform(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var environment = builder.Environment;

        // ---- Durable state -------------------------------------------------
        var dataDirectory = config["ARES_DATA_DIR"]
            ?? config[DataDirectoryConfigKey]
            ?? Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataDirectory);

        var journalPath = Path.Combine(dataDirectory, "ares.journal.db");

        // PostgreSQL-backed state journal (optional, fallback to SQLite)
        var postgresConnectionString = config["ARES_POSTGRES_CONNECTION_STRING"];
        IStateJournal journal;
        if (!string.IsNullOrWhiteSpace(postgresConnectionString))
        {
            var pgJournal = new PostgresStateJournal(postgresConnectionString);
            journal = pgJournal;
            services.AddSingleton<IStateJournal>(pgJournal);
            services.AddSingleton<IAuditLog, JournalAuditLog>();
            Console.WriteLine($"Using PostgreSQL state journal (connection: {postgresConnectionString.Substring(0, Math.Min(30, postgresConnectionString.Length))}...)");
        }
        else
        {
            journal = new SqliteStateJournal(journalPath);
            services.AddSingleton<IStateJournal>(journal);
            services.AddSingleton<IAuditLog, JournalAuditLog>();
            Console.WriteLine("Using SQLite state journal (local file: " + journalPath + ")");
        }

        // PostgreSQL-backed evidence store (optional)
        var postgresEvidenceConnectionString = config["ARES_POSTGRES_EVIDENCE_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(postgresEvidenceConnectionString))
        {
            var pgEvidenceService = new PostgresEvidenceService(postgresEvidenceConnectionString);
            services.AddSingleton(typeof(EvidenceService), pgEvidenceService);
            Console.WriteLine($"Using PostgreSQL evidence store (connection: {postgresEvidenceConnectionString.Substring(0, Math.Min(30, postgresEvidenceConnectionString.Length))}...)");
        }
        else
        {
            Console.WriteLine("Using in-memory evidence store (PostgreSQL not configured)");
            services.AddSingleton<EvidenceService>();
        }

        // ---- Credentials ---------------------------------------------------
        var authOptions = ConfiguredApiKeyStore.LoadOptions(config, environment.IsProduction());
        AresStartup.BootstrapKey = null;
        if (authOptions.Keys.Length == 0 && string.IsNullOrWhiteSpace(authOptions.BootstrapAdminKey))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException(
                    "Production requires API keys: set Ares:Auth:Keys or ARES_BOOTSTRAP_ADMIN_KEY.");

            // Local development convenience: one ephemeral admin credential,
            // printed once at startup. Never persisted, never default.
            var generated = ApiKeyHasher.GenerateKey();
            authOptions.BootstrapAdminKey = generated;
            AresStartup.BootstrapKey = generated;
        }

        var apiKeyStore = new ConfiguredApiKeyStore(authOptions);
        services.AddSingleton<IApiKeyStore>(apiKeyStore);

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization(AresPolicies.Register);

        // ---- Domain services ----------------------------------------------
        services.AddSingleton<ITenantService>(_ => new TenantService(journal));
        services.AddSingleton<IEngagementService>(_ => new EngagementService(journal));
        services.AddSingleton<IPolicyEngine, PolicyEngine>();
        services.AddSingleton<IApprovalService>(_ => new ApprovalService(journal));
        services.AddSingleton<IExecutionService>(sp => new ExecutionService(
            sp.GetRequiredService<IPolicyEngine>(),
            sp.GetRequiredService<IEngagementService>(),
            sp.GetRequiredService<IApprovalService>(),
            journal));
        services.AddSingleton<EvidenceService>();
        services.AddSingleton<IAttackGraphService, AttackGraphService>();

        // ---- HTTP hardening ------------------------------------------------
        var permitPerMinute = config.GetValue(RateLimitConfigKey, 240);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = context.User.Identity?.IsAuthenticated == true
                    ? "sub:" + context.User.SubjectId()
                    : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://ares.local/errors/rate-limited",
                    title = "Too many requests",
                    status = 429,
                    detail = $"Limit is {permitPerMinute} requests per minute per caller.",
                    traceId = context.HttpContext.TraceIdentifier
                }, cancellationToken);
            };
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<AresExceptionHandler>();

        services.AddHealthChecks()
            .AddCheck<JournalReadinessCheck>("journal", tags: ["ready"]);

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "ARES Security Assessment Platform API",
                    Version = "v1",
                    Description =
                        "Authorized, human-gated security assessment orchestration. " +
                        "All endpoints require an API key supplied in the X-Ares-Api-Key header. " +
                        "Execution is denied unless the engagement is active, in window, in scope, " +
                        "and (where required) bound to an unexpired human approval."
                };
                var components = document.Components ?? new OpenApiComponents();
                document.Components = components;
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["AresApiKey"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = ApiKeyAuthenticationHandler.HeaderName,
                    Description = "Platform API key. Issued server-side; only its SHA-256 is stored."
                };
                // Microsoft.OpenApi 2.x models global security as document.Security.
                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("AresApiKey", document, null)] = new List<string>()
                });
                return Task.CompletedTask;
            });
        });

        // ---- Host limits ----------------------------------------------------
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = 1024 * 1024; // 1 MiB
            kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
            kestrel.AddServerHeader = false;
        });

        AresStartup.DataDirectory = dataDirectory;
        AresStartup.JournalPath = journalPath;
        AresStartup.KeyDescriptors = apiKeyStore.Describe();
        return builder;
    }
}

/// <summary>Startup facts the host logs once. Never contains secret material.</summary>
public static class AresStartup
{
    public static string? BootstrapKey { get; set; }
    public static string DataDirectory { get; set; } = string.Empty;
    public static string JournalPath { get; set; } = string.Empty;
    public static IReadOnlyList<ApiKeyDescriptor> KeyDescriptors { get; set; } = [];
}

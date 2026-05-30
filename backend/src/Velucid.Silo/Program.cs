using KurrentDB.Client;
using Microsoft.EntityFrameworkCore;
using Orleans.Configuration;
using StackExchange.Redis;
using Velucid.ReadModel;
using Velucid.Silo.Authorization;
using Velucid.Silo.Configuration;
using Velucid.Silo.Events;
using Velucid.Silo.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Redis (shared connection for Orleans clustering + token store) ──
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? "velucid-redis:6379";
var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);

// Register Redis connection for token store
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(configurationOptions));

// ─── Orleans Silo ────────────────────────────────────────────────
builder.UseOrleans(siloBuilder =>
{
    siloBuilder
        .UseKubernetesHosting()
        .UseRedisClustering(options =>
            options.ConfigurationOptions = configurationOptions);

    siloBuilder
        .ConfigureEndpoints(
            siloPort: 11111,
            gatewayPort: 30000)
        .AddMemoryGrainStorageAsDefault()
        .Configure<GrainCollectionOptions>(options =>
        {
            options.CollectionAge = TimeSpan.FromMinutes(30);
        });
});

// ─── KurrentDB Client (singleton, injected into grains via DI) ──
var kurrentDbConnectionString = builder.Configuration
    .GetSection(KurrentDbOptions.SectionName)
    .Get<KurrentDbOptions>()?.ConnectionString
    ?? KurrentDbOptions.DefaultConnectionString;

builder.Services.AddKurrentDBClient(kurrentDbConnectionString);
builder.Services.AddSingleton<IEventStreamClient, KurrentDbStreamClient>();

// ─── Email Verification Store (Redis-backed) ──────────────────────
builder.Services.AddSingleton<IEmailVerificationStore, RedisEmailVerificationStore>();

// ─── Read Model (PostgreSQL) ────────────────────────────────────
var readModelConnectionString = builder.Configuration["ConnectionStrings:PostgreSQL"]
    ?? "Host=localhost;Port=5432;Database=velucid_readmodel;Username=velucid;Password=velucid";

builder.Services.AddDbContext<ReadModelDbContext>(options =>
    options.UseNpgsql(readModelConnectionString, npgsql =>
        npgsql.MigrationsAssembly("Velucid.ReadModel.Migrations")));

// ─── Application Services ────────────────────────────────────────
builder.Services.AddSingleton<ISignInService, SignInService>();

// ─── OpenFGA Authorization ────────────────────────────────────────
builder.Services.Configure<OpenFgaOptions>(
    builder.Configuration.GetSection(OpenFgaOptions.SectionName));
builder.Services.AddSingleton<IOpenFgaAuthorizationService, OpenFgaAuthorizationService>();
builder.Services.AddSingleton<IOpenFgaInitializer, OpenFgaInitializer>();

// ─── Event Type Registrations ────────────────────────────────────
EventTypeMapping.Register<UserRegisteredEvent>("UserCreated");
EventTypeMapping.Register<IdentityInfoUpdatedEvent>("IdentityInfoUpdated");
EventTypeMapping.Register<UserProfileUpdatedEvent>("UserProfileUpdated");
EventTypeMapping.Register<EmailVerificationRequestedEvent>("EmailVerificationRequested");
EventTypeMapping.Register<EmailVerifiedEvent>("EmailVerified");
EventTypeMapping.Register<IdentityLinkedToUserEvent>("IdentityLinkedToUser");
EventTypeMapping.Register<OrgCreatedEvent>("OrgCreated");
EventTypeMapping.Register<OrgRenamedEvent>("OrgRenamed");
EventTypeMapping.Register<OrgDeletedEvent>("OrgDeleted");
EventTypeMapping.Register<MemberAddedEvent>("MemberAdded");
EventTypeMapping.Register<MemberRemovedEvent>("MemberRemoved");
EventTypeMapping.Register<InvitationSentEvent>("InvitationSent");
EventTypeMapping.Freeze();

// ─── Co-hosted ASP.NET Core API ──────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ─── OpenFGA Initialization ────────────────────────────────────────
var openFgaInitializer = app.Services.GetRequiredService<IOpenFgaInitializer>();
await openFgaInitializer.InitializeAsync();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
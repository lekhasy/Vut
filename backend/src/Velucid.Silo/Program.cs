using KurrentDB.Client;
using Orleans.Configuration;
using Velucid.Silo.Configuration;
using Velucid.Silo.Events;

var builder = WebApplication.CreateBuilder(args);

// ─── Orleans Silo ────────────────────────────────────────────────
builder.UseOrleans(siloBuilder =>
{
    siloBuilder
        .UseAdoNetClustering(options =>
        {
            options.ConnectionString = builder.Configuration
                .GetConnectionString("PostgreSQL");
            options.Invariant = "Npgsql";
        })
        .ConfigureEndpoints(
            siloPort: 11111,
            gatewayPort: 30000)
        .AddMemoryGrainStorageAsDefault()
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "velucid-cluster";
            options.ServiceId = "velucid";
        })
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

// ─── Event Type Registrations ────────────────────────────────────
EventTypeMapping.Register<UserCreatedEvent>("UserCreated");
EventTypeMapping.Register<IdentityLinkedEvent>("IdentityLinked");
EventTypeMapping.Register<UserProfileUpdatedEvent>("UserProfileUpdated");
EventTypeMapping.Register<EmailVerificationRequestedEvent>("EmailVerificationRequested");
EventTypeMapping.Register<EmailVerifiedEvent>("EmailVerified");
EventTypeMapping.Freeze();

// ─── Co-hosted ASP.NET Core API ──────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

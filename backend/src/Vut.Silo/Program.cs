using EventStore.Client;
using Orleans.Configuration;
using Vut.Silo.Configuration;
using Vut.Silo.Events;

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
            options.ClusterId = "vut-cluster";
            options.ServiceId = "vut";
        })
        .Configure<GrainCollectionOptions>(options =>
        {
            options.CollectionAge = TimeSpan.FromMinutes(30);
        });
});

// ─── KurrentDB Client (singleton, injected into grains via DI) ──
var kurrentDbOptions = builder.Configuration
    .GetSection(KurrentDbOptions.SectionName)
    .Get<KurrentDbOptions>() ?? new KurrentDbOptions();

builder.Services.AddSingleton(
    new EventStoreClient(
        EventStoreClientSettings.Create(kurrentDbOptions.ConnectionString)));

// ─── Event Type Registrations ────────────────────────────────────
// Concrete event types are registered here by Tasks 05/06.
// Example:
//   EventTypeMapping.Register<UserCreatedEvent>("UserCreated");
//   EventTypeMapping.Register<OrganizationCreatedEvent>("OrganizationCreated");
EventTypeMapping.Freeze();

// ─── Co-hosted ASP.NET Core API ──────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

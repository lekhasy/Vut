using KurrentDB.Client;
using Microsoft.EntityFrameworkCore;
using Velucid.ProjectorService.Configuration;
using Velucid.ProjectorService.Handlers;
using Velucid.ProjectorService.Services;
using Velucid.ReadModel;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<KurrentDbOptions>(
    builder.Configuration.GetSection(KurrentDbOptions.SectionName));
builder.Services.Configure<PostgresOptions>(
    builder.Configuration.GetSection(PostgresOptions.SectionName));
builder.Services.Configure<OpenFgaOptions>(
    builder.Configuration.GetSection(OpenFgaOptions.SectionName));

// KurrentDB client
var kurrentDbConnectionString = builder.Configuration
    .GetSection(KurrentDbOptions.SectionName)
    .Get<KurrentDbOptions>()?.ConnectionString
    ?? KurrentDbOptions.DefaultConnectionString;
var kurrentDbSettings = KurrentDBClientSettings.Create(kurrentDbConnectionString);
builder.Services.AddSingleton(new KurrentDBClient(kurrentDbSettings));
builder.Services.AddSingleton(new KurrentDBPersistentSubscriptionsClient(kurrentDbSettings));

// PostgreSQL via EF Core
var postgresConnectionString = builder.Configuration
    .GetSection(PostgresOptions.SectionName)
    .Get<PostgresOptions>()?.ConnectionString
    ?? PostgresOptions.DefaultConnectionString;

builder.Services.AddDbContext<ReadModelDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

// OpenFGA tuple sync
builder.Services.AddSingleton<OpenFgaTupleSync>();

// Projectors
builder.Services.AddHostedService<UserProjector>();
builder.Services.AddHostedService<OrgProjector>();

var host = builder.Build();
host.Run();
using Worker.BackgroundServices;
using Application;
using Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Register dependencies
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ArticleFetchingService>();
builder.Services.AddHostedService<ScoringWorkerService>();
builder.Services.AddHostedService<DailyAggregationService>();
builder.Services.AddHostedService<CleanUpService>();

var host = builder.Build();
host.Run();

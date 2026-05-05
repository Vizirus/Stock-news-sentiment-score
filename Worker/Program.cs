using Worker.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<ArticleFetchingService>();
builder.Services.AddHostedService<ScoringWorkerService>();
builder.Services.AddHostedService<DailyAggregationService>();
builder.Services.AddHostedService<CleanUpService>();

var host = builder.Build();
host.Run();

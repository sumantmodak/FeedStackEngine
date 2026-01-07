using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Application Insights telemetry (optional)
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register HttpClient factory for feed fetching
        services.AddHttpClient();

        // Placeholder service registrations (to be implemented in later phases)
        // services.AddSingleton<IFeedConfigService, FeedConfigService>();
        // services.AddScoped<IFeedParserService, FeedParserService>();
        // services.AddScoped<IArticleTableService, ArticleTableService>();
    })
    .Build();

host.Run();

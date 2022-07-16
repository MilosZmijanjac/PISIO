using FileService;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "localhost:6378";
        });
    })
    .Build();

await host.RunAsync();

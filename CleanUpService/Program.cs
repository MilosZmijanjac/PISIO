using CleanUpService;
using Coravel;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
       // services.AddHostedService<Worker>();
        services.AddScheduler();
        services.AddTransient<Worker>();
    })
    .Build();
host.Services.UseScheduler(scheduler => {
    scheduler
         .Schedule<Worker>().Cron("0 0 */2 * *");
         
});
await host.RunAsync();

using GifService;
using MessageQueue;
using RabbitMQ.Client;

var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME");
var connectionFactory = new ConnectionFactory
{
    HostName = rabbitHostName ?? "localhost",
    Port = 5672,
    UserName = "gif_service",
    Password = "123"
};
var rabbitMqConnection = connectionFactory.CreateConnection();
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton(rabbitMqConnection);
        services.AddSingleton<IRabbitMQClient, RabbitMQClient>();
        services.AddStackExchangeRedisCache(options =>
        {
            //options.ConfigurationOptions.Password = "eYVX7EwVmmxKPCDmwMtyKVge8oLd2t81";
            options.Configuration = "localhost:6378";
        });
    })
    .Build();

await host.RunAsync();

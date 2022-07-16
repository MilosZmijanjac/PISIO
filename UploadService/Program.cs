using RabbitMQ.Client;
using System.Text.Json;
using MessageQueue;
using MessageQueue.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Cors;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME");
//var redisHostName = Environment.GetEnvironmentVariable("REDIS_HOSTNAME");
var connectionFactory = new ConnectionFactory
{
    HostName = rabbitHostName ?? "localhost",
    Port = 5672,
    UserName = "upload_service",
    Password = "123"
};
var rabbitMqConnection = connectionFactory.CreateConnection();
builder.Services.AddSingleton(rabbitMqConnection);
builder.Services.AddSingleton<IRabbitMQClient, RabbitMQClient>();
builder.Services.AddStackExchangeRedisCache(options =>
{
   // options.ConfigurationOptions.Password = "eYVX7EwVmmxKPCDmwMtyKVge8oLd2t81";
    options.Configuration = "localhost:6378";
});
builder.Services.AddCors();
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(p =>
{
    p.AllowAnyOrigin();
    p.AllowAnyMethod();
    p.AllowAnyHeader();
});
app.UseHttpsRedirection();

app.Lifetime.ApplicationStopping.Register(() =>
{
    var rabbitMqClient = app.Services.GetRequiredService<IRabbitMQClient>();
    rabbitMqClient.CloseConnection();
});


app.MapPost("/upload",async (IRabbitMQClient rabbitMqClient, IDistributedCache distributedCache, HttpRequest request) =>
{
    try
    {
        var ticks = new DateTime(2016, 1, 1).Ticks;
        var ans = DateTime.Now.Ticks - ticks;
        var jobId = ans.ToString("x");
        Console.WriteLine(jobId);
        var form = await request.ReadFormAsync();
        FileJob fileJob = new FileJob();
        fileJob.Id = jobId;
        fileJob.Files = new List<Byte[]>(form.Count);
        fileJob.Extension = ".img";
        foreach (var file in form.Files)
        {
            await using Stream input = file.OpenReadStream();
            var mem = new MemoryStream();
            input.CopyTo(mem);
            fileJob.Files.Add(mem.ToArray());
        }
        string payload = JsonSerializer.Serialize(fileJob);

        rabbitMqClient.Publish("UploadService", "upload_service", "upload_ocr", "upload.ocr_service", payload);
        rabbitMqClient.Publish("UploadService", "upload_service", "upload_gif", "upload.gif_service", payload);
        var options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.Now.AddMinutes(15))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));
         distributedCache.SetString($"JOB:{jobId}", "UPLOADED");
        
        return Results.Ok(jobId);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex);
    }
});

app.Run();

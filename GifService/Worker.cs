using AnimatedGif;
using MessageQueue;
using MessageQueue.Models;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Drawing;
using System.Text;
using System.Text.Json;

namespace GifService;

public class Worker : BackgroundService
{

    private readonly ILogger<Worker> _logger;
    private ConnectionFactory _connectionFactory;
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly IDistributedCache _distributedCache;
    private IConnection _connection;
    private IModel _channel;
    private const string QueueName = "upload.gif_service";

    public Worker(ILogger<Worker> logger, IRabbitMQClient rabbitMqClient, IDistributedCache distributedCache)
    {
        _logger = logger;
        _rabbitMqClient = rabbitMqClient;
        _distributedCache = distributedCache;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME");
        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitHostName ?? "localhost",
            Port = 5672,
            UserName = "gif_service",
            Password = "123",
            DispatchConsumersAsync = true
        };
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclarePassive(QueueName);
        _channel.BasicQos(0, 1, false);
        _logger.LogInformation($"Queue [{QueueName}] is waiting for messages.");

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var messageCount = _channel.MessageCount(QueueName);
        if (messageCount > 0)
        {
            _logger.LogInformation($"\tDetected {messageCount} message(s).");
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (bc, ea) =>
        {
            if (ea.BasicProperties.UserId != "upload_service")
            {
                _logger.LogInformation($"\tIgnored a message sent by [{ea.BasicProperties.UserId}].");
                return;
            }

            var t = DateTimeOffset.FromUnixTimeMilliseconds(ea.BasicProperties.Timestamp.UnixTime);
            _logger.LogInformation($"{t.LocalDateTime:O} ID=[{ea.BasicProperties.MessageId}]");
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                var fileJob = JsonSerializer.Deserialize<FileJob>(message);
                if (_distributedCache.GetString($"JOB:{fileJob.Id}") == "ABORT")
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                _distributedCache.SetString($"JOB:{fileJob.Id}", "GIF-START");
                var newFileJob = new FileJob
                {
                    Id = fileJob.Id,
                    Extension = ".gif",
                    Files = new List<byte[]>()
                };
                if (fileJob == null)
                    return;
                var mem = new MemoryStream();
       
                using var gif = new AnimatedGifCreator(mem, 1000);
                foreach (var file in fileJob.Files)
                {
                    var img = Image.FromStream(new MemoryStream(file));
                    gif.AddFrame(img, delay: -1, quality: GifQuality.Bit8);
                }


                newFileJob.Files.Add(mem.ToArray());
                if (_distributedCache.GetString($"JOB:{fileJob.Id}") == "ABORT")
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                _rabbitMqClient.Publish("GifService", "gif_service", "gif_file", "gif.file_service", JsonSerializer.Serialize(newFileJob));
                await Task.Delay(2000, stoppingToken);
                _distributedCache.SetString($"JOB:{fileJob.Id}", "GIF-DONE");
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (JsonException)
            {
                _logger.LogError($"JSON Parse Error: '{message}'.");
                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
            catch (AlreadyClosedException)
            {
                _logger.LogInformation("RabbitMQ is closed!");
            }
            catch (Exception e)
            {
                _logger.LogError(default, e, e.Message);
            }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
        await Task.Delay(500, stoppingToken);
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        _connection.Close();
        _rabbitMqClient.CloseConnection();
        _logger.LogInformation("RabbitMQ connection is closed.");
    }
}

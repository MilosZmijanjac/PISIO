using MessageQueue.Models;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FileService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private ConnectionFactory _connectionFactory;
        private readonly IDistributedCache _distributedCache;
        private IConnection _connection;
        private IModel _channel;
        private const string QueueName = "gif_pdf.file_service";
        private readonly string folder = @"C:\Users\SnakeKD\Desktop\PISIO\Storage";

        public Worker(ILogger<Worker> logger, IDistributedCache distributedCache)
        {
            _logger = logger;
            _distributedCache = distributedCache;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME");
            _connectionFactory = new ConnectionFactory
            {
                HostName = rabbitHostName ?? "localhost",
                Port = 5672,
                UserName = "file_service",
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
                if (ea.BasicProperties.UserId != "gif_service"&& ea.BasicProperties.UserId != "pdf_service")
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


                    if (fileJob == null)
                    {
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }
                    if(!Directory.Exists(Path.Combine(folder,fileJob.Id)))
                        Directory.CreateDirectory(Path.Combine(folder, fileJob.Id));

                    await using (var fs = new FileStream(Path.Combine(Path.Combine(folder, fileJob.Id), fileJob.Id + fileJob.Extension), FileMode.Create))
                    {
                        await new MemoryStream(fileJob.Files.First()).CopyToAsync(fs);
                    }
                    if (Directory.GetFiles(Path.Combine(folder, fileJob.Id)).Count() == 2) 
                    {
                        _distributedCache.SetString($"JOB:{fileJob.Id}", "FILE-START");
                        ZipFile.CreateFromDirectory(Path.Combine(folder, fileJob.Id), Path.Combine(folder, fileJob.Id + ".zip"), CompressionLevel.Fastest,true);
                        File.Move(Path.Combine(folder, fileJob.Id + ".zip"), Path.Combine(Path.Combine(folder, fileJob.Id), fileJob.Id + ".zip"));
                    }
                   
                    if (_distributedCache.GetString($"JOB:{fileJob.Id}") == "ABORT")
                    {
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }
                    
                    await Task.Delay(2000, stoppingToken);
                    if (Directory.GetFiles(Path.Combine(folder, fileJob.Id)).Count() == 3)
                    {
                        _distributedCache.SetString($"JOB:{fileJob.Id}", "FILE-DONE");
                    }
                    
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
            _logger.LogInformation("RabbitMQ connection is closed.");
        }
    }
}
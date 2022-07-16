using iTextSharp.text;
using iTextSharp.text.pdf;
using MessageQueue;
using MessageQueue.Models;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace PdfService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private ConnectionFactory _connectionFactory;
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly IDistributedCache _distributedCache;
    private IConnection _connection;
    private IModel _channel;
    private const string QueueName = "ocr.pdf_service";

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
            UserName = "pdf_service",
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
            if (ea.BasicProperties.UserId != "ocr_service")
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
                _distributedCache.SetString($"JOB:{fileJob.Id}", "PDF-START");
                var newFileJob = new FileJob
                {
                    Id = fileJob.Id,
                    Extension = ".pdf",
                    Files = new List<byte[]>()
                };
                if (fileJob == null)
                    return;
                MemoryStream mem = new MemoryStream();
                using var pdfDoc = new Document(PageSize.A4, 40, 40, 80, 40);
                using var writer = PdfWriter.GetInstance(pdfDoc, mem);
                pdfDoc.Open();
                foreach (var file in fileJob.Files)
                {
                    pdfDoc.NewPage();
                    pdfDoc.Add(new Paragraph(Encoding.Latin1.GetString(file)));
                }
                pdfDoc.Close();
                newFileJob.Files.Add(mem.ToArray());
                if (_distributedCache.GetString($"JOB:{fileJob.Id}") == "ABORT")
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                _rabbitMqClient.Publish("PdfService", "pdf_service", "pdf_file", "pdf.file_service", JsonSerializer.Serialize(newFileJob));
                await Task.Delay(2000, stoppingToken);
                _distributedCache.SetString($"JOB:{fileJob.Id}", "PDF-DONE");
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

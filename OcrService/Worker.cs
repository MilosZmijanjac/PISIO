using MessageQueue;
using MessageQueue.Models;
using Microsoft.Extensions.Caching.Distributed;
using Patagames.Ocr;
using Patagames.Ocr.Enums;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using TesseractOCR;
using TesseractOCR.Enums;

namespace OcrService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly IDistributedCache _distributedCache;
    private ConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IModel _channel;
    private const string QueueName = "upload.ocr_service";

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
            UserName = "ocr_service",
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
                    
                _distributedCache.SetString($"JOB:{fileJob.Id}", "OCR-START");
                var newFileJob = new FileJob
                {
                    Id = fileJob.Id,
                    Extension = ".txt",
                    Files = new List<byte[]>()
                };
                if (fileJob == null)
                    return;
                foreach (var file in fileJob.Files)
                {
                    using var api = OcrApi.Create() ;
                    api.Init(Languages.English);

                    newFileJob.Files.Add(Encoding.Latin1.GetBytes(api.GetTextFromImage(MakeGrayscale3(new Bitmap(new MemoryStream(file))))));
                    
                }
                if (_distributedCache.GetString($"JOB:{fileJob.Id}") == "ABORT")
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                _rabbitMqClient.Publish("OcrService", "ocr_service", "ocr_pdf", "ocr.pdf_service", JsonSerializer.Serialize(newFileJob));
                await Task.Delay(2000, stoppingToken);
                _distributedCache.SetString($"JOB:{fileJob.Id}", "OCR-DONE");
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
    public static Bitmap MakeGrayscale3(Bitmap original)
    {
        //create a blank bitmap the same size as original
        Bitmap newBitmap = new Bitmap(original.Width, original.Height);
        //get a graphics object from the new image
        Graphics g = Graphics.FromImage(newBitmap);
        //create the grayscale ColorMatrix
        ColorMatrix colorMatrix = new ColorMatrix(
           new float[][]
          {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
          });
        //create some image attributes
        ImageAttributes attributes = new ImageAttributes();
        //set the color matrix attribute
        attributes.SetColorMatrix(colorMatrix);
        //draw the original image on the new image
        //using the grayscale color matrix
        g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
           0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        //dispose the Graphics object
        g.Dispose();
        return newBitmap;
    }
}

using RabbitMQ.Client;
using System.Text;

namespace MessageQueue;

public class RabbitMQClient : IRabbitMQClient
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMQClient(IConnection connection)
    {
        _connection = connection;
        _channel = _connection.CreateModel();
        _channel.ConfirmSelect();
    }

    public void Publish(string appId, string userId, string exchange, string routingKey, string payload)
    {
        var props = _channel.CreateBasicProperties();
        props.AppId = appId;
        props.Persistent = true;
        props.UserId = userId;
        props.MessageId = Guid.NewGuid().ToString("N");
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var body = Encoding.UTF8.GetBytes(payload);
        _channel.BasicPublish(exchange, routingKey, props, body);
        _channel.WaitForConfirmsOrDie(new TimeSpan(0, 0, 5));
    }

    public void CloseConnection()
    {
        _connection?.Close();
    }
}

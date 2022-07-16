namespace MessageQueue;

public interface IRabbitMQClient
{
    void CloseConnection();
    void Publish(string appId, string userId, string exchange, string routingKey, string payload);
}

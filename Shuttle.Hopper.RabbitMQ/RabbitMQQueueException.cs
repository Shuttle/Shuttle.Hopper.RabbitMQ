namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQQueueException : Exception
{
    public RabbitMQQueueException(string message) : base(message)
    {
    }
}
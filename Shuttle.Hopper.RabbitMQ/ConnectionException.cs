namespace Shuttle.Hopper.RabbitMQ;

internal class ConnectionException : Exception
{
    public ConnectionException(string message) : base(message)
    {
    }
}
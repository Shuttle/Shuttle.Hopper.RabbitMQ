using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQBuilder
{
    internal readonly Dictionary<string, Action<RabbitMQOptions>> RabbitMQConfigureOptions = new();

    public RabbitMQBuilder Configure(string name, Action<RabbitMQOptions> configureOptions)
    {
        Guard.AgainstEmpty(name);
        Guard.AgainstNull(configureOptions);

        RabbitMQConfigureOptions.Remove(name);
        RabbitMQConfigureOptions.Add(name, configureOptions);

        return this;
    }
}
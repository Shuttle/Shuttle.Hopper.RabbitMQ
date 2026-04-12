using Microsoft.Extensions.DependencyInjection;
using Shuttle.Contract;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQBuilder(IServiceCollection services)
{
    internal readonly Dictionary<string, Action<RabbitMQOptions>> RabbitMQConfigureOptions = new();

    public RabbitMQBuilder Configure(string name, Action<RabbitMQOptions> configureOptions)
    {
        Guard.AgainstNull(services)
            .AddOptions<RabbitMQOptions>(Guard.AgainstEmpty(name))
            .Configure(Guard.AgainstNull(configureOptions));

        return this;
    }
}
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQBuilder(IServiceCollection services)
{
    internal readonly Dictionary<string, RabbitMQOptions> RabbitMQOptions = new();

    public IServiceCollection Services { get; } = Guard.AgainstNull(services);

    public RabbitMQBuilder AddOptions(string name, RabbitMQOptions rabbitMQOptions)
    {
        Guard.AgainstEmpty(name);
        Guard.AgainstNull(rabbitMQOptions);

        RabbitMQOptions.Remove(name);

        RabbitMQOptions.Add(name, rabbitMQOptions);

        return this;
    }
}
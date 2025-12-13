using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQQueueFactory(IOptions<ServiceBusOptions> serviceBusOptions, IOptionsMonitor<RabbitMQOptions> rabbitMQOptions) : ITransportFactory
{
    private readonly ServiceBusOptions _serviceBusOptions = Guard.AgainstNull(Guard.AgainstNull(serviceBusOptions).Value);
    private readonly IOptionsMonitor<RabbitMQOptions> _rabbitMQOptions = Guard.AgainstNull(rabbitMQOptions);

    public string Scheme => "rabbitmq";

    public Task<ITransport> CreateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var transportUri = new TransportUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var rabbitMQOptions = _rabbitMQOptions.Get(transportUri.ConfigurationName);

        if (rabbitMQOptions == null)
        {
            throw new InvalidOperationException(string.Format(Hopper.Resources.TransportConfigurationNameException, transportUri.ConfigurationName));
        }

        return Task.FromResult<ITransport>(new RabbitMQQueue(_serviceBusOptions, rabbitMQOptions, transportUri));
    }
}
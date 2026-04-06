using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public class RabbitMQDeferredMessageFixture : DeferredFixture
{
    [Test]
    public async Task Should_be_able_to_perform_full_processing_async()
    {
        await TestDeferredProcessingAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}");
    }
}
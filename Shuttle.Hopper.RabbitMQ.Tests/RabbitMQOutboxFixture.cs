using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public class RabbitMQOutboxFixture : OutboxFixture
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_be_able_to_use_an_outbox_async(bool isTransactionalEndpoint)
    {
        await TestOutboxSendingAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", 3, isTransactionalEndpoint);
    }
}
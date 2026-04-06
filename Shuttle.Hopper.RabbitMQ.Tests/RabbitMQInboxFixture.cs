using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public class RabbitMQInboxFixture : InboxFixture
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_be_able_handle_errors_async(bool hasErrorQueue)
    {
        await TestInboxErrorAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", hasErrorQueue);
    }

    [Test]
    public async Task Should_be_able_to_expire_a_message_async()
    {
        await TestInboxExpiryAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}");
    }

    [Test]
    public async Task Should_be_able_to_handle_a_deferred_message_async()
    {
        await TestInboxDeferredAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", TimeSpan.FromMilliseconds(500));
    }

    [Test]
    public async Task Should_be_able_to_process_messages_concurrently_async()
    {
        await TestInboxConcurrencyAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", TimeSpan.FromMinutes(1));
    }
}
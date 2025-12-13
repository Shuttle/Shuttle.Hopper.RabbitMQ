using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public class RabbitMQInboxFixture : InboxFixture
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task Should_be_able_handle_errors_async(bool hasErrorQueue, bool isTransactionalEndpoint)
    {
        await TestInboxErrorAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", hasErrorQueue, isTransactionalEndpoint);
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

    [TestCase(250, false)]
    [TestCase(250, true)]
    public async Task Should_be_able_to_process_messages_concurrently_async(int msToComplete, bool isTransactionalEndpoint)
    {
        await TestInboxConcurrencyAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}", msToComplete, isTransactionalEndpoint);
    }
}
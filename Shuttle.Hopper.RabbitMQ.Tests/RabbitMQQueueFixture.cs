using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.RabbitMQ.Tests;

[TestFixture]
public class RabbitMQQueueFixture : BasicTransportFixture
{
    [Test]
    public async Task Should_be_able_to_perform_simple_send_and_receive_async()
    {
        await TestSimpleSendAndReceiveAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}");
        await TestSimpleSendAndReceiveAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}-transient?durable=false");
    }

    [Test]
    public async Task Should_be_able_to_release_a_message_async()
    {
        await TestReleaseMessageAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}");
    }

    [Test]
    public async Task Should_be_able_to_get_message_again_when_not_acknowledged_before_queue_is_disposed_async()
    {
        await TestUnacknowledgedMessageAsync(RabbitMQConfiguration.GetServiceCollection(), "rabbitmq://local/{0}");
    }
}
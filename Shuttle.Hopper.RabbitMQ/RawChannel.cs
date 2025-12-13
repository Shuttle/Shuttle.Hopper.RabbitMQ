using System.Buffers;
using System.Collections.Concurrent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.RabbitMQ;

internal sealed class RawChannel : IAsyncBasicConsumer, IDisposable
{
    private readonly int _millisecondsTimeout;

    private readonly BlockingCollection<DeliveredMessage> _queue = new(new ConcurrentQueue<DeliveredMessage>());

    private readonly TransportUri _uri;
    private volatile bool _consumerAdded;
    private bool _disposed;
    private bool _disposing;

    public RawChannel(IChannel channel, TransportUri uri, RabbitMQOptions rabbitMOptions)
    {
        Guard.AgainstNull(rabbitMOptions);

        Channel = Guard.AgainstNull(channel);
        _uri = Guard.AgainstNull(uri);

        _millisecondsTimeout = (int)rabbitMOptions.QueueTimeout.TotalMilliseconds;
    }

    private bool IsOpen => Channel.IsOpen;

    public IChannel Channel { get; }

    public async Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
    {
        _consumerAdded = false;

        await Task.CompletedTask;
    }

    public async Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default)
    {
        _consumerAdded = false;

        await Task.CompletedTask;
    }

    public async Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public async Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        if (_disposed || _disposing)
        {
            return;
        }

        // body should be copied, since it will be accessed later from another thread
        var data = ArrayPool<byte>.Shared.Rent(body.Length);
        body.CopyTo(data);

        try
        {
            _queue.Add(new()
            {
                Data = data,
                DataLength = body.Length,
                BasicProperties = new(properties),
                DeliveryTag = deliveryTag
            }, cancellationToken);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(data);
        }

        await Task.CompletedTask;
    }

    public async Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
    {
        _consumerAdded = false;

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposing = true;

        try
        {
            _queue.Dispose();

            if (Channel.IsOpen)
            {
                Channel.CloseAsync().GetAwaiter().GetResult();
            }

            Channel.Dispose();

            _disposed = true;
        }
        catch
        {
            // ignored
        }
    }

    public async Task AcknowledgeAsync(DeliveredMessage deliveredMessage, CancellationToken cancellationToken)
    {
        ArrayPool<byte>.Shared.Return(deliveredMessage.Data);

        await EnsureConsumerAsync(cancellationToken);

        if (!IsOpen)
        {
            return;
        }

        await Channel.BasicAckAsync(deliveredMessage.DeliveryTag, false, cancellationToken);
    }

    private async Task EnsureConsumerAsync(CancellationToken cancellationToken)
    {
        if (_consumerAdded || !IsOpen)
        {
            return;
        }

        _consumerAdded = true;

        await Channel.BasicConsumeAsync(_uri.TransportName, false, this, cancellationToken: cancellationToken);
    }

    public async Task<DeliveredMessage?> NextAsync(CancellationToken cancellationToken)
    {
        await EnsureConsumerAsync(cancellationToken);

        try
        {
            if (_consumerAdded && !Channel.IsClosed &&
                _queue.TryTake(out var deliveredMessage, _millisecondsTimeout))
            {
                if (deliveredMessage == null)
                {
                    throw new ConnectionException(string.Format(Resources.SubscriptionNextConnectionException, _uri));
                }

                return deliveredMessage;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
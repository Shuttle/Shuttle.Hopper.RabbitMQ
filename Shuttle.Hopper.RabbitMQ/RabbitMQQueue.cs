using RabbitMQ.Client;
using Shuttle.Core.Contract;
using Shuttle.Core.Streams;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQQueue : ITransport, ICreateTransport, IDeleteTransport, IPurgeTransport, IDisposable
{
    private readonly Dictionary<string, object?> _arguments = new();

    private readonly ConnectionFactory _factory;
    private readonly HopperOptions _hopperOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly int _operationRetryCount;
    private readonly RabbitMQOptions _rabbitMQOptions;

    private RawChannel? _channel;
    private IConnection? _connection;

    private bool _disposed;

    public RabbitMQQueue(HopperOptions hopperOptions, RabbitMQOptions rabbitMQOptions, TransportUri uri)
    {
        _hopperOptions = Guard.AgainstNull(hopperOptions);
        _rabbitMQOptions = Guard.AgainstNull(rabbitMQOptions);

        Uri = Guard.AgainstNull(uri);

        if (_rabbitMQOptions.Priority != 0)
        {
            _arguments.Add("x-max-priority", _rabbitMQOptions.Priority);
        }

        _operationRetryCount = _rabbitMQOptions.OperationRetryCount;

        if (_operationRetryCount < 1)
        {
            _operationRetryCount = 3;
        }

        _factory = rabbitMQOptions.ConnectionFactory ?? new()
        {
            UserName = _rabbitMQOptions.Username,
            Password = _rabbitMQOptions.Password,
            HostName = _rabbitMQOptions.Host,
            VirtualHost = _rabbitMQOptions.VirtualHost,
            Port = _rabbitMQOptions.Port,
            RequestedHeartbeat = rabbitMQOptions.RequestedHeartbeat
        };
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[create/starting]"), cancellationToken);

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () => await QueueDeclareAsync((await GetRawChannelAsync(cancellationToken)).Channel, cancellationToken));
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[create/completed]"), cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[delete/starting]"), cancellationToken);

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () =>
            {
                await (await GetRawChannelAsync(cancellationToken)).Channel.QueueDeleteAsync(Uri.TransportName, cancellationToken: cancellationToken);
            });
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[delete/completed]"), cancellationToken);
    }

    public void Dispose()
    {
        _lock.Wait(CancellationToken.None);

        try
        {
            _channel?.Dispose();

            _disposed = true;

            CloseConnectionAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[purge/starting]"), cancellationToken);

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () =>
            {
                await (await GetRawChannelAsync(cancellationToken)).Channel.QueuePurgeAsync(Uri.TransportName, cancellationToken);
            });
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[purge/completed]"), cancellationToken);
    }

    public TransportUri Uri { get; }

    public async Task AcknowledgeAsync(object acknowledgementToken, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () => await (await GetRawChannelAsync(cancellationToken)).AcknowledgeAsync((DeliveredMessage)acknowledgementToken, cancellationToken));
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.MessageAcknowledged.InvokeAsync(new(this, acknowledgementToken), cancellationToken);
    }

    public async Task SendAsync(TransportMessage transportMessage, Stream stream, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(transportMessage);
        Guard.AgainstNull(stream);

        if (_disposed)
        {
            throw new RabbitMQQueueException(string.Format(Resources.QueueDisposed, Uri));
        }

        if (transportMessage.HasExpired())
        {
            return;
        }

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () =>
            {
                var channel = (await GetRawChannelAsync(cancellationToken)).Channel;

                var properties = new BasicProperties
                {
                    Persistent = _rabbitMQOptions.Persistent,
                    CorrelationId = transportMessage.MessageId.ToString()
                };

                if (transportMessage.HasExpiryDate())
                {
                    var milliseconds = (long)(transportMessage.ExpiresAt - DateTimeOffset.UtcNow).TotalMilliseconds;

                    if (milliseconds < 1)
                    {
                        milliseconds = 1;
                    }

                    properties.Expiration = milliseconds.ToString();
                }

                if (transportMessage.HasPriority())
                {
                    if (transportMessage.Priority > 255)
                    {
                        transportMessage.Priority = 255;
                    }

                    properties.Priority = (byte)transportMessage.Priority;
                }

                ReadOnlyMemory<byte> data;

                if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
                {
                    var length = (int)ms.Length;
                    data = new(segment.Array, segment.Offset, length);
                }
                else
                {
                    data = stream.ToBytesAsync().GetAwaiter().GetResult();
                }

                await channel.BasicPublishAsync(string.Empty, Uri.TransportName, false, properties, data, cancellationToken);
            });
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.MessageSent.InvokeAsync(new(this, transportMessage, stream), cancellationToken);
    }

    public TransportType Type => TransportType.Queue;

    public async Task<ReceivedMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        return await AccessQueueAsync(async () =>
        {
            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            ReceivedMessage? receivedMessage;

            try
            {
                var result = await (await GetRawChannelAsync(cancellationToken)).NextAsync(cancellationToken);

                receivedMessage = result == null
                    ? null
                    : new ReceivedMessage(new MemoryStream(result.Data, 0, result.DataLength, false, true), result);
            }
            finally
            {
                _lock.Release();
            }

            if (receivedMessage != null)
            {
                await _hopperOptions.MessageReceived.InvokeAsync(new(this, receivedMessage), cancellationToken);
            }

            return receivedMessage;
        });
    }

    public async ValueTask<bool> HasPendingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return false;
        }

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[has-pending/starting]"), cancellationToken);

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        return await AccessQueueAsync(async () =>
        {
            bool hasPending;

            try
            {
                var result = await (await GetRawChannelAsync(cancellationToken)).Channel.BasicGetAsync(Uri.TransportName, false, cancellationToken);

                hasPending = false;

                if (result != null)
                {
                    await (await GetRawChannelAsync(cancellationToken)).Channel.BasicRejectAsync(result.DeliveryTag, true, cancellationToken);

                    hasPending = true;
                }
            }
            finally
            {
                _lock.Release();
            }

            await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[has-pending]", hasPending), cancellationToken);

            return hasPending;
        });
    }

    public async Task ReleaseAsync(object acknowledgementToken, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await AccessQueueAsync(async () =>
            {
                var deliveredMessage = (DeliveredMessage)acknowledgementToken;

                var rawChannel = await GetRawChannelAsync(cancellationToken);

                await rawChannel.Channel.BasicPublishAsync(string.Empty, Uri.TransportName, false, deliveredMessage.BasicProperties, deliveredMessage.Data.AsMemory(0, deliveredMessage.DataLength), cancellationToken).ConfigureAwait(false);
                await rawChannel.AcknowledgeAsync(deliveredMessage, cancellationToken);
            });
        }
        finally
        {
            _lock.Release();
        }

        await _hopperOptions.MessageReleased.InvokeAsync(new(this, acknowledgementToken), cancellationToken);
    }

    private async Task AccessQueueAsync(Func<Task> action, int retry = 0)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await action.Invoke();
        }
        catch (ConnectionException)
        {
            if (retry == _operationRetryCount)
            {
                throw;
            }

            await CloseConnectionAsync();

            await AccessQueueAsync(action, retry + 1);
        }
    }

    private async Task<T?> AccessQueueAsync<T>(Func<Task<T>> action, int retry = 0)
    {
        if (_disposed)
        {
            return default;
        }

        try
        {
            return await action.Invoke();
        }
        catch (ConnectionException)
        {
            if (retry == 3)
            {
                throw;
            }

            await CloseConnectionAsync();

            return await AccessQueueAsync(action, retry + 1);
        }
    }

    private async Task CloseConnectionAsync()
    {
        if (_connection == null)
        {
            return;
        }

        if (_connection.IsOpen)
        {
            await _connection.CloseAsync(_rabbitMQOptions.ConnectionCloseTimeout);
        }

        try
        {
            _connection.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        if (_connection != null)
        {
            if (_connection.IsOpen)
            {
                return _connection;
            }

            await CloseConnectionAsync();
        }

        return await _factory.CreateConnectionAsync(Uri.TransportName, cancellationToken);
    }

    private async Task<RawChannel> GetRawChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection != null && _channel is { Channel.IsOpen: true })
        {
            return _channel;
        }

        _channel?.Dispose();

        var retry = 0;
        _connection = null;

        while (_connection == null && retry < _operationRetryCount)
        {
            try
            {
                _connection = await GetConnectionAsync(cancellationToken);
            }
            catch
            {
                retry++;
            }
        }

        if (_connection == null)
        {
            throw new ConnectionException(string.Format(Resources.ConnectionException, Uri));
        }

        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.BasicQosAsync(0, _rabbitMQOptions.PrefetchCount, false, cancellationToken);

        await QueueDeclareAsync(channel, cancellationToken);

        _channel = new(channel, Uri, _rabbitMQOptions);

        return _channel;
    }

    private async Task QueueDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[queue-declare/starting]"), cancellationToken);

        await channel.QueueDeclareAsync(Uri.TransportName, _rabbitMQOptions.Durable, false, false, _arguments, cancellationToken: cancellationToken);

        try
        {
            await channel.QueueDeclarePassiveAsync(Uri.TransportName, cancellationToken);

            await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[queue-declare/]"), cancellationToken);
        }
        catch
        {
            await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[queue-declare/failed]"), cancellationToken);
        }
    }
}

internal class DeliveredMessage
{
    public BasicProperties BasicProperties { get; set; } = null!;
    public byte[] Data { get; set; } = null!;
    public int DataLength { get; set; }
    public ulong DeliveryTag { get; set; }
}
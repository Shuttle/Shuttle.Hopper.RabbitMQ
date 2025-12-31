using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.RabbitMQ;

public static class HopperBuilderExtensions
{
    extension(HopperBuilder hopperBuilder)
    {
        public IServiceCollection UseRabbitMQ(Action<RabbitMQBuilder>? builder = null)
        {
            var services = hopperBuilder.Services;
            var rabbitMQBuilder = new RabbitMQBuilder(services);

            builder?.Invoke(rabbitMQBuilder);

            services.AddSingleton<IValidateOptions<RabbitMQOptions>, RabbitMQOptionsValidator>();

            foreach (var pair in rabbitMQBuilder.RabbitMQOptions)
            {
                services.AddOptions<RabbitMQOptions>(pair.Key).Configure(options =>
                {
                    options.ConnectionFactory = pair.Value.ConnectionFactory;
                    options.ConnectionCloseTimeout = pair.Value.ConnectionCloseTimeout;
                    options.QueueTimeout = pair.Value.QueueTimeout;
                    options.OperationRetryCount = pair.Value.OperationRetryCount;
                    options.RequestedHeartbeat = pair.Value.RequestedHeartbeat;
                    options.Priority = pair.Value.Priority;
                    options.Host = pair.Value.Host;
                    options.VirtualHost = pair.Value.VirtualHost;
                    options.Port = pair.Value.Port;
                    options.Username = pair.Value.Username;
                    options.Password = pair.Value.Password;
                    options.Persistent = pair.Value.Persistent;
                    options.PrefetchCount = pair.Value.PrefetchCount;
                    options.Durable = pair.Value.Durable;

                    if (options.PrefetchCount < 0)
                    {
                        options.PrefetchCount = 0;
                    }
                });
            }

            services.AddSingleton<ITransportFactory, RabbitMQQueueFactory>();

            return services;
        }
    }
}
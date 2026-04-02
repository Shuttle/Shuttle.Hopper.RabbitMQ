using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Shuttle.Hopper.RabbitMQ;

public static class HopperBuilderExtensions
{
    extension(HopperBuilder hopperBuilder)
    {
        public HopperBuilder UseRabbitMQ(Action<RabbitMQBuilder>? builder = null)
        {
            var services = hopperBuilder.Services;
            var rabbitMQBuilder = new RabbitMQBuilder();

            builder?.Invoke(rabbitMQBuilder);

            services.AddSingleton<IValidateOptions<RabbitMQOptions>, RabbitMQOptionsValidator>();

            foreach (var pair in rabbitMQBuilder.RabbitMQConfigureOptions)
            {
                services.AddOptions<RabbitMQOptions>(pair.Key).Configure(options =>
                {
                    pair.Value(options);

                    if (options.PrefetchCount < 0)
                    {
                        options.PrefetchCount = 0;
                    }
                });
            }

            services.AddSingleton<ITransportFactory, RabbitMQQueueFactory>();

            return hopperBuilder;
        }
    }
}
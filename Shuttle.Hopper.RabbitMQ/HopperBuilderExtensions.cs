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

            builder?.Invoke(new(services));

            services.PostConfigureAll<RabbitMQOptions>(options =>
            {
                if (options.PrefetchCount < 0)
                {
                    options.PrefetchCount = 0;
                }
            });

            services.AddSingleton<IValidateOptions<RabbitMQOptions>, RabbitMQOptionsValidator>();
            services.AddSingleton<ITransportFactory, RabbitMQQueueFactory>();

            return hopperBuilder;
        }
    }
}
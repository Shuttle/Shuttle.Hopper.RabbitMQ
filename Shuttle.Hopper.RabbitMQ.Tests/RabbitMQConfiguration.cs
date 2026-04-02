using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public static class RabbitMQConfiguration
{
    public static IServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddHopper()
            .UseRabbitMQ(builder =>
            {
                builder.Configure("local", options =>
                {
                    options.Host = "127.0.0.1";
                    options.Username = "guest";
                    options.Password = "guest";
                    options.PrefetchCount = 15;
                    options.QueueTimeout = TimeSpan.FromMilliseconds(25);
                    options.ConnectionCloseTimeout = TimeSpan.FromMilliseconds(25);
                });
            });

        return services;
    }
}
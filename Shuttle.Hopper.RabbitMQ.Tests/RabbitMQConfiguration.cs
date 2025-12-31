using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shuttle.Hopper.RabbitMQ.Tests;

public static class RabbitMQConfiguration
{
    public static IServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddHopper(hopperBuilder =>
        {
            hopperBuilder.UseRabbitMQ(builder =>
            {
                builder.AddOptions("local", new()
                {
                    Host = "127.0.0.1",
                    Username = "guest",
                    Password = "guest",
                    PrefetchCount = 15,
                    QueueTimeout = TimeSpan.FromMilliseconds(25),
                    ConnectionCloseTimeout = TimeSpan.FromMilliseconds(25)
                });
            });
        });

        return services;
    }
}
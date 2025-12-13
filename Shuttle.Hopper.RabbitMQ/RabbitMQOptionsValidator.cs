using Microsoft.Extensions.Options;

namespace Shuttle.Hopper.RabbitMQ;

public class RabbitMQOptionsValidator : IValidateOptions<RabbitMQOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMQOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidateOptionsResult.Fail(Hopper.Resources.TransportConfigurationNameException);
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return ValidateOptionsResult.Fail(string.Format(Hopper.Resources.TransportConfigurationItemException, name, nameof(options.Host)));
        }

        return ValidateOptionsResult.Success;
    }
}
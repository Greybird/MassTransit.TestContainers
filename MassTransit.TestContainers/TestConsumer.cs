using Microsoft.Extensions.Logging;

namespace MassTransit.TestContainers;

public class TestConsumer(ILogger<TestConsumer> logger) : IConsumer<TestMessage>
{
    public Task Consume(ConsumeContext<TestMessage> context)
    {
        logger.LogInformation("Received message: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}
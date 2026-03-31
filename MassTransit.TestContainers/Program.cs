using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MassTransit.TestContainers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);

Console.WriteLine("Start emulator");

var serviceBusContainer = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.0")
    .WithAcceptLicenseAgreement(true)
    .WithConfig("servicebus.json")
    .Build();
await serviceBusContainer.StartAsync();

Console.WriteLine($"Service Bus connection string: {serviceBusContainer.GetConnectionString()}");
Console.WriteLine($"Service Bus connection string: {serviceBusContainer.GetHttpConnectionString()}");


var connectionString = serviceBusContainer.GetConnectionString();
var httpConnectionString = serviceBusContainer.GetHttpConnectionString();

var admin = new ServiceBusAdministrationClient(httpConnectionString);
var queue = await admin.CreateQueueAsync("test-queue");
var topic = await admin.CreateTopicAsync("test-topic");
var subscription = await admin.CreateSubscriptionAsync(new CreateSubscriptionOptions(topic.Value.Name, "test-subscription")
{
    ForwardTo = queue.Value.Name
});


builder.Services.AddMassTransit(c =>
{
    c.AddConsumer<TestConsumer>();
    c.UsingAzureServiceBus((_, cc) =>
    {
        cc.Host(connectionString);
    });
});

Console.WriteLine("Start host");
var host = builder.Build();
await host.StartAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

await TopologyPrinter.PrintTopologyAsync(loggerFactory, httpConnectionString);

logger.LogInformation("Publish test message");
var bus = host.Services.GetRequiredService<IBus>();
await bus.Publish(new TestMessage
{
    Text = "Hello!"
});

logger.LogInformation("Waiting...");
await Task.Delay(TimeSpan.FromSeconds(10));

await TopologyPrinter.PrintTopologyAsync(loggerFactory, httpConnectionString);

Console.ReadKey(intercept: true);

await host.StopAsync();
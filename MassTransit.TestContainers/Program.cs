using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MassTransit.AzureServiceBusTransport;
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

var connectionString = serviceBusContainer.GetConnectionString();
var httpConnectionString = serviceBusContainer.GetHttpConnectionString();

Console.WriteLine($"Service Bus connection string: {connectionString}");
Console.WriteLine($"Service Bus HTTP connection string: {httpConnectionString}");

var properties = ServiceBusConnectionStringProperties.Parse(httpConnectionString);
Environment.SetEnvironmentVariable("EMULATOR_HTTP_PORT", properties.Endpoint.Port.ToString());
Console.WriteLine($"EMULATOR_HTTP_PORT: {Environment.GetEnvironmentVariable("EMULATOR_HTTP_PORT")}");

httpConnectionString = httpConnectionString.Replace("127.0.0.1", "localhost");
connectionString = connectionString.Replace("127.0.0.1", "localhost").Replace("amqp", "sb");
Console.WriteLine($"Updated Service Bus connection string: {connectionString}");
Console.WriteLine($"Updated Service Bus HTTP connection string: {httpConnectionString}");

Defaults.DefaultMessageTimeToLive = TimeSpan.FromHours(1);
Defaults.BasicMessageTimeToLive = TimeSpan.FromHours(1);
Defaults.AutoDeleteOnIdle = TimeSpan.FromHours(1);

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
    c.UsingAzureServiceBus((context, cc) =>
    {
        cc.Host(connectionString);
        cc.ConfigureEndpoints(context);
    });
});

Console.WriteLine("Start host");
var host = builder.Build();
await host.StartAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

//await TopologyPrinter.PrintTopologyAsync(loggerFactory, httpConnectionString);

logger.LogInformation("Starting the bus");
var busControl = host.Services.GetRequiredService<IBusControl>();
await busControl.StartAsync(TimeSpan.FromSeconds(20));

logger.LogInformation("Publish test message");
var publishEndpoint = host.Services.GetRequiredService<IPublishEndpoint>();
await publishEndpoint.Publish(new TestMessage
{
    Text = "Hello!"
});


logger.LogInformation("Waiting...");
await Task.Delay(TimeSpan.FromSeconds(10));

//await TopologyPrinter.PrintTopologyAsync(loggerFactory, httpConnectionString);

Console.ReadKey(intercept: true);

await host.StopAsync();
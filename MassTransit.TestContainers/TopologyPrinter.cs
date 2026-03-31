using System.Text;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace MassTransit.TestContainers;

public class TopologyPrinter
{
    public static async Task PrintTopologyAsync(ILoggerFactory loggerFactory, string connectionString)
    {
        var logger = loggerFactory.CreateLogger<TopologyPrinter>();
        var admin = new ServiceBusAdministrationClient(connectionString);

        var queues = new List<string>();
        logger.LogTrace("Get queues");
        await foreach (var q in admin.GetQueuesAsync())
        {
            logger.LogTrace("  Found queue: {Name}", q.Name);
            queues.Add(q.Name);
        }


        var topics = new List<(string Name, List<string> Subscriptions)>();
        logger.LogTrace("Get topics");
        await foreach (var t in admin.GetTopicsAsync())
        {
            var subs = new List<string>();
            logger.LogTrace("  Get subscriptions for {Name}", t.Name);
            await foreach (var s in admin.GetSubscriptionsAsync(t.Name))
            {
                logger.LogTrace("    Found subscription: {Name}", s.SubscriptionName);
                subs.Add(s.SubscriptionName);
            }
            topics.Add((t.Name, subs));
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Service Bus Topology");
        sb.AppendLine("├── Queues");
        for (int i = 0; i < queues.Count; i++)
            sb.AppendLine($"│   {(i < queues.Count - 1 ? "├──" : "└──")} {queues[i]}");
        sb.AppendLine("└── Topics");
        for (int i = 0; i < topics.Count; i++)
        {
            bool isLastTopic = i == topics.Count - 1;
            sb.AppendLine($"    {(isLastTopic ? "└──" : "├──")} {topics[i].Name}");
            var subs = topics[i].Subscriptions;
            for (int j = 0; j < subs.Count; j++)
                sb.AppendLine(
                    $"    {(isLastTopic ? " " : "│")}   {(j < subs.Count - 1 ? "├──" : "└──")} {subs[j]}");
        }

        logger.LogInformation("{Topology}", sb.ToString());
    }
}
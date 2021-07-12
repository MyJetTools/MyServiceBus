using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Metrics;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains
{
    public static class ServicesBinder
    {
        public static void RegisterMyNoServiceBusDomainServices(this IServiceCollection sc)
        {
            
            sc.AddSingleton<TopicsList>();
            sc.AddSingleton<GlobalVariables>();
            
            sc.AddSingleton<MyServiceBusPublisherOperations>();
            sc.AddSingleton<MyServiceBusSubscriberOperations>();
            sc.AddSingleton<MyServiceBusBackgroundExecutor>();
            
            sc.AddSingleton<TopicsAndQueuesPersistenceProcessor>();
            
            sc.AddSingleton<MessageContentPersistentProcessor>();
            
            sc.AddSingleton<MyServiceBusDeliveryHandler>();

            sc.AddSingleton<Log>();

            sc.AddSingleton<MessagesPerSecondByTopic>();
            sc.AddSingleton<MessagesPageLoader>();

            sc.AddSingleton<SessionsList>();

            sc.AddSingleton<QueuesGc>();
        }
        
        
    }
}
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Tests.GrpcMocks;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Tests.Utils
{
    
    public static class TestIoc 
    {
        public static IServiceProvider CreateForTests()
        {
            var ioc = new ServiceCollection();

            ioc.AddSingleton<IMyServiceBusMessagesPersistenceGrpcService>(new MyServiceBusMessagesPersistenceGrpcServiceMock());
            
            ioc.AddSingleton<IMyServiceBusQueuePersistenceGrpcService>(new MyServiceBusQueuePersistenceGrpcServiceMock());

            ioc.AddSingleton<IMessagesToPersistQueue>(new MessagesToPersistQueueForTests());
            
            ioc.RegisterMyNoServiceBusDomainServices();

            var testSettings = new TestSettings(); 
            ioc.AddSingleton(testSettings);
            ioc.AddSingleton<IMyServiceBusSettings>(testSettings);
            
            ioc.AddSingleton<IMetricCollector, MetricsCollectorMock>();
            
            return ioc.BuildServiceProvider();
        }

    }



    public static class MyIocHelpers
    {

        
        public static MockConnection ConnectSession(this IServiceProvider ioc, string sessionName, DateTime? dateTime = null)
        {
            dateTime ??= DateTime.UtcNow;
            return new MockConnection(ioc, sessionName, dateTime.Value);
        }

        public static void SetCurrentTime(this IServiceProvider ioc, DateTime now)
        {
            var backgroundExecutor = ioc.GetRequiredService<MyServiceBusBackgroundExecutor>();
            backgroundExecutor.GcOrWarmupMessagesAndPushDelivery().AsTask().Wait();
            backgroundExecutor.PersistTopicsAndQueuesSnapshotAsync().AsTask().Wait();
            backgroundExecutor.PersistMessageContentAsync().AsTask().Wait();

        }

        public static int GetMessagesCount(this IServiceProvider ioc, string topicId, string queueId)
        {
            var topic = ioc.GetRequiredService<TopicsList>().Get(topicId);
            return (int)topic.GetQueueMessagesCount(queueId);
        }
        
        public static MessagesPage GetMessagesFromPersistentStorage(this IServiceProvider ioc, string topicId)
        {
            var persistentStorage = ioc.GetRequiredService<IMyServiceBusMessagesPersistenceGrpcService>();
            var messagesPageId = new MessagesPageId(0);
            return persistentStorage.GetPageAsync(topicId, messagesPageId.Value).ToPageInMemoryAsync(messagesPageId).Result;
        }
        
        public static IQueueSnapshot GetMessageSnapshotsFromPersistentStorage(this IServiceProvider ioc, string topicId, string queueName)
        {
            var persistentStorage = ioc.GetRequiredService<IMyServiceBusQueuePersistenceGrpcService>();
            var snapshot = persistentStorage.GetTopicsAndQueuesSnapshotAsync().Result.ToDictionary(itm => itm.TopicId);
            return snapshot[topicId].QueueSnapshots.First(itm => itm.QueueId == queueName);
        }


    }
    
}
using System;
using System.Linq;
using MyDependencies;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Persistence.AzureStorage;
using MyServiceBus.Persistence.AzureStorage.PageBlob;
using MyServiceBus.Persistence.AzureStorage.TopicMessages;

namespace MyServiceBus.Domains.Tests.Utils
{
    
    public static class TestIoc 
    {
        public static MyIoc CreateForTests()
        {
            var ioc = new MyIoc();

            ioc.Register<IMessagesPersistentStorage>(new MessagesPersistentStorage((topic, pageId)=>new PageBlobInMem()));
            
            ioc.Register<ITopicPersistenceStorage>(new TopicsPersistentStorageAzureBlobs(new PageBlobInMem()));

            ioc.Register<IMessagesToPersistQueue>(new MessagesToPersistQueueForTests());
            
            ioc.RegisterMyNoServiceBusDomainServices(ServerMode.Server);

            var testSettings = new TestSettings(); 
            ioc.Register(testSettings);
            ioc.Register<IMyServiceBusSettings>(testSettings);
            
            ioc.Register<IMetricCollector, MetricsCollectorMock>();
            
            return ioc;
        }

        public static MyIoc WithSessionTimeOut(this MyIoc ioc, TimeSpan sessionTimeOut)
        {
            var settings = (TestSettings)ioc.GetService<IMyServiceBusSettings>();
            settings.SessionTimeOut = sessionTimeOut;
            return ioc;
        }

        public static MyIoc WithEventuallyPersistenceTimeOut(this MyIoc ioc, TimeSpan eventuallyPersistenceTimeOut)
        {
            var settings = (TestSettings)ioc.GetService<IMyServiceBusSettings>();
            settings.EventuallyPersistenceDelay = eventuallyPersistenceTimeOut;
            return ioc;
        }
    }



    public static class MyIocHelpers
    {

        
        public static MockConnection ConnectSession(this MyIoc ioc, string sessionName, DateTime? dateTime = null)
        {
            if (dateTime == null)
                dateTime = DateTime.UtcNow;

            return new MockConnection(ioc, sessionName, dateTime.Value);
        }

        public static void SetCurrentTime(this MyIoc ioc, DateTime now)
        {
            var backgroundExecutor = ioc.GetService<IMyServerBusBackgroundExecutor>();
            backgroundExecutor.GarbageCollect().AsTask().Wait();
            backgroundExecutor.PersistAsync().AsTask().Wait();
        }

        public static int GetMessagesCount(this MyIoc ioc, string topicId, string queueId)
        {
            var topic = ioc.GetService<TopicsList>().Get(topicId);
            return (int)topic.GetQueueMessagesCount(queueId);
        }
        
        public static int GetLeasedMessagesCount(this MyIoc ioc, string topicId, string queueId)
        {
            var topic = ioc.GetService<TopicsList>().Get(topicId);
            var queue = topic.GetQueue(queueId);
            return (int)queue.GetLeasedMessagesCount();
        }


        public static MessagesPageInMemory GetMessagesFromPersistentStorage(this MyIoc ioc, string topicId)
        {
            var persistentStorage = ioc.GetService<IMessagesPersistentStorage>();
            return persistentStorage.GetMessagesPageAsync(topicId, new MessagesPageId(0)).Result;
        }
        
        public static IQueueSnapshot GetMessageSnapshotsFromPersistentStorage(this MyIoc ioc, string topicId, string queueName)
        {
            var persistentStorage = ioc.GetService<ITopicPersistenceStorage>();
            var snapshot = persistentStorage.GetSnapshotAsync().Result.ToDictionary(itm => itm.TopicId);
            return snapshot[topicId].QueueSnapshots.First(itm => itm.QueueId == queueName);
        }


    }
    
}
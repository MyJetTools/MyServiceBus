using System;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    public class TestDeleteQueueOnDisconnect
    {

        [Test]
        public async Task TestWeHaveZeroSubscribersAfterDisconnect()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            
            var session = ioc.ConnectSession("MySession", nowTime);
            var topic = session.CreateTopic(topicName);
            await session.SubscribeAsync(topicName, queueName);

            var queues = topic.Queues.GetAll();
            Assert.AreEqual(1, queues.Count);

            var now = DateTime.Parse("2021-01-01T00:00:00");
            session.Disconnect(now);


            var queue = topic.Queues.GetQueue(queueName);
      
            Assert.AreEqual(0, queue.GetRwAccess(rw => rw.SubscribersList.GetCount()));
        }
        
        
        [Test]
        public async Task TestDeleteOnDisconnect()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            
            var session = ioc.ConnectSession("MySession", nowTime);
            var topic = session.CreateTopic(topicName);
            await session.SubscribeAsync(topicName, queueName);

            var queues = topic.Queues.GetAll();
            Assert.AreEqual(1, queues.Count);
            
            session.Disconnect(DateTime.UtcNow);
            
            queues = topic.Queues.GetAll();
            Assert.AreEqual(0, queues.Count);
            
        }
        
        [Test]
        public async Task TestNotDeleteOnDisconnect()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            
            var session = ioc.ConnectSession("MySession", nowTime);
            var topic = session.CreateTopic(topicName);
            await session.SubscribeAsync(topicName, queueName, TopicQueueType.Permanent);

            var queues = topic.Queues.GetAll();
            Assert.AreEqual(1, queues.Count);
            
            session.Disconnect(DateTime.UtcNow);
            
            queues = topic.Queues.GetAll();
            Assert.AreEqual(1, queues.Count);
            
        }
    }
}
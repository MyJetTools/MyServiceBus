using System;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    
    public class TestDisconnects
    {

        [Test]
        public async Task TestPublishSubscribeDisconnectWithoutConfirmationAndMessagesGoesBackToQueue()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            
            var session = ioc.ConnectSession("MySession", nowTime);
            session.CreateTopic(topicName);
            var subscriber = await session.SubscribeAsync(topicName, queueName, TopicQueueType.Permanent);
            
            session.PublishMessage(topicName, new byte[] {1, 2, 3}, nowTime);

            Assert.AreEqual(0, ioc.GetMessagesCount(topicName, queueName));
            Assert.AreEqual(1,  subscriber.MessagesOnDelivery.Count);
            
            var lastDelivered = session.GetLastSentMessage();

            Assert.AreEqual(queueName, lastDelivered.topicQueue.QueueId); 
            
            session.Disconnect(DateTime.UtcNow);
            
            Assert.AreEqual(1, ioc.GetMessagesCount(topicName, queueName));
            
            var session2 = ioc.ConnectSession("MySession2", nowTime);
            var subscriber2 = await session2.SubscribeAsync(topicName, queueName);
            Assert.AreEqual(0, ioc.GetMessagesCount(topicName, queueName));
            Assert.AreEqual(1, subscriber2.MessagesOnDelivery.Count);
        }


        [Test]
        public async Task TestDisconnectImmediateConnectAndSeveralPublishes()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";
            
            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            
            var session = ioc.ConnectSession("MySession", nowTime);
           
            session.CreateTopic(topicName);
            var queue = session.SubscribeAsync(topicName, queueName);
            
            session.PublishMessage(topicName, new byte[] {1}, nowTime);
            session.PublishMessage(topicName, new byte[] {2}, nowTime);
            
            Console.WriteLine("First Publish:          "+queue);
        //    Assert.AreEqual(0, queue.GetMessagesCount());
        //    Assert.AreEqual(1, queue.GetLeasedMessagesCount());

            var lastMessage = session.GetLastSentMessage();
            
            var session2 = ioc.ConnectSession("MySession2", nowTime);
            await session2.SubscribeAsync(topicName, queueName);
            Console.WriteLine("Subscribe to Sess2:     "+queue);
            
            session2.PublishMessage(topicName, new byte[] {2}, nowTime);
            Console.WriteLine("First Publish to Sess2: "+queue);
         //   Assert.AreEqual(0, queue.GetMessagesCount());
         //   Assert.AreEqual(2, queue.GetLeasedMessagesCount());
            
            var lastMessageSession2 = session2.GetLastSentMessage();
            await session2.ConfirmDeliveryAsync(lastMessageSession2.topicQueue, lastMessageSession2.confirmationId);
            Console.WriteLine("Confirm delivery:       "+queue);
 
            session2.PublishMessage(topicName, new byte[] {3}, nowTime);
            session2.PublishMessage(topicName, new byte[] {4}, nowTime);
            session2.PublishMessage(topicName, new byte[] {5}, nowTime);
            Console.WriteLine("Double Publish:         "+queue);
            
            session.Disconnect(DateTime.UtcNow);
            
            Console.WriteLine("After Disconnect:       "+queue);
            
            
            lastMessageSession2 = session2.GetLastSentMessage();
            await session2.ConfirmDeliveryAsync(lastMessageSession2.topicQueue, lastMessageSession2.confirmationId);
            Console.WriteLine("6:                      "+queue);

            
            session2.Disconnect(DateTime.UtcNow);
            
            Console.WriteLine("7:                      "+queue);
        }
        
    }
    
}
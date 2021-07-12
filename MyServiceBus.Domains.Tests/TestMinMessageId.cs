using System;
using System.Threading.Tasks;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    public class TestMinMessageId
    {

        [Test]
        public async Task TestMinMessageIdCleaning()
        {

            var ioc = TestIoc.CreateForTests();
            
            const string topicName = "testtopic";
            const string queueName = "testqueue";

            
            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            var session = ioc.ConnectSession("MySession", nowTime);
            
            session.CreateTopic(topicName);
            var subscriber = await session.SubscribeAsync(topicName, queueName);


            
            session.PublishMessage(topicName, new byte[] {0}, nowTime);
            Assert.AreEqual(0, subscriber.TopicQueue.GetMessagesCount());
            Assert.AreEqual(1, subscriber.MessagesOnDelivery.Count);
            var firstSent = session.GetLastSentMessage();
            
            session.PublishMessage(topicName, new byte[] {1}, nowTime);
            
            Assert.AreEqual(1, subscriber.TopicQueue.GetMessagesCount());
            Assert.AreEqual(1, subscriber.MessagesOnDelivery.Count);

            await session.ConfirmDeliveryAsync(firstSent.topicQueue, firstSent.confirmationId);
            
            Assert.AreEqual(0, subscriber.TopicQueue.GetMessagesCount());
            Assert.AreEqual(1, subscriber.MessagesOnDelivery.Count);
            
       
        }
   
    }
}
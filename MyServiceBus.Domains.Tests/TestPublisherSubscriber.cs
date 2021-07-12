using System;
using System.Threading.Tasks;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    public class TestPublisherSubscriber
    {
        
        
        [Test]
        public async Task TestFirstSubscribeNextPublish()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var session = ioc.ConnectSession("MySession");
            session.CreateTopic(topicName);

            await session.SubscribeAsync(topicName, queueName);

            var message = new byte[] {1, 2, 3};

            session.PublishMessage(topicName, message, DateTime.UtcNow);

            Assert.AreEqual(1, session.GetSentPackagesCount());

            var lastMessage = session.GetLastSentMessage();
            
            Assert.AreEqual(message.Length, lastMessage.messages[0].message.Data.Length);
        }
        
        [Test]
        public void TestIfWeClearDeliveredMessages()
        {
            /*
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var session = ioc.ConnectSession("MySession");
            
            session.CreateTopic(topicName);

            session.Subscribe(topicName, queueName);
            

            var message = new byte[] {1, 2, 3};

            var nowTime = DateTime.Parse("2019-01-01");

            session.PublishMessage(topicName, message, nowTime, true);
            
            var messagesCountInStorage = ioc.GetMessagesFromPersistentStorage(topicName);
             Assert.AreEqual(1, messagesCountInStorage.Count);

            //var response = readingTask.Result;
            
           // var readingTask2 = ioc.GetMessagesAsync(session, topicName, queueName, response.ConfirmationId);
            
            var message2 = new byte[] {4, 5, 6};
            
            session.PublishMessage(topicName, message2, nowTime, true);
            
            ioc.SetCurrentTime(nowTime);
            
            var messagesCountInCache = ioc.GetCachedMessagesCount(topicName);
            
            Assert.AreEqual(2, messagesCountInCache);

            messagesCountInStorage = ioc.GetMessagesFromPersistentStorage(topicName);
            Assert.AreEqual(2, messagesCountInStorage.Count);
            */

        }

    }
}
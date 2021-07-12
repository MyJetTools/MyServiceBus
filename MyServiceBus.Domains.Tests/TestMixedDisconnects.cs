using System;
using System.Threading.Tasks;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    public class TestMixedDisconnects
    {


        [Test]
        public async Task TestThreePublishesFirstAndThirdDisconnect()
        {
            var ioc = TestIoc.CreateForTests();

            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            var session1 = ioc.ConnectSession("MySession1", nowTime);
            session1.CreateTopic(topicName);
            await session1.SubscribeAsync(topicName, queueName);

            session1.PublishMessage(topicName, new byte[] {1}, nowTime);
            
            Assert.AreEqual(1, session1.GetSentPackagesCount());

            var session2 = ioc.ConnectSession("MySession2", nowTime);
            await session2.SubscribeAsync(topicName, queueName);
            
            Assert.AreEqual(0, session2.GetSentPackagesCount());
            
            session1.Disconnect(DateTime.UtcNow);
            
            Assert.AreEqual(1, session2.GetSentPackagesCount());
            
            var session3 = ioc.ConnectSession("MySession3", nowTime);
            await session3.SubscribeAsync(topicName, queueName);
            Assert.AreEqual(0, session3.GetSentPackagesCount());

        }

    }
}
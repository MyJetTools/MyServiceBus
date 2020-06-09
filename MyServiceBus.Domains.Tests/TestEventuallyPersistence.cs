using System;
using DotNetCoreDecorators;
using MyServiceBus.Domains.Tests.Utils;
using NUnit.Framework;

namespace MyServiceBus.Domains.Tests
{
    public class TestEventuallyPersistence
    {

        [Test]
        public void TestPersistence()
        {
            var ioc = TestIoc.CreateForTests().WithEventuallyPersistenceTimeOut(TimeSpan.FromSeconds(5));
            
            const string topicName = "testtopic";
            const string queueName = "testqueue";

            var nowTime = DateTime.Parse("2019-01-01T00:00:00");
            var session = ioc.ConnectSession("MySession", nowTime);
            
            session.CreateTopic(topicName);
            session.Subscribe(topicName, queueName, false);
            
            session.PublishMessage(topicName, new byte[] {0}, nowTime);
            session.PublishMessage(topicName, new byte[] {1}, nowTime);

            nowTime = nowTime.AddSeconds(4);
            ioc.SetCurrentTime(nowTime);
            
            var savedMessages = ioc.GetMessagesFromPersistentStorage(topicName);
            var snapshot = ioc.GetMessageSnapshotsFromPersistentStorage(topicName, queueName);
            
            Assert.AreEqual(2,savedMessages.Count);

            
            session.PublishMessage(topicName, new byte[] {2}, nowTime);
            session.PublishMessage(topicName, new byte[] {3}, nowTime);
            
            nowTime = nowTime.AddSeconds(2);
            ioc.SetCurrentTime(nowTime);
            
            savedMessages = ioc.GetMessagesFromPersistentStorage(topicName);
            snapshot = ioc.GetMessageSnapshotsFromPersistentStorage(topicName, queueName);

            Assert.AreEqual(4,savedMessages.Count);

            
            Assert.AreEqual(1,snapshot.Ranges.AsReadOnlyList()[0].FromId);
            Assert.AreEqual(3,snapshot.Ranges.AsReadOnlyList()[0].ToId);
        }
        
        
        
        [Test]
        public void TestWhenWeDeliverHalfAndNotDeliverHalf()
        {
            var ioc = TestIoc.CreateForTests().WithEventuallyPersistenceTimeOut(TimeSpan.FromSeconds(5));
            
            const string topicName = "testtopic";
            const string queueName = "testqueue";


            var nowTime = DateTime.Parse("2019-01-01T00:00:00");

            var session = ioc.ConnectSession("MySession");
            
            session.CreateTopic(topicName);
            session.Subscribe(topicName, queueName,false);


            session.PublishMessage(topicName, new byte[] {0}, nowTime);
            session.PublishMessage(topicName, new byte[] {1}, nowTime);
            
            Assert.AreEqual(1, session.GetSentPackagesCount());

            nowTime = nowTime.AddSeconds(4);
            ioc.SetCurrentTime(nowTime);
            
            var savedMessages = ioc.GetMessagesFromPersistentStorage(topicName);
            var snapshot = ioc.GetMessageSnapshotsFromPersistentStorage(topicName, queueName);
            
            Assert.AreEqual(2,savedMessages.Count);

            
            session.PublishMessage(topicName, new byte[] {2}, nowTime);
            session.PublishMessage(topicName, new byte[] {3}, nowTime);

            session.Disconnect();

            nowTime = nowTime.AddSeconds(5);
            ioc.SetCurrentTime(nowTime);
            
            
            savedMessages = ioc.GetMessagesFromPersistentStorage(topicName);
            snapshot = ioc.GetMessageSnapshotsFromPersistentStorage(topicName, queueName);

            Assert.AreEqual(4,savedMessages.Count);


            var ranges = snapshot.Ranges.AsReadOnlyList();
            
            Assert.AreEqual(0,ranges[0].FromId);
            Assert.AreEqual(3,ranges[0].ToId);
        }
        
        
    }
}
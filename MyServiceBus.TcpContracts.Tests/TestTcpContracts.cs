using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyTcpSockets.Extensions;
using NUnit.Framework;

namespace MyServiceBus.TcpContracts.Tests
{
    public class TestTcpContracts
    {

        private static void TestValues(object t1, object t2)
        {
            var ps1 = t1.GetType().GetProperties().ToDictionary(itm => itm.Name);
            var ps2 = t2.GetType().GetProperties().ToDictionary(itm => itm.Name);

            foreach (var pi1 in ps1)
            {
                var v1 = pi1.Value.GetValue(t1);
                var v2 = ps2[pi1.Key].GetValue(t2);

                Assert.AreEqual(v1, v2);
            }
        }

        [Test]
        public async Task TestPing()
        {

            var serializer = new MyServiceBusTcpSerializer();

            var pingContract = new PingContract();

            var rawData = serializer.Serialize(pingContract);
            
            var dataReader = new TcpDataReader(2048);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();

            var result
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(typeof(PingContract) == result.GetType());
        }

        
        [Test]
        public async Task TestPong()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new PongContract();

            var rawData = serializer.Serialize(inContract);

            var dataReader = new TcpDataReader(2048);
            await dataReader.NewPackageAsync(rawData);
            
            var ct = new CancellationTokenSource();
            
            var result
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(inContract.GetType() == result.GetType());   
        }

        [Test]
        public async Task TestGreeting()
        {

            var serializer = new MyServiceBusTcpSerializer();

            var inContract = new GreetingContract
            {
                Name = "MyName"
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);
            
            var ct = new CancellationTokenSource();
            
            var result
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(inContract.GetType() == result.GetType());
            TestValues(inContract, result);
        }

        [Test]
        public async Task TestPublishContract()
        {
            var serializer = new MyServiceBusTcpSerializer();

            var inContract = PublishContract
                .Create(
                    "MyName", 
                    5, true, 
                    new[]
                    {
                        new byte[] {1, 2, 3}, 
                        new byte[] {4, 5, 6}, 
                        new byte[] {7, 8, 9}
                    });

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();
            
            var res 
                = await serializer.DeserializeAsync(dataReader, ct.Token);
           
           
           var result = (PublishContract) res;
           Assert.AreEqual(inContract.TopicId, result.TopicId);
           Assert.AreEqual(inContract.RequestId, result.RequestId);
           Assert.AreEqual(inContract.ImmediatePersist, result.ImmediatePersist);
           var d1 = inContract.Data.ToArray();
           var d2 = result.Data.ToArray();

           Assert.AreEqual(d1.Length, d2.Length);
           Assert.AreEqual(d1[0].payload.ToArray()[0], d2[0].payload.ToArray()[0]);
           Assert.AreEqual(d1[1].payload.ToArray()[0], d2[1].payload.ToArray()[0]);
           Assert.AreEqual(d1[2].payload.ToArray()[0], d2[2].payload.ToArray()[0]); 
        }
        
        [Test]
        public async Task TestPublishContractWithMetaData()
        {
            var serializer = new MyServiceBusTcpSerializer();

            var packetVersion = new PacketVersionsContract();
            packetVersion.SetPacketVersion(CommandType.Publish, 1);

            serializer.HandlePacketVersions(packetVersion);

            var inContract = PublishContract
                .Create(
                    "MyName", 
                    5, true, 
                    new[]
                    {
                        (new byte[] {1, 2, 3}, (IReadOnlyDictionary<string, string>)
                            new Dictionary<string, string> {["Key1"] = "Value1"}), 
                        (new byte[] {4, 5, 6}, new Dictionary<string, string> {["Key2"] = "Value2"}), 
                        (new byte[] {7, 8, 9}, new Dictionary<string, string> {["Key3"] = "Value3"})
                    });

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();
            
            var res 
                = await serializer.DeserializeAsync(dataReader, ct.Token);
           
            var result = (PublishContract) res;
            Assert.AreEqual(inContract.TopicId, result.TopicId);
            Assert.AreEqual(inContract.RequestId, result.RequestId);
            Assert.AreEqual(inContract.ImmediatePersist, result.ImmediatePersist);
            var d1 = inContract.Data.ToArray();
            var d2 = result.Data.ToArray();

            Assert.AreEqual(d1.Length, d2.Length);
            Assert.AreEqual(d1[0].payload.ToArray()[0], d2[0].payload.ToArray()[0]);
            Assert.AreEqual(d1[1].payload.ToArray()[0], d2[1].payload.ToArray()[0]);
            Assert.AreEqual(d1[2].payload.ToArray()[0], d2[2].payload.ToArray()[0]); 
            
            
            Assert.AreEqual(d1[0].metaData.ToArray()[0].Key, d2[0].metaData.ToArray()[0].Key);
            Assert.AreEqual(d1[0].metaData.ToArray()[0].Value, d2[0].metaData.ToArray()[0].Value);
        }

        [Test]
        public async Task TestPublishResponseContract()
        {

            var serializer = new MyServiceBusTcpSerializer();

            var inContract = new PublishResponseContract
            {
                RequestId = 66
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);
            
            var ct = new CancellationTokenSource();
            
            var result
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(inContract.GetType() == result.GetType());
            TestValues(inContract, result);

        }

        [Test]
        public async Task TestSubscribeContract()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new SubscribeContract
            {
                TopicId = "aaa",
                QueueId = "bbb",
                QueueType = TopicQueueType.PermanentWithSingleConnection
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();
            
            var result 
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(inContract.GetType() == result.GetType());
            TestValues(inContract, result);
        }

        [Test]
        public async Task TestSubscribeResponseContract()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new SubscribeResponseContract
            {
                TopicId = "aaa",
                QueueId = "bbb"

            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();
            
            var result 
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            Assert.IsTrue(inContract.GetType() == result.GetType());
            TestValues(inContract, result);

        }


        [Test]
        public async Task TestNewMessageContract()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new NewMessagesContract
            {
                TopicId = "aaa",
                QueueId = "bbb",
                Data = new[]
                {
                    new NewMessagesContract.NewMessageData
                    {
                        Id = 5,
                        Data = new byte[] {1, 2, 3}
                    },
                    new NewMessagesContract.NewMessageData
                    {
                        Id = 6,
                        Data = new byte[] {4, 5, 6}
                    },

                }
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);

            var ct = new CancellationTokenSource();
            
            var res = await serializer
                .DeserializeAsync(dataReader, ct.Token);
            
            var result = (NewMessagesContract) res;
            Assert.AreEqual(inContract.TopicId, result.TopicId);
            Assert.AreEqual(inContract.QueueId, result.QueueId);
            var d1 = inContract.Data.ToArray();
            var d2 = result.Data.ToArray();

            Assert.AreEqual(d1.Length, d2.Length);
            Assert.AreEqual(d1[0].Id, d2[0].Id);
            Assert.AreEqual(d1[1].Id, d2[1].Id);

            Assert.AreEqual(d1[0].Data.ToArray()[0], d2[0].Data.ToArray()[0]);
            Assert.AreEqual(d1[1].Data.ToArray()[0], d2[1].Data.ToArray()[0]);

        }

        [Test]
        public async Task TestNewMessageConfirmationContract()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new NewMessageConfirmationContract
            {
                TopicId = "234",
                QueueId = "ggg",
                ConfirmationId = 555
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);       

            var ct = new CancellationTokenSource();
            
            var res
                = await serializer.DeserializeAsync(dataReader, ct.Token);

             var result = (NewMessageConfirmationContract) res;
             Assert.AreEqual(inContract.TopicId, result.TopicId);
             Assert.AreEqual(inContract.QueueId, result.QueueId);
             Assert.AreEqual(inContract.ConfirmationId, result.ConfirmationId);

        }

        [Test]
        public async Task TestCreateTopicIfNotExists()
        {

            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new CreateTopicIfNotExistsContract
            {
                TopicId = "234",
                MaxMessagesInCache = 243432
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);            

            var ct = new CancellationTokenSource();
            
            var res
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            var result = (CreateTopicIfNotExistsContract) res;
            Assert.AreEqual(inContract.TopicId, result.TopicId);
            Assert.AreEqual(inContract.MaxMessagesInCache, result.MaxMessagesInCache);
        }

        [Test]
        public async Task SendConfirmationsPacket()
        {
            var serializer = new MyServiceBusTcpSerializer();


            var inContract = new MessagesConfirmationContract
            {
                TopicId = "234",
                QueueId = "567",
                Ok = new []{new MessagesInterval{FromId = 5, ToId = 7}},
                NotOk = new []{new MessagesInterval{FromId = 10, ToId = 10}, new MessagesInterval{FromId = 15, ToId = 16} },
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);          
            
            var ct = new CancellationTokenSource();
            
            var res
                = await serializer
                    .DeserializeAsync(dataReader, ct.Token);

            var result = (MessagesConfirmationContract) res;
            Assert.AreEqual(inContract.TopicId, result.TopicId);
            Assert.AreEqual(inContract.QueueId, result.QueueId); 
            Assert.AreEqual(1, result.Ok.Count);
            Assert.AreEqual(2, result.NotOk.Count);
            Assert.AreEqual(10, result.NotOk[0].FromId);
            Assert.AreEqual(10, result.NotOk[0].ToId);
            
            Assert.AreEqual(15, result.NotOk[1].FromId);
            Assert.AreEqual(16, result.NotOk[1].ToId);
            
        }
        
        
        [Test]
        public async Task TestPacketsVersionsContract()
        {
            var serializer = new MyServiceBusTcpSerializer();
            var deserializer = new MyServiceBusTcpSerializer();

            var inContract = new PacketVersionsContract();
            inContract.SetPacketVersion(CommandType.Publish, 1);
            inContract.SetPacketVersion(CommandType.NewMessage, 2);

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);       

            var ct = new CancellationTokenSource();
            
            var res
                = await deserializer.DeserializeAsync(dataReader, ct.Token);

            var result = (PacketVersionsContract) res;
            var inPackets = inContract.GetPackets().ToDictionary(itm => itm.Key);
            var resultPackets = result.GetPackets().ToDictionary(itm => itm.Key);
            
            Assert.AreEqual(inPackets.Count, resultPackets.Count);

            foreach (var kvp in inPackets)
            {
                Assert.AreEqual(inPackets[kvp.Key], resultPackets[kvp.Key]);
            }

        }        
        
        
        [Test]
        public async Task TestRejectConnectionContract()
        {
            var serializer = new MyServiceBusTcpSerializer();

            var inContract = new RejectConnectionContract
            {
                Message = "MyMessage"
            };

            var dataReader = new TcpDataReader(2048);
            var rawData = serializer.Serialize(inContract);
            await dataReader.NewPackageAsync(rawData);         

            var ct = new CancellationTokenSource();
            
            var res
                = await serializer.DeserializeAsync(dataReader, ct.Token);

            var result = (RejectConnectionContract) res;
            Assert.AreEqual(inContract.Message, result.Message);


        }          
        
    }
}
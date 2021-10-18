using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.TcpClient
{
    
    
    public class MyServiceBusTcpClient : IMyServiceBusPublisher, IMyServiceBusSubscriber
    {

        private readonly MyClientTcpSocket<IServiceBusTcpContract> _clientTcpSocket;

        private readonly List<(string topicName, int maxCachedSize)> _checkAndCreateTopics = new ();

        public MyServiceBusLog<MyServiceBusTcpClient> Log { get;}

        private readonly object _lockObject = new object();

        private long _requestId;
        
        public MyServiceBusTcpClient(Func<string> getHostPort, string name)
        {
            Log = new MyServiceBusLog<MyServiceBusTcpClient>(this);
            
            _clientTcpSocket = new MyClientTcpSocket<IServiceBusTcpContract>(
                    getHostPort,
                    TimeSpan.FromSeconds(3)
                )
                .RegisterTcpSerializerFactory(()=>new MyServiceBusTcpSerializer())
                .RegisterTcpContextFactory(() => new MyServiceBusTcpContext(_subscribers, name, 
                    ()=>_checkAndCreateTopics));
        }



        public SocketLog<MyClientTcpSocket<IServiceBusTcpContract>> SocketLogs => _clientTcpSocket.Logs;

        public MyServiceBusTcpClient CreateTopicIfNotExists(string topicName)
        {
            _checkAndCreateTopics.Add((topicName, 0));
            return this;
        }

        private readonly Dictionary<string, SubscriberInfo> _subscribers = new ();


        public void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IMyServiceBusMessage, ValueTask> callback, bool enableDeDublication = false)
        {
            var id = MyServiceBusTcpContext.GetId(topicId, queueId);

            _subscribers.Add(id,
                new SubscriberInfo(Log, topicId, queueId, topicQueueType, callback, null, enableDeDublication));
        }

        public void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IConfirmationContext, IReadOnlyList<IMyServiceBusMessage>, ValueTask> callback, bool enableDeDublication = false)
        {
            var id = MyServiceBusTcpContext.GetId(topicId, queueId);

            _subscribers.Add(id,
                new SubscriberInfo(Log, topicId, queueId, topicQueueType, null, callback, enableDeDublication));
        }


        public Task PublishAsync(string topicId, byte[] valueToPublish, bool immediatelyPersist)
        {
            var connection = (MyServiceBusTcpContext) _clientTcpSocket.CurrentTcpContext;

            if (connection == null)
                throw new Exception("No active connection found");


            lock (_lockObject)
            {
                _requestId++;
                
                return connection.PublishAsync(new PublishContract
                {
                    RequestId = _requestId,
                    Data = new []{valueToPublish},
                    ImmediatePersist = immediatelyPersist ? (byte)1 : (byte)0,
                    TopicId = topicId
                }, _lockObject); 
            }
        }
        
        public Task PublishAsync(string topicId, IEnumerable<byte[]> valueToPublish, bool immediatelyPersist)
        {
            var connection = (MyServiceBusTcpContext) _clientTcpSocket.CurrentTcpContext;

            if (connection == null)
                throw new PublishFailException( PublishFailReason.NoActiveConnection, "No active connection found");


            lock (_lockObject)
            {
                _requestId++;

                return connection.PublishAsync(new PublishContract
                {
                    RequestId = _requestId,
                    Data = valueToPublish.ToArray(),
                    ImmediatePersist = immediatelyPersist ? (byte)1 : (byte)0,
                    TopicId = topicId
                }, _lockObject);
            }
        }

        public bool Connected => _clientTcpSocket.Connected;

        public void Start()
        {
            _clientTcpSocket.Start();
        }

        public void Stop()
        {
            _clientTcpSocket.Stop();
        }

    }
}
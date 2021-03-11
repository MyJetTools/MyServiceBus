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

        private readonly List<(string topicName, int maxCachedSize)> _checkAndCreateTopics = new List<(string topicName, int maxCachedSize)>();

        public MyServiceBusLog<MyServiceBusTcpClient> Log { get;}
        
        public MyServiceBusTcpClient(Func<string> getHostPort, string name)
        {
            Log = new MyServiceBusLog<MyServiceBusTcpClient>(this);
            
            _clientTcpSocket = new MyClientTcpSocket<IServiceBusTcpContract>(
                    getHostPort,
                    TimeSpan.FromSeconds(3)
                )
                .RegisterTcpSerializerFactory(()=>new MyServiceBusTcpSerializer())
                .RegisterTcpContextFactory(() => new MyServiceBusTcpContext(_subscribers, name, 
                    _payLoadCollector, ()=>_checkAndCreateTopics));
        }


        private bool _throwExceptionIfPublishNoConnection;
        public MyServiceBusTcpClient ThrowExceptionOnPublishIfNoConnection(bool throwExceptionIfPublishNoConnection)
        {
            _throwExceptionIfPublishNoConnection = throwExceptionIfPublishNoConnection;
            return this;
        }

        public SocketLog<MyClientTcpSocket<IServiceBusTcpContract>> SocketLogs => _clientTcpSocket.Logs;

        public MyServiceBusTcpClient CreateTopicIfNotExists(string topicName)
        {
            _checkAndCreateTopics.Add((topicName, 0));
            return this;
        }

        private readonly Dictionary<string, SubscriberInfo> _subscribers = new ();


        public void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IMyServiceBusMessage, ValueTask> callback)
        {
            var id = MyServiceBusTcpContext.GetId(topicId, queueId);

            _subscribers.Add(id,
                new SubscriberInfo(Log, topicId, queueId, topicQueueType, callback, null));
        }

        public void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IConfirmationContext, IReadOnlyList<IMyServiceBusMessage>, ValueTask> callback)
        {
            var id = MyServiceBusTcpContext.GetId(topicId, queueId);

            _subscribers.Add(id,
                new SubscriberInfo(Log, topicId, queueId, topicQueueType, null, callback));
        }

        public Task PublishAsync(string topicId, byte[] valueToPublish, bool immediatelyPersist)
        {
            var connection = (MyServiceBusTcpContext) _clientTcpSocket.CurrentTcpContext;

            if (_throwExceptionIfPublishNoConnection)
            {
                if (connection == null)
                    throw new Exception("No active connection");
            }

            var result = _payLoadCollector.AddMessage(connection.Id, topicId, 
                valueToPublish, null, immediatelyPersist);

            var nextPayloadToPublish = _payLoadCollector.GetNextPayloadToPublish();
            
            if (nextPayloadToPublish != null)
                connection.Publish(nextPayloadToPublish);
            
            return result;
        }
        
        public Task PublishAsync(string topicId, IEnumerable<byte[]> payLoads, bool immediatelyPersist)
        {
            var connection = (MyServiceBusTcpContext)_clientTcpSocket.CurrentTcpContext;
            
            if (_throwExceptionIfPublishNoConnection)
            {
                if (connection == null)
                    throw new Exception("No active connection");
            }

            var publishData 
                = payLoads.Select(bytes => (bytes, (IReadOnlyDictionary<string, string>)null));

            var result = _payLoadCollector.AddMessages(connection.Id, topicId, publishData, immediatelyPersist);

            var nextPayloadToPublish = _payLoadCollector.GetNextPayloadToPublish();

            if (nextPayloadToPublish != null)
                connection.Publish(nextPayloadToPublish);

            return result;
        }

        public PublishBuilder PublishWithMetaData(string topicId, byte[] payLoad, bool immediatelyPersist)
        {
            var connection = (MyServiceBusTcpContext)_clientTcpSocket.CurrentTcpContext;
            
            if (_throwExceptionIfPublishNoConnection)
            {
                if (connection == null)
                    throw new Exception("No active connection");
            }
            
            return new PublishBuilder(connection.Id, _payLoadCollector, topicId, payLoad, immediatelyPersist);
        }
        

        private readonly PayLoadCollector _payLoadCollector = new (1024*1024*5);
        
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
using System;
using MyServiceBus.Domains.QueueSubscribers;


namespace MyServiceBus.Domains.Sessions
{

    
    

    public class MyServiceBusSession : IDisposable
    {
        private readonly Action<MyServiceBusSession> _onDispose;
        
        public readonly TopicPublisherInfo PublisherInfo = new ();

        public readonly SubscribersInfo Subscribers = new ();
        
        public DateTime Created { get;  } = DateTime.UtcNow;
        
        public DateTime LastAccessTime { get; set; } = DateTime.UtcNow;
        
        public bool Disconnected { get; set; }
        
        public int ProtocolVersion { get; set; }
        public int ReadBytes { get; set; }
        public int SentBytes { get; set; }

        public string Id { get; }
        
        public string Name { get; set; }
        
        public string ClientVersion { get; set; }
        public string Ip { get; set; }


        private Action<QueueSubscriber> _sendMessages;

        public void SendMessagesToSubscriber(QueueSubscriber subscriber)
        {
            if (_sendMessages == null)
                throw new Exception("Send messages is not plugged");

            _sendMessages(subscriber);
        }


        public void PlugSendMessages(Action<QueueSubscriber> sendMessages)
        {
            _sendMessages = sendMessages;
        }

        public MyServiceBusSession(string sessionId, Action<MyServiceBusSession> onDispose)
        {
            _onDispose = onDispose;
            Id = sessionId;
        }

        public void OneSecondTimer()
        {
            PublisherInfo.OneSecondTimer();
            Subscribers.OneSecondTimer();
        }

        public void Dispose()
        {
            _onDispose(this);
        }
    }

}
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyServiceBus.TcpClient
{
    public class PublishBuilder
    {
        private readonly long _connectionId;
        private readonly PayLoadCollector _payLoadCollector;

        private Dictionary<string, string> _metaData;
        public PublishBuilder(long connectionId, PayLoadCollector payLoadCollector, string topicId, 
            byte[] payLoad, bool immediatelyPersist)
        {
            _connectionId = connectionId;
            _payLoadCollector = payLoadCollector;
            _immediatelyPersist = immediatelyPersist;
            _topicId = topicId;
            _payLoad = payLoad;

        }

        public void WithMetadata(string key, string value)
        {
            _metaData ??= new Dictionary<string, string>();
            _metaData.Add(key, value);
        }

        private readonly string _topicId;
        private readonly byte[] _payLoad;
        private readonly bool _immediatelyPersist;

        public Task PublishAsync()
        {
            return _payLoadCollector.AddMessage(_connectionId, _topicId, _payLoad, _metaData, _immediatelyPersist);
        }
    }
}
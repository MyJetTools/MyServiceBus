using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyServiceBus.TcpClient
{

    public class PayloadPackage
    {
        public long RequestId { get; }
        
        public string TopicId { get; }
        
        public bool ImmediatelyPersist { get; private set; }
        
        public bool OnPublishing { get; set; }
        
        public long ConnectionId { get; }

        public PayloadPackage(string topicId, long requestId, long connectionId)
        {
            ConnectionId = connectionId;
            TopicId = topicId;
            RequestId = requestId;
        }

        public readonly TaskCompletionSource<int> CommitTask = new TaskCompletionSource<int>();

        private readonly List<(byte[] data, IReadOnlyList<KeyValuePair<string, string>>)> _payLoads = new ();

        public IReadOnlyList<(byte[] data, IReadOnlyList<KeyValuePair<string, string>>)> PayLoads => _payLoads;

        public void AddRange(IEnumerable<(byte[] data, IReadOnlyList<KeyValuePair<string, string>>)> payLoads, bool immediatelyPersist)
        {
            if (immediatelyPersist)
                ImmediatelyPersist = true;

            foreach (var payLoad in payLoads)
            {
                _payLoads.Add(payLoad);
                PayLoadSize++;
            }
        }

        public void Add(byte[] payLoad, bool immediatelyPersist)
        {
            if (immediatelyPersist)
                ImmediatelyPersist = true;
            
            _payLoads.Add((payLoad, Array.Empty<KeyValuePair<string, string>>()));
            PayLoadSize++;
        }
        
        public void Add(byte[] payLoad, bool immediatelyPersist, IReadOnlyList<KeyValuePair<string, string>> metaData)
        {
            if (immediatelyPersist)
                ImmediatelyPersist = true;
            
            _payLoads.Add((payLoad, metaData));
            PayLoadSize++;
        }

        public long PayLoadSize { get; private set; } 
    }

    
    public class PayLoadCollector
    {
        private readonly int _maxPayLoadSize;

        private long _nextRequestId;

        private readonly SortedDictionary<string, List<PayloadPackage>> _readyToGo = new SortedDictionary<string, List<PayloadPackage>>();

        public PayLoadCollector(int maxPayLoadSize)
        {
            _maxPayLoadSize = maxPayLoadSize;
        }

        private PayloadPackage AddPayloadPackage(string topicId, long connectionId)
        {
            _nextRequestId++;
            var result = new PayloadPackage(topicId, _nextRequestId, connectionId);

            if (_readyToGo.TryGetValue(topicId, out var list))
            {
                list.Add(result);
            }
            else
                _readyToGo[topicId].Add(result);
            
            return result;
        }

        private List<PayloadPackage> GetPayloadPackagesByTopic(string topicId)
        {
            if (_readyToGo.TryGetValue(topicId, out var foundResult))
                return foundResult;

            var result = new List<PayloadPackage>();
            _readyToGo.Add(topicId, result);
            return result;
        }

        private PayloadPackage GetNextPayloadPackage(string topicId, long connectionId)
        {
            var payloadsByTopic = GetPayloadPackagesByTopic(topicId);

            if (payloadsByTopic.Count == 0)
                return AddPayloadPackage(topicId, connectionId);

            var lastPayload = payloadsByTopic[^1];

            if (lastPayload.PayLoadSize >= _maxPayLoadSize || lastPayload.OnPublishing)
                return AddPayloadPackage(topicId, connectionId);

            return lastPayload;
        }
        
        private void CheckIfItStillConnected(long connectionId)
        {
            if (_lastDisconnectId == -1)
                return;

            if (_lastDisconnectId == connectionId)
                throw new Exception("Disconnected");

        }

        private static IReadOnlyList<KeyValuePair<string, string>> _emptyPayload = Array.Empty<KeyValuePair<string, string>>();
        
        public Task AddMessage(long connectionId, string topicId, IEnumerable<byte[]> newPayLoad, bool immediatelyPersist)
        {
            lock (_readyToGo)
            {
                CheckIfItStillConnected(connectionId);
                var payLoadPackage = GetNextPayloadPackage(topicId, connectionId);

                var payLoad = newPayLoad.Select(itm => (itm, _emptyPayload));
                payLoadPackage.AddRange(payLoad, immediatelyPersist);
                return payLoadPackage.CommitTask.Task;
            }
        }
 
        public Task AddMessage(long connectionId, string topicId, byte[] newPayLoad, bool immediatelyPersist)
        {
            lock (_readyToGo)
            {
                CheckIfItStillConnected(connectionId);
                var payLoadPackage = GetNextPayloadPackage(topicId, connectionId);
                payLoadPackage.Add(newPayLoad, immediatelyPersist);
                return payLoadPackage.CommitTask.Task;
            }
        }
        
        public PayloadPackage GetNextPayloadToPublish()
        {
            lock (_readyToGo)
            {

                if (_readyToGo.Count == 0)
                    return null;


                foreach (var topicData in _readyToGo.Where(topicData => topicData.Value.Count > 0))
                {
                    var resultPackage = topicData.Value.First();
                    if (resultPackage.OnPublishing)
                        continue;
                    
                    resultPackage.OnPublishing = true;
                    return resultPackage;
                }

                return null;
            }
        }

        public void SetPublished(long requestId)
        {
            lock (_readyToGo)
            {
                foreach (var topicData in _readyToGo.Where(topicData => topicData.Value.Count > 0))
                {
                    var packageToCommit = topicData.Value.First();

                    if (packageToCommit.RequestId != requestId)
                        continue;

                    topicData.Value.RemoveAt(0);
                    packageToCommit.CommitTask.SetResult(0);
                }
            }
            
        }

        private long _lastDisconnectId = -1;

        public void Disconnect(long connectionId)
        {
            lock (_readyToGo)
            {
                _lastDisconnectId = connectionId;
                foreach (var value in _readyToGo.Values)
                {
                    while (value.Count > 0)
                    {
                        var first = value.First();
                        if (first.ConnectionId != connectionId)
                            break;
                        
                        first.CommitTask.SetException(new Exception("Disconnected"));
                        value.RemoveAt(0);
                    }
                }
            }
        }
    }
}
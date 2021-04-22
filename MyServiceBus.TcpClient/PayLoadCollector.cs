using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyServiceBus.TcpClient
{
    public class PayLoadCollector
    {
        private readonly int _maxPayLoadSize;

        private readonly SortedDictionary<string, PayloadPackagesList> _packagesByTopic = new ();

        private bool _disconnected;

        public PayLoadCollector(int maxPayLoadSize)
        {
            _maxPayLoadSize = maxPayLoadSize;
        }


        private static long _nextId;

        private static long GetNextId()
        {
            _nextId++;
            return _nextId;
        }

        private PayloadPackagesList GetOrCreatePayloadPackagesByTopic(string topicId)
        {
            if (_packagesByTopic.TryGetValue(topicId, out var foundResult))
                return foundResult;

            var result = new PayloadPackagesList(_maxPayLoadSize, GetNextId);
            _packagesByTopic.Add(topicId, result);
            return result;
        }

        private PayloadPackage GetNextPayloadPackageToPost(string topicId)
        {
            var payloadsByTopic = GetOrCreatePayloadPackagesByTopic(topicId);

            return payloadsByTopic.GetNextPayloadPackageToPost();
        }

        private void CheckIfItStillConnected()
        {
            if (_disconnected)
                throw new Exception("Disconnected");
        }

        public Task PostMessages(string topicId, IEnumerable<byte[]> newPayLoad, bool immediatelyPersist)
        {
            lock (_packagesByTopic)
            {
                CheckIfItStillConnected();
                var payLoadPackage = GetNextPayloadPackageToPost(topicId);
                payLoadPackage.Add(newPayLoad, immediatelyPersist);
                return payLoadPackage.GetTask();
            }
        }
 
        public Task PostMessage(string topicId, byte[] newPayLoad, bool immediatelyPersist)
        {
            lock (_packagesByTopic)
            {
                CheckIfItStillConnected();
                var payLoadPackage = GetNextPayloadPackageToPost(topicId);
                payLoadPackage.Add(newPayLoad, immediatelyPersist);
                return payLoadPackage.GetTask();
            }
        }

        public (PayloadPackage nextPackage, string topicId) GetNextPayloadToPublish()
        {
            lock (_packagesByTopic)
            {

                if (_packagesByTopic.Count == 0)
                    return (null, null);

                foreach (var (topicId, packagesList) in _packagesByTopic)
                {
                    var nextPackageToDeliver = packagesList.GetNextPayloadPackageToDeliver();

                    if (nextPackageToDeliver != null)
                        return (nextPackageToDeliver, topicId);
                }

                return (null, null);
            }
        }

        public void SetPublished(long requestId)
        {
            PayloadPackage singleTask = null;
            List<PayloadPackage> taskToComplete = null;

            lock (_packagesByTopic)
            {
                foreach (var (topicId, packagesList) in _packagesByTopic)
                {
                    var committedPackage = packagesList.TryToCommit(requestId);

                    if (committedPackage == null)
                        continue;

                    if (singleTask == null)
                        singleTask = committedPackage;
                    else
                    {
                        taskToComplete ??= new();
                        taskToComplete.Add(committedPackage);
                    }
                }

            }

            singleTask?.CommitTask();
            taskToComplete?.ForEach(task => task.CommitTask());
        }

        public void Disconnect()
        {
            List<PayloadPackage> result = null;
            lock (_packagesByTopic)
            {
                _disconnected = true;
                foreach (var value in _packagesByTopic.Values)
                {
                    var allPackages = value.ClearAll();
                    
                    if (allPackages.Count>0)
                    {
                        result ??= new List<PayloadPackage>();
                        result.AddRange(allPackages);
                    }
                }
            }
            
            if (result == null)
                return;

            foreach (var payloadPackage in result)
            {
                payloadPackage.Disconnect();
            }
        }
    }
}
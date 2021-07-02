using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Server.Hubs
{
    
    public class MonitoringConnection
    {
        
        public IClientProxy ClientProxy { get; }
        
        public string Id { get; }


        private int _topicsSnapshotId;

        private IReadOnlyList<MonitoringConnectionTopicContext> _topicContexts =
            Array.Empty<MonitoringConnectionTopicContext>();

        public bool TopicSnapshotHasChanged(int topicsSnapshotId, IReadOnlyList<MyTopic> topics)
        {
            lock (this)
            {
                var result = topicsSnapshotId != _topicsSnapshotId;

                if (result)
                {
                    _topicsSnapshotId = topicsSnapshotId;
                    _topicContexts = topics.Select(MonitoringConnectionTopicContext.Create).ToList();

                }

                return result;
            }
        }

        public IReadOnlyList<MonitoringConnectionTopicContext> GetTopicContexts()
        {
            return _topicContexts;
        }


        public readonly object LockObject = new ();

        private readonly Dictionary<string, bool> _sentLastTimeAsEmpty = new ();
        
    
        internal bool DidWeSendLastTimeAsEmptyTopicGraph(string topic)
        {
            lock (LockObject)
            {
                return _sentLastTimeAsEmpty.TryGetValue(topic, out var result) && result;
            }
        }

        public void SetSentAsEmptyLastTime(string topic, bool value)
        {
            lock (LockObject)
            {
                if (_sentLastTimeAsEmpty.ContainsKey(topic))
                    _sentLastTimeAsEmpty[topic] = value;
                else
                    _sentLastTimeAsEmpty.Add(topic, value);
            }
        }


        #region LastQueueDurationGraphSentSnapshot

        private readonly Dictionary<string, Dictionary<string, long>> _lastQueueDurationGraphSnapshot
            = new ();

        internal void SetLastQueueDurationGraphSentSnapshot(string topicId, string queueId, long snapshotId)
        {

            lock (LockObject)
            {
                if (!_lastQueueDurationGraphSnapshot.ContainsKey(topicId))
                    _lastQueueDurationGraphSnapshot.Add(topicId, new Dictionary<string, long>());

                var topicSnapshot = _lastQueueDurationGraphSnapshot[topicId];

                if (topicSnapshot.ContainsKey(queueId))
                    topicSnapshot[queueId] = snapshotId;
                else
                    topicSnapshot.Add(queueId, snapshotId);
            }
            
        }

        internal long GetLastQueueDurationGraphSentSnapshot(string topicId, string queueId)
        {
            lock (LockObject)
            {
                if (_lastQueueDurationGraphSnapshot.TryGetValue(topicId, out var topicSnapshot))
                    if (topicSnapshot.TryGetValue(queueId, out var result))
                        return result;

                return -100;
            }
        }
        
        #endregion

        public MonitoringConnection(string id, IClientProxy clientProxy)
        {
            Id = id;
            ClientProxy = clientProxy;
        }
    }
}
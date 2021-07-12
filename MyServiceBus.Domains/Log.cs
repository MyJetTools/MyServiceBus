using System;
using System.Collections.Generic;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogItem
    {
        public DateTimeOffset DateTime { get; set; }
        
        public LogLevel Level { get; set; }
        
        public string QueueId { get; set; }
        public string Message { get; set; }
    }
    
    internal class LogsByTopic
    {
        
        private readonly Queue<LogItem> _items = new Queue<LogItem>();

        public void Add(LogItem item)
        {
            _items.Enqueue(item);
            while (_items.Count>100)
                _items.Dequeue();
        }
    }
    
    public class Log
    {

        private readonly Dictionary<string, LogsByTopic> _messages = new Dictionary<string, LogsByTopic>();

        public void AddLog(LogLevel level, TopicQueue queue, string message)
        {
            AddLog(level, queue.Topic.TopicId, queue.QueueId, message);
        }

        public void AddLog(LogLevel level, string topicId, string queueId, string message)
        {

            var newItem = new LogItem
            {
                Message = message,
                QueueId = queueId,
                DateTime = DateTimeOffset.UtcNow,
                Level = level
            };
            
            lock (_messages)
            {
                if (!_messages.ContainsKey(topicId))
                    _messages.Add(topicId, new LogsByTopic());
                
                _messages[topicId].Add(newItem);
            }
        }
        
    }
}
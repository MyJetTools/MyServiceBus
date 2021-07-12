using System;
using System.Collections.Generic;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.MessagesContent
{
    
    public struct MessagesPageId
    {

        public MessagesPageId(long pageId)
        {
            Value = pageId;
        }
    
        public long Value { get; set; }

        public bool EqualsTo(long pageId)
        {
            return Value == pageId;
        }
        
        public bool EqualsTo(MessagesPageId pageId)
        {
            return Value == pageId.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static MessagesPageId CreateFromMessageId(long messageId)
        {
            return messageId.GetMessageContentPageId();
        }
    }
    
    public class MessagesPage
    {

        private readonly Dictionary<long, MessageContentGrpcModel> _messages;

        public MessagesPageId PageId { get;  }
        
        public DateTime Created { get; } = DateTime.UtcNow;

        public MessagesPage(MessagesPageId pageId)
        {
            PageId = pageId;
            _messages = new Dictionary<long, MessageContentGrpcModel>();
        }

        public long ContentSize { get; private set; }
        public int Count => _messages.Count;
        public int Percent { get; private set; }


        public bool Add(MessageContentGrpcModel message)
        {
            if (!_messages.ContainsKey(message.MessageId))
            {
                _messages.Add(message.MessageId, message);
                ContentSize += message.Data.Length;
                Percent = (int)(_messages.Count * 0.001);
                return true;
            }

            return false;
        }
        
        public MessageContentGrpcModel TryGet(long messageId)
        {
            return _messages.ContainsKey(messageId) ? _messages[messageId] : null;
        }

    }
    
}
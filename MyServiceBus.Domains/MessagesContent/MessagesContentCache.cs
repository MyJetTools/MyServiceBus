using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotNetCoreDecorators;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.MessagesContent
{

    public class MessagesContentCache
    {
        private readonly Dictionary<long, MessagesPageInMemory> _messages = new ();
        public string TopicId { get; }

        private readonly ReaderWriterLockSlim _lockSlim = new ();

        public IReadOnlyList<long> Pages { get; private set; } = Array.Empty<long>();
        
        public MessagesContentCache(string topicId)
        {
            TopicId = topicId;
        }

        /// <summary>
        /// Lock disclaimer. This method is only executed from the Topic Publish method
        /// </summary>
        /// <param name="messages"></param>
        public void AddMessages(IEnumerable<MessageContentGrpcModel> messages)
        {
            
            _lockSlim.EnterWriteLock();
            try
            {
                var hasNewPage = false;
                
                foreach (var message in messages)
                {
                    var pageId = message.GetMessageContentPageId();

                    var (page, created) = _messages.GetOrCreate(pageId.Value, () => new MessagesPageInMemory(pageId));


                    if (created)
                        hasNewPage = true; 
                    
                    page.Add(message);
                }
                
                if (hasNewPage)
                    Pages = _messages.Keys.OrderBy(key => key).AsReadOnlyList();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
    
        }

        public bool HasCacheLoaded(in long pageId)
        {
            _lockSlim.EnterReadLock();
            try
            {
                return _messages.ContainsKey(pageId);
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }

        public void UploadPage(MessagesPageInMemory page)
        {
            _lockSlim.EnterWriteLock();
            try
            {
                var (_, added) = _messages.GetOrCreate(page.PageId.Value, ()=>page);
                if (added) 
                    Pages = _messages.Keys.OrderBy(key => key).AsReadOnlyList();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }

        }

        private IReadOnlyList<long> GetKeysToGarbageCollect(IDictionary<long, long> activePages)
        {
            List<long> result = null;
            
            _lockSlim.EnterReadLock();
            try
            {
 
                foreach (var pageId in _messages.Keys)
                {
                    if (!activePages.ContainsKey(pageId))
                    {
                        result ??= new List<long>();
                        result.Add(pageId);
                    }
                }
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }

            return result;
        }

        public void GarbageCollect(IDictionary<long, long> activePages)
        {
            var pagesToGc = GetKeysToGarbageCollect(activePages);
                            
            if (pagesToGc == null)
                return;
            
            _lockSlim.EnterWriteLock();
            try
            {

                foreach (var pageToGc in pagesToGc)
                {
                    Console.WriteLine($"Garbage collecting page for Topic {TopicId} from MessagesCache with #" +
                                      pageToGc);
                     _messages.Remove(pageToGc);
                }

                Pages = _messages.Keys.OrderBy(key => key).AsReadOnlyList();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }

        }

        public (MessageContentGrpcModel message, bool pageIsLoaded) TryGetMessage(MessagesPageId pageId, long messageId)
        {
            return _messages.TryGetValue(pageId.Value, out var page)
                ? (page.TryGet(messageId), true) 
                : (null, false);
        }
        
    }

    
}
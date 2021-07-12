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
        private readonly Dictionary<long, MessagesPage> _messages = new Dictionary<long, MessagesPage>();
        private readonly string _topicId;

        private readonly ReaderWriterLockSlim _lockSlim = new ReaderWriterLockSlim();
        public IReadOnlyList<long> Pages { get; private set; } = Array.Empty<long>();

        public MessagesContentCache(string topicId)
        {
            _topicId = topicId;
        }

        public IEnumerable<(long no, long size, int percent)> GetPages()
        {
            var pages = Pages;

            foreach (var no in pages)
            {
                var page = TryGetPage(no);
                
                if (page == null)
                    continue;

                yield return (no, page.ContentSize, page.Percent);
            }

        }

        private MessagesPage TryGetPage(long no)
        {
            _lockSlim.EnterReadLock();
            try
            {
                return _messages.TryGetOrDefault(no);

            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
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

                    var (page, created) = _messages.GetOrCreate(pageId.Value, () => new MessagesPage(pageId));


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

        public bool HasCacheLoaded(in MessagesPageId pageId)
        {
            _lockSlim.EnterReadLock();
            try
            {
                return _messages.ContainsKey(pageId.Value);
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }

        public void UploadPage(MessagesPage page)
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
        
        private readonly TimeSpan _minPageLifeTime = TimeSpan.FromSeconds(30);

        private IReadOnlyList<long> GetKeysToGarbageCollect(IDictionary<long, long> activePages)
        {
            List<long> result = null;
            
            _lockSlim.EnterReadLock();
            try
            {
 
                foreach (var (pageId, pageInMemory) in _messages)
                {
                    if (activePages.ContainsKey(pageId)) 
                        continue;
                    
                    if (DateTime.UtcNow - pageInMemory.Created < _minPageLifeTime)
                        continue;
                        
                    result ??= new List<long>();
                    result.Add(pageId);
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
                    Console.WriteLine($"Garbage collecting page for Topic {_topicId} from MessagesCache with #" +
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


        public MessagesPage TryGetPage(MessagesPageId pageId)
        {
            _lockSlim.EnterReadLock();
            try
            {

                return _messages.TryGetValue(pageId.Value, out var result)
                    ? result
                    : null;
            }
            finally
            {
                _lockSlim.ExitReadLock();
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
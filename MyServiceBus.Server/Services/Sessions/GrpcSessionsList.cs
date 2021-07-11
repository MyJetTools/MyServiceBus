using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MyServiceBus.Domains;

namespace MyServiceBus.Server.Services.Sessions
{
    
    public class GrpcSessionsList
    {

        private readonly ReaderWriterLockSlim _readerWriterLock = new();

        private long _sessionId;

        private readonly Dictionary<long, GrpcSession> _sessions = new ();

        private IReadOnlyList<GrpcSession> _sessionsAsList = Array.Empty<GrpcSession>();

        public GrpcSessionsList()
        {
            LastSessionsGcScan = DateTime.UtcNow;
        }

        private long GetSessionId()
        {
            return _sessionId++;
        }
        
        public GrpcSession GenerateNewSession(string name, string clientVersion)
        {
            _readerWriterLock.EnterWriteLock();
            try
            {
                var result = new GrpcSession(GetSessionId(), name, clientVersion);
                _sessions.Add(result.Id, result);
                _sessionsAsList = _sessions.Values.ToList();
                return result;
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

        }


        public GrpcSession TryGetSession(long sessionId, DateTime utcNow)
        {
            _readerWriterLock.EnterReadLock();
            try
            {
                var result = _sessions.TryGetOrDefault(sessionId);
                result?.UpdateLastAccess(utcNow);
                return result;
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

        }


        private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

        private IReadOnlyList<GrpcSession> GetSessionsToGc(DateTime now)
        {
            List<GrpcSession> result = null;

            foreach (var grpcSession in _sessionsAsList.Where(grpcSession => now - grpcSession.LastAccess >= OneMinute)
            )
            {
                result ??= new List<GrpcSession>();
                result.Add(grpcSession);
            }

            return result;
        }

        public DateTime LastSessionsGcScan { get; private set; }

        public void GarbageCollectSessions(DateTime now)
        {
            if (now - LastSessionsGcScan < OneMinute)
                return;
            
            var sessionsToGc = GetSessionsToGc(now);

            try
            {
                if (sessionsToGc == null)
                    return;

                _readerWriterLock.EnterWriteLock();
                try
                {
                    foreach (var sessionToGc in sessionsToGc)
                    {
                        _sessions.TryRemoveOrDefault(sessionToGc.Id);
                    }
                    
                    _sessionsAsList = _sessions.Values.ToList();
                }
                finally
                {
                    _readerWriterLock.ExitWriteLock();
                }

            }
            finally
            {
                LastSessionsGcScan = now;
            }
        }

        public void TryRemoveSession(long sessionId)
        {
            _readerWriterLock.EnterWriteLock();
            try
            {
                _sessions.TryRemoveOrDefault(sessionId);
                _sessionsAsList = _sessions.Values.ToList();
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }
        }

        public IReadOnlyCollection<GrpcSession> GetAll()
        {
            return _sessionsAsList;
        }

        public int Count => _sessionsAsList.Count;
    }
}
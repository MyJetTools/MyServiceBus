using System;
using System.Collections.Generic;
using System.Linq;

namespace MyServiceBus.Domains.Sessions
{
    public class SessionsList
    {
        private readonly Dictionary<string, MyServiceBusSession> _sessions =
            new Dictionary<string, MyServiceBusSession>();

        private IReadOnlyList<MyServiceBusSession> _sessionsAsList = Array.Empty<MyServiceBusSession>();


        private readonly object _lockObject = new object();


        public MyServiceBusSession IssueSession()
        {
            var id = Guid.NewGuid().ToString();

            var session = new MyServiceBusSession(id, Remove);

            lock (_lockObject)
            {
                _sessions.Add(id, session);
                _sessionsAsList = _sessions.Values.ToList();
            }

            return session;
        }


        private void Remove(MyServiceBusSession session)
        {
            lock (_lockObject)
            {
                if (_sessions.Remove(session.Id))
                {
                    _sessionsAsList = _sessions.Values.ToList();
                }
            }
        }



        public void OneSecondTimer()
        {
            foreach (var session in _sessionsAsList)
                session.OneSecondTimer();
        }
        
        
    }
}
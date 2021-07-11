using System;
using MyServiceBus.Domains.Sessions;

namespace MyServiceBus.Server.Services.Sessions
{
    public class GrpcSession
    {
        
        public DateTime Created { get; } = DateTime.UtcNow;
        
        public DateTime LastAccess { get; private set; } = DateTime.UtcNow;

        internal void UpdateLastAccess(DateTime utcNow)
        {
            LastAccess = utcNow;
        }
        
        public GrpcSession(long id, string name, string clientVersion)
        {
            Id = id;
            Name = name;
            ClientVersion = clientVersion;
        }
        public long Id { get; }

        public MyServiceBusSessionContext SessionContext = new ();
        
        public string Name { get; }
        
        public string ClientVersion { get; }

    }
    
    
}
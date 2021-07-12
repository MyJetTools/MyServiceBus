using MyServiceBus.Domains.Sessions;

namespace MyServiceBus.Server.Services.Sessions
{
    public class GrpcSession
    {
        public MyServiceBusSession Session { get; }

        public GrpcSession(long id, MyServiceBusSession session)
        {
            Session = session;
            Id = id;
        }

        public long Id { get; }
    }
    
    
}
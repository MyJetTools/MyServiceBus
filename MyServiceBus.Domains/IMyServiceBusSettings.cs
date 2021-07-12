using System;

namespace MyServiceBus.Domains
{
    public interface IMyServiceBusSettings
    {
        
        int MaxDeliveryPackageSize { get; }
        
        int MaxPersistencePackage { get; }
        
        TimeSpan QueueGcTimeout { get; }

    }
}
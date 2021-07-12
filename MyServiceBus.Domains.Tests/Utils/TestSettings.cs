using System;
using MyServiceBus.Domains;

namespace MyServiceBus.Domains.Tests.Utils
{
    public class TestSettings : IMyServiceBusSettings
    {
        public int MaxDeliveryPackageSize { get; } = 1024 * 1024;
        public int MaxPersistencePackage { get; } = 1024 * 1024 * 3;
        public TimeSpan QueueGcTimeout { get; } = TimeSpan.FromSeconds(20);
    }
}
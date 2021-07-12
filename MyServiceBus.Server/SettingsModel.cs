using System;
using MyServiceBus.Domains;
using MyYamlParser;

namespace MyServiceBus.Server
{
    public class SettingsModel : IMyServiceBusSettings
    {
        [YamlProperty]
        public string GrpcUrl { get; set; }
        
        [YamlProperty(defaultValue: 1024*1024)]
        public int MaxDeliveryPackageSize { get; set; }

        [YamlProperty(defaultValue: 1024*1024*3)]
        public int MaxPersistencePackage { get; set; }

        [YamlProperty]
        public TimeSpan QueueGcTimeout { get; set; }
    }

}
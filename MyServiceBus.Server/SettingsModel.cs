using System;
using System.IO;
using MyServiceBus.Domains;
using MyYamlParser;

namespace MyServiceBus.Server
{


    
    public class SettingsModel : IMyServiceBusSettings
    {
        public SettingsModel()
        {
            _eventuallyPersistentDelay = new Lazy<TimeSpan>(() => TimeSpan.Parse(EventuallyPersistenceDelay));
        }
        
        [YamlProperty("Server.ListenHttpPort")]
        public string ListenHttpPort { get; set; }
        
        [YamlProperty("Server.ListenHttp2Port")]
        public string ListenHttp2Port { get; set; }
        
        [YamlProperty("Server.Mode")]
        public string Mode { get; set; }
        

        [YamlProperty("Server.ListenTcpPort")]
        public string ListenTcpPort { get; set; }

        [YamlProperty("Persistent.QueueConnectionString")]
        public string QueuesConnectionString { get; set; }
        
        [YamlProperty("Persistent.MessagesConnectionString")]
        public string MessagesConnectionString { get; set; }

        TimeSpan IMyServiceBusSettings.EventuallyPersistenceDelay => _eventuallyPersistentDelay.Value;
        private readonly Lazy<TimeSpan> _eventuallyPersistentDelay;
            
        [YamlProperty]
        public string EventuallyPersistenceDelay { get; set; }
        
        [YamlProperty("Master.HostPort")]
        public string MasterHostPort { get; set; }
    }
    
    
    public static class SettingsReader
    {
        public static SettingsModel Read()
        {
            var homePath = Environment.GetEnvironmentVariable("HOME");
            var yamlContent = File.ReadAllBytes(homePath+".myservicebus");
            return MyYamlDeserializer.Deserialize<SettingsModel>(yamlContent);
        }
    }


    public static class SettingsUtils
    {
        private const int DefaultHttpPort = 6123;
        private const int DefaultHttp2Port = 6124;
        private const int DefaultTcpPort = 6421;

        private static int GetListenPort(this SettingsModel src, Func<SettingsModel, string> getListenPort, 
            int defaultPort, Func<string> getErrorMessage)
        {
            try
            {

                var portString = getListenPort(src);
                return string.IsNullOrEmpty(portString) 
                    ? defaultPort 
                    : int.Parse(portString);
            }
            catch (Exception)
            {
                Console.WriteLine(getErrorMessage());
                return defaultPort;
            }
        }
        
        public static int GetHttpPort(this SettingsModel src)
        {
            return src.GetListenPort(itm => itm.ListenHttpPort, DefaultHttpPort,
                () => "Invalid Listen Http2 Port: " + src.ListenHttpPort);
        }
        
        public static int GetHttp2Port(this SettingsModel src)
        {
            return src.GetListenPort(itm => itm.ListenHttp2Port, 
                DefaultHttp2Port, () => "Invalid Listen Http2 Port: " + src.ListenHttp2Port);
        }
        
        public static int GetTcpPort(this SettingsModel src)
        {
            return src.GetListenPort(itm => itm.ListenTcpPort, 
                DefaultTcpPort, () => "Invalid Listen TCP Port: " + src.ListenTcpPort);
        }


        public static ServerMode GetServerMode(this SettingsModel src)
        {
            try
            {
                return Enum.Parse<ServerMode>(src.Mode);
            }
            catch (Exception)
            {
               throw new Exception($"Invalid Server.Mode value. It must be {ServerMode.Server} or  {ServerMode.Proxy}");
            }
        }
    }

}
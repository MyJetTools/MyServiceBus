using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MyTcpSockets;
using MyTcpSockets.Extensions;

namespace MyServiceBus.TcpContracts
{
    public class MyServiceBusTcpSerializer : ITcpSerializer<IServiceBusTcpContract>
    {

        private int _protocolVersion;
        
        private readonly Dictionary<byte, int> _packetVersions = new Dictionary<byte, int>();

        private int GetPacketVersion(byte packet)
        {
            if (_packetVersions.ContainsKey(packet))
                return _packetVersions[packet];

            return 0;
        }

        public void HandlePacketVersions(PacketVersionsContract packetVersions)
        {
            foreach (var (key, value) in packetVersions.GetPackets())
                _packetVersions.Add(key, value);  
        }

        public ReadOnlyMemory<byte> Serialize(IServiceBusTcpContract data)
        {
            
            if (data is GreetingContract greetingContract)
                _protocolVersion = greetingContract.ProtocolVersion;
            
            if (data is PacketVersionsContract packetVersions)
                HandlePacketVersions(packetVersions);            
            
            var mem = new MemoryStream();

            var command = DataContractsMapper.ResolveCommandType(data);
            
            var packetVersion = GetPacketVersion(command);

            mem.WriteByte(command);
            data.Serialize(mem, _protocolVersion, packetVersion);
            return mem.ToArray();
        }

        public async ValueTask<IServiceBusTcpContract> DeserializeAsync(ITcpDataReader reader, CancellationToken ct)
        {

            var command = await reader.ReadAndCommitByteAsync(ct);

            var instance = DataContractsMapper.ResolveDataContact(command);

            var packetVersion = GetPacketVersion(command);

            await instance.DeserializeAsync(reader, _protocolVersion, packetVersion, ct);

            if (instance is GreetingContract greetingContract)
            {
                Console.WriteLine($"Greeting: {greetingContract.Name}; ProtocolVersion: " +
                                  greetingContract.ProtocolVersion);
                _protocolVersion = greetingContract.ProtocolVersion;
            }

            if (instance is PacketVersionsContract packetVersions)
                HandlePacketVersions(packetVersions);

            return instance;
        }
    }
    
}
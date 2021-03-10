using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using MyServiceBus.Abstractions.QueueIndex;
using MyTcpSockets.Extensions;

namespace MyServiceBus.TcpContracts
{
    public static class DataContractUtils
    {

        public static void WriteLong(this Stream stream, long value, long protocolVersion)
        {
            if (protocolVersion < 2)
            {
                stream.WriteInt((int)value);
                return;
            }
            
            stream.WriteLong(value);
        }
        
        public static async ValueTask<long> ReadLongAsync(this ITcpDataReader dataReader, long protocolVersion, CancellationToken ct)
        {
            if (protocolVersion < 2)
                return await dataReader.ReadIntAsync(ct);

            return await dataReader.ReadLongAsync(ct);
        }
        
        
        public static void WriteArrayOfItems<T>(this Stream stream, IEnumerable<T> src, int protocolVersion, int packetVersion) 
            where T:IServiceBusTcpContract
        {
            var list = src.AsReadOnlyList();
            stream.WriteInt(list.Count);

            foreach (var item in list)
                item.Serialize(stream, protocolVersion, packetVersion);
        }
        
        public static async Task<IReadOnlyList<T>> ReadArrayOfItemsAsync<T>(this ITcpDataReader reader,  int protocolVersion, int packetVersion, CancellationToken ct) 
            where T:IServiceBusTcpContract, new()
        {
            var len = await reader.ReadIntAsync(ct);

            var result = new List<T>();

            for (var i = 0; i < len; i++)
            {
                var itm = new T();
                await itm.DeserializeAsync(reader, protocolVersion, packetVersion, ct);
                result.Add(itm);
            }

            return result;
        }

        public static void WriteListOfByteArray(this Stream stream, in IReadOnlyList<byte[]> src)
        {
            stream.WriteInt(src.Count);

            foreach (var itm in src)
            {
                stream.WriteByteArray(itm);
            }
        }

        public static async ValueTask<IReadOnlyList<byte[]>> ReadListOfByteArrayAsync(this ITcpDataReader reader, CancellationToken ct)
        {

            var dataLen = await reader.ReadIntAsync(ct);

            var result = new List<byte[]>();
            for (var i = 0; i < dataLen; i++)
            {
                var data = await reader.ReadByteArrayAsync(ct);
                result.Add(data);
            }

            return result;
        }

        public static void SerializeMessagesIntervals(this Stream stream, IReadOnlyList<IQueueIndexRange> ranges, int protocolVersion, int packetVersion)
        {
            stream.WriteInt(ranges.Count);
            foreach (var indexRange in ranges)
                QueueIndexRangeTcpContract.Create(indexRange).Serialize(stream, protocolVersion, packetVersion);
        }

        public static async ValueTask<IReadOnlyList<IQueueIndexRange>> DeserializeMessagesIntervals(this ITcpDataReader dataReader, CancellationToken ct, int protocolVersion, int packetVersion)
        {
            
            var dataLen = await dataReader.ReadIntAsync(ct);
            
            var result = new List<IQueueIndexRange>(dataLen);

            for (var i = 0; i > dataLen; i++)
            {
                var queueIndex = new QueueIndexRangeTcpContract();
                await queueIndex.DeserializeAsync(dataReader, protocolVersion, packetVersion, ct);
                result.Add(queueIndex);
            }

            return result;
        }

        public static void SerializeList<T>(this Stream stream, IReadOnlyList<T> data, Action<T> serializeItem)
        {
            var len = data.Count;
            stream.WriteInt(len);

            foreach (var itm in data)
            {
                serializeItem(itm);
            }
        }
        
        public static void SerializeListWithCounterAsByte<T>(this Stream stream, IReadOnlyList<T> data, Action<T> serializeItem)
        {
            var len = (byte)data.Count;
            stream.WriteByte(len);

            foreach (var itm in data)
            {
                serializeItem(itm);
            }
        }


        public static async ValueTask<IReadOnlyList<T>> DeserializeList<T>(this ITcpDataReader dataReader, Func<ValueTask<T>> deserializeItemAsync, CancellationToken t)
        {
            var len = await dataReader.ReadIntAsync(t);

            var result = new T[len];
            for (var i = 0; i < len; i++)
            {
                result[i] = await deserializeItemAsync();
            }

            return result;
        }
        
        public static async ValueTask<IReadOnlyList<T>> DeserializeListWithCounterAsByte<T>(this ITcpDataReader dataReader, Func<ValueTask<T>> deserializeItemAsync, CancellationToken t)
        {
            var len = await dataReader.ReadAndCommitByteAsync(t);

            var result = new T[len];
            for (var i = 0; i < len; i++)
            {
                result[i] = await deserializeItemAsync();
            }

            return result;
        }
        

        /*
        public static void SerializePayloadWithMetaData(this Stream stream, 
            IReadOnlyList<(byte[] payload, IReadOnlyList<KeyValuePair<string, string>> metadata)> data)
        {
            foreach (var itm in data)
            {
                stream.WriteByteArray(payload);

                var metaDataLen = (byte)metadata.Count;
                
                stream.WriteByte(metaDataLen);

                foreach (var (key, value) in metadata)
                {
                    stream.WritePascalString(key);
                    stream.WritePascalString(value);
                }
            }

        }
        */
        
    }
}
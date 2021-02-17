using System;
using System.Collections.Generic;

namespace MyServiceBus.TcpContracts
{
    
    
    public enum CommandType
    {
        Ping, Pong, Greeting, Publish, PublishResponse, 
        Subscribe, SubscribeResponse, NewMessage, NewMessageConfirmation, CreateTopicIfNotExists, 
        MessagesConfirmation, PacketVersions, Reject, MessagesConfirmationAsFail
    }
    
    public static class DataContractsMapper
    {
        private static readonly Dictionary<CommandType, Func<IServiceBusTcpContract>> CommandToContractMapper
            = new Dictionary<CommandType, Func<IServiceBusTcpContract>>
            {
                [CommandType.Ping] = () => PingContract.Instance,
                [CommandType.Pong] = () => PongContract.Instance,
                [CommandType.Greeting] = () => new GreetingContract(),
                [CommandType.Publish] = () => new PublishContract(),
                [CommandType.PublishResponse] = () => new PublishResponseContract(),
                [CommandType.Subscribe] = () => new SubscribeContract(),
                [CommandType.SubscribeResponse] = () => new SubscribeResponseContract(),
                [CommandType.NewMessage] = () => new NewMessageContract(),
                [CommandType.NewMessageConfirmation] = () => new NewMessageConfirmationContract(),
                [CommandType.CreateTopicIfNotExists] = () => new CreateTopicIfNotExistsContract(),
                [CommandType.MessagesConfirmation] = () => new MessagesConfirmationContract(),
                [CommandType.MessagesConfirmationAsFail] = () => new MessagesConfirmationAsFailContract(),
                [CommandType.PacketVersions] = ()=>new PacketVersionsContract(),
                [CommandType.Reject] = ()=> new RejectConnectionContract()
            };
        
        public static readonly Dictionary<Type, byte> TypeToCommandType =
            new Dictionary<Type, byte>();

        static DataContractsMapper()
        {
            foreach (var itm in CommandToContractMapper)
            {
                TypeToCommandType.Add(itm.Value().GetType(), (byte)itm.Key);
            }
        }


        public static IServiceBusTcpContract ResolveDataContact(int commandTypeAsByte)
        {
            var commandType = (CommandType) commandTypeAsByte;
            
            if (CommandToContractMapper.ContainsKey(commandType))
                return CommandToContractMapper[commandType]();
            
            throw new Exception("Not Supported command type: "+commandTypeAsByte);
        }

        public static byte ResolveCommandType(IServiceBusTcpContract contract)
        {
            var type = contract.GetType();

            if (TypeToCommandType.ContainsKey(type))
                return TypeToCommandType[type];
            
            throw new Exception("Unknown contract type: "+type);

        }
    }
    
}
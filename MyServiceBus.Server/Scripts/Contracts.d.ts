
interface ITopicSignalRContract{
  id: string,
  pages: IPageModel[],
}


interface IInitSignalRContract{
  version: string;
}

interface ITopicQueueSignalRContract{
  id: string,
  connections: number,
  size:number,
  queueType: number,
  leased: number,
  ready: IQueueIndex[]
}

interface ISubscriberSignalrRContract{
  topicId :string,
  queueId:string,
  leased: IQueueIndex[],
  light: number
}

interface ITopic{
  id:string;
  light:number;
}

interface IConnectionSignalRContract{
  id: string;
  name: string;
  ip:string;
  topics: ITopic[];
  queues: ISubscriberSignalrRContract[],
  connected: string,
  recv:string,
  readBytes:number,
  sentBytes:number,
  deliveryEventsPerSecond:number,
  protocolVersion:number
}


interface IPersistInfo {
  id: string;
  size: number;
}


interface IUnknownConnection {
  id: number;
  ip: number;
  connectedTimeStamp: string;
  sentBytes: number;
  receivedBytes: number;
  sentTimeStamp: string;
  receiveTimeStamp: string;
  lastSendDuration : string;
}


interface IConnectionQueueInfo{
  id: string,
  leased: IQueueIndex[]
}

interface IConnection extends IUnknownConnection {
  name: string;
  publishPacketsPerSecond: number;
  deliveryPacketsPerSecond: number;
  protocolVersion: number;
  topics: string[];
  queues: IConnectionQueueInfo[];
}

interface IQueueIndex {
  from: number;
  to: number;
}

interface ITopicMetricsSignalRContract{
  id:string,
  msgPerSec:number,
  reqPerSec:number,
  pages: IPageModel[],
  queues: ITopicQueueSignalRContract[]
}


interface IPageModel{
  label: string,
  percent: number
}

interface ILogItem{
  date: string,
  connection: number,
  name: number
  ip: number;
  msg: number;
  
}
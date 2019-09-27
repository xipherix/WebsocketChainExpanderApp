using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChainExpander.Models.Data;
using ChainExpander.Models.Enum;
using ChainExpander.Models.Message;
using WebsocketAdapter;

namespace ChainExpander
{
    internal class WebsocketMarketDataManager
    {
        private readonly WebsocketConnectionClient _websocket;

        public WebsocketMarketDataManager(WebsocketConnectionClient websocket)
        {
            _websocket = websocket ?? throw new ArgumentNullException(nameof(websocket));
        }

        public bool Verbose { get; set; } = false;

        internal void Out(string message,bool overrideVerbose=false)
        {
            if(overrideVerbose || Verbose)
                Console.WriteLine(message);
        }
        internal async Task SendLogin(string username, string position, string appID = "256", int streamID = 1)
        {
            
            var loginReq = new ReqMessage
            {
                 ID = streamID, Domain = DomainEnum.Login,
                 MsgType = MessageTypeEnum.Request,
                 Key = new MessageKey()
            };
            loginReq.Key.Elements = new Dictionary<string, object>{{"ApplicationId", appID}, {"Position", position}};
            loginReq.Key.Name = new List<string> {username};
            Out(loginReq.ToJson());
            await ClientWebSocketUtils.SendTextMessage(_websocket.WebSocket, loginReq.ToJson());
           
        }

        internal async Task SendMarketPriceRequest(string itemList, int streamId, bool streamingFlag = true,List<string> fieldList = null)
        {
            var marketPriceReq = new ReqMessage
            {
                ID = streamId,
                Domain = DomainEnum.MarketPrice,
                Streaming = streamingFlag,
                Key = new MessageKey
                {
                    Name = itemList.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries).ToList(),
                    NameType = NameTypeEnum.Ric
                }
            };
            if (fieldList != null)
                marketPriceReq.View = fieldList;
            Out(marketPriceReq.ToJson());
            await ClientWebSocketUtils.SendTextMessage(_websocket.WebSocket, marketPriceReq.ToJson());
        }

        internal async Task SendCloseMessage(int streamId, DomainEnum domain = DomainEnum.MarketPrice)
        {
            var closeMsg = new CloseMessage {ID = streamId, Domain = domain, MsgType = MessageTypeEnum.Close};
            Out(closeMsg.ToJson());
            await ClientWebSocketUtils.SendTextMessage(_websocket.WebSocket, closeMsg.ToJson());
        }

        // True = Ping
        // False = Pong
        internal async Task SendPingPong(bool sendPing)
        {
            var pingMsg = new PingPongMessage {Type = sendPing ? MessageTypeEnum.Ping : MessageTypeEnum.Pong};
            await ClientWebSocketUtils.SendTextMessage(_websocket.WebSocket, pingMsg.ToJson());
            Out(pingMsg.ToJson());
        }
    }
}
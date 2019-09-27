using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ChainExpander.Events;
using ChainExpander.Models.Data;
using ChainExpander.Models.Enum;
using ChainExpander.Models.Message;
using ChainExpander.Utils;
using ChainExpander.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebsocketAdapter;
using WebsocketAdapter.Events;

namespace ChainExpander
{
    public class ChainExpander
    {
        private readonly WebsocketMarketDataManager _websocketMarketDataMgr;
        internal int _streamId = 5;
        private bool _isSequentialMode = true;
        private bool _useHex = false;
        private string _subRic = string.Empty;
        private string _indexRic = string.Empty;
        private static int _startIndex = 11;
        private static int _stopIndex = 50;
        private static int _batchSize = 1500;
        private int _processRound = 1;
        private int _loginStreamId = 1;

        private readonly ChainData _chainData=new ChainData();
        private readonly SortedDictionary<string,ChainRequestStatusEnum> _chainList=new SortedDictionary<string, ChainRequestStatusEnum>(new ChainComparer());
        public bool IsLoggedIn { get; set; }

        public bool Verbose { get; set; } = false;

        public bool PrintJson {
            set => _websocketMarketDataMgr.Verbose = value;
        }
        public bool IsOperationCompleted { get; set; } = false;

        internal void Out(string message,bool overrideVerbose=false)
        {
            if(overrideVerbose|| Verbose)
                Console.WriteLine(message);
        }
        public int MaxBatchSize { get; set; } = 0;

        public int OverrideStopIndexValue
        {
            get => _stopIndex;
            set => _stopIndex = value;
        }
        public ChainExpander(WebsocketConnectionClient websocketAdapter)
        {
            var websocketAdapter1 = websocketAdapter ?? throw new ArgumentNullException(nameof(websocketAdapter));
            _websocketMarketDataMgr = new WebsocketMarketDataManager(websocketAdapter);
            websocketAdapter1.MessageEvent += this.ProcessWebsocketMessage;
        }

        public async Task<bool> CloseLogin()
        {
            try
            {
                await _websocketMarketDataMgr.SendCloseMessage(_loginStreamId, DomainEnum.Login);

            }
            catch (Exception ex)
            {
                RaiseErrorEvent(DateTime.Now,ex.Message);
                return false;
            }

            return true;
        }
        public async Task<bool> SendLogin(string username, string position, string appID = "256", int streamID = 1)
        {
            try
            {
                _loginStreamId = streamID;
                await _websocketMarketDataMgr.SendLogin(username, position, appID, streamID);
            }
            catch (Exception ex)
            {
                RaiseErrorEvent(DateTime.Now,ex.Message);
                return false;
            }

            return true;
        }

        private List<string> GenItemList(int startIndex, int stopIndex,string subRic,bool useHex)
        {
            
          
            var itemList = new List<string>();

            for (var i = startIndex; i <= stopIndex; i++)
            {
                itemList.Add(!useHex ? $"{i}#{subRic}" : $"{i:X}#{subRic}");
            }
            
   
            return itemList;
        }
        public async Task RunExtraction(string chainRic,bool sequentialMode=true)
        {
           
            _isSequentialMode = sequentialMode;
            IsOperationCompleted = false;
            _startIndex = 0;
            _stopIndex = 50;

            _chainData.Clear();
            _chainList.Clear();
            _chainData.StartChainRic = chainRic;
            
            if (_isSequentialMode)
            {
                _chainList.Add(chainRic, ChainRequestStatusEnum.Wait);
                await _websocketMarketDataMgr.SendMarketPriceRequest(chainRic, _streamId, false);
            }
            else
            {
                //Method 1
                
                var tempStr = chainRic.Split('#').ToList();
                _subRic = tempStr.Count > 1 ? tempStr[1] : chainRic;
                _indexRic = tempStr.Count > 1 ? "0" : string.Empty;
                Out($"Start retrieving {_indexRic}{(string.IsNullOrEmpty(_indexRic) ? string.Empty:"#")}{_subRic}",true);
                _chainList.Add(chainRic,ChainRequestStatusEnum.Wait);
                for (var i = 0; i <=9; i++)
                {
                    if(!_chainList.ContainsKey($"{i}#{_subRic}"))
                         _chainList.Add($"{i}#{_subRic}", ChainRequestStatusEnum.Wait);
                }
                _chainList.Add($"10#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"A#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"60#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"3C#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"95#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"5F#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"1F4#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"500#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"3E8#{_subRic}", ChainRequestStatusEnum.Wait);
                _chainList.Add($"1000#{_subRic}", ChainRequestStatusEnum.Wait);
                var batchList = new StringBuilder();
                foreach (var item in _chainList.Keys)
                {
                    batchList.Append(item);
                    if (item != _chainList.Keys.Last())
                        batchList.Append(",");
                    
                }
                
                _processRound = 1;
                await _websocketMarketDataMgr.SendMarketPriceRequest(batchList.ToString(), _streamId, false);
               

                
            }
        }
        private bool AllReceived(SortedDictionary<string, ChainRequestStatusEnum> subscriptionList)
        {
            return !(subscriptionList.Where(x => x.Value == ChainRequestStatusEnum.Wait).Select(y => y.Key)).Any();
        }
    
        internal void ProcessWebsocketMessage(object sender, MessageEventArgs e)
        {
            if (_websocketMarketDataMgr.Verbose)
            {
                var msg = new StringBuilder();
                msg.Append($"Message Received:{MarketDataUtils.TimeStampToString(e.TimeStamp)}\n");
                msg.Append("================ Original JSON Data =================\n");
                var messageJson = JArray.Parse(Encoding.ASCII.GetString(e.Buffer));
                var prettyJson = JsonConvert.SerializeObject(messageJson, Formatting.Indented);
                msg.Append($"Data:\n{prettyJson}\n");
                msg.Append("=====================================================");
                Out(msg.ToString());
                
            }

            try
            {
                var data = Encoding.UTF8.GetString(e.Buffer);
                var messages = JArray.Parse(data);
                foreach (var jsonData in messages.Children())
                    if (jsonData["Type"] != null)
                    {
                        var msgType = (MessageTypeEnum) Enum.Parse(typeof(MessageTypeEnum), (string) jsonData["Type"],
                            true);
                        var rdmDomain = DomainEnum.MarketPrice;
                        if (jsonData["Domain"] != null) 
                            rdmDomain = (DomainEnum) Enum.Parse(typeof(DomainEnum), (string) jsonData["Domain"],
                            true);

                        switch (msgType)
                        {
                            case MessageTypeEnum.Error:
                                // Process Error
                                ProcessError(jsonData);
                                break;
                            case MessageTypeEnum.Ping:
                            {
                                // Send Pong back
                                Out("Ping Received");
                                _websocketMarketDataMgr.SendPingPong(false).GetAwaiter().GetResult();
                                Out("Pong Sent");
                            }
                                break;
                            default:
                                ProcessMessage(jsonData, msgType, rdmDomain);
                                break;
                        }
                    }
            }
            catch (Exception ex)
            {
                var msg=$"Error ProcessWebsocketMessage() {ex.Message}\n{ex.StackTrace}";
                RaiseErrorEvent(DateTime.Now,msg);
            }
        }

        private void ProcessMessage(JToken jsonData, MessageTypeEnum msgType, DomainEnum domain)
        {
            switch (domain)
            {
                case DomainEnum.Login:
                    ProcessLogin(jsonData, msgType);
                    break;
                case DomainEnum.MarketPrice:
                    ProcessChainResponseMessage(jsonData, msgType);
                    break;
                default:
                    Console.WriteLine("Unsupported Domain");
                    RaiseErrorEvent(DateTime.Now,$"Received response message for unhandled domain model");
                    break;
            }
        }

        private void ProcessError(JToken jsonData)
        {
            var message = jsonData.ToObject<ErrorMessage>();
            var errorMsg = new StringBuilder();

            errorMsg.Append($"Websocket Connection Error ID:{message.ID}\n");
            errorMsg.Append($"Message:{message.Text}\n");
            errorMsg.Append($"DebugInfo:\n{message.DebugInfo.ToJson()}\n");
            RaiseErrorEvent(DateTime.Now,errorMsg.ToString());
        }

      

        private void ProcessLogin(JToken jsonData, MessageTypeEnum msgType)
        {   
            switch (msgType)
            {
                case MessageTypeEnum.Refresh:
                {
                    var message = jsonData.ToObject<RefreshMessage>();
                    if (message.State.Stream == StreamStateEnum.Open && message.State.Data != DataStateEnum.Suspect)
                    {
                            IsLoggedIn = true;
                    }
                    RaiseLoginMessageEvent(DateTime.Now,message);
                   
                }
                break;
                case MessageTypeEnum.Status:
                    {
                        var message = jsonData.ToObject<StatusMessage>();
                        if (message.State.Stream == StreamStateEnum.Closed || message.State.Stream == StreamStateEnum.ClosedRecover)
                            IsLoggedIn = false;
                        
                        RaiseLoginMessageEvent(DateTime.Now, message);
                    }
                    break;
                case MessageTypeEnum.Update:
                    RaiseLoginMessageEvent(DateTime.Now, jsonData.ToObject<UpdateMessage>());
                    break;
               
            }
            
        }

        private void ProcessChainResponseMessage(JToken jsonData, MessageTypeEnum msgType)
        {
            
            switch (msgType)
            {
                case MessageTypeEnum.Refresh:
                    {
                        var message = jsonData.ToObject<MarketPriceRefreshMessage>();
                        message.MsgType = MessageTypeEnum.Refresh;
                        var id = message.ID;
                        var ricName = MarketDataUtils.StringListToString(message.Key.Name);
                        
                       _chainList[ricName] = ChainRequestStatusEnum.Received;
                       
                       
                        if (message.Fields != null)
                        {
                           var templateEnum=ChainUtils.GetChainTemplate(message.Fields);
                           string nextric=string.Empty, prevric=String.Empty;
                           IChain fieldData = default;
                          
                            switch (templateEnum)
                            {
                               case ChainTemplateEnum.None:
                               {
                                   if (Verbose)
                                   {
                                       Out($"{ricName} is not a valid Chain.\n");
                                       Out($"===========================================\n");
                                            foreach (var field in message.Fields)
                                       {
                                           Out($"{field.Key}:{field.Value}");
                                       }
                                       Out($"===========================================\n");
                                   }
                                   _chainList.Clear();
                                   _chainData.Clear();
                                   GenerateResult(_chainData.ChainList, $"Extraction Failed because {ricName} is not a valid Chain RIC.", false);                         
                                   return;
                               }
                               case ChainTemplateEnum.LinkEnum:
                               {
                                   fieldData = (ChainLink)message.Fields.ToObject<ChainLink>();
                                   fieldData.StreamId = id.GetValueOrDefault();
                                   _chainData.Add(ricName, fieldData);
                                   _chainData.ChainList[ricName].TemplateType = templateEnum;
                                   nextric = (fieldData as ChainLink).NEXT_LR;
                                   prevric = (fieldData as ChainLink).PREV_LR;
                               }
                               break;
                               case ChainTemplateEnum.LongLinkEnum:
                               {
                                   fieldData = message.Fields.ToObject<ChainLongLink>() as ChainLongLink;
                                   fieldData.StreamId = id.GetValueOrDefault();
                                   _chainData.Add(ricName, fieldData);
                                   _chainData.ChainList[ricName].TemplateType = templateEnum;
                                   nextric = (fieldData as ChainLongLink).LONGNEXTLR;
                                   prevric = (fieldData as ChainLongLink).LONGPREVLR;

                               }
                               break;
                               case ChainTemplateEnum.BrLinkEnum:
                               {
                                   fieldData = (ChainBrLink)message.Fields.ToObject <ChainBrLink>();
                                   fieldData.StreamId = id.GetValueOrDefault();
                                   _chainData.Add(ricName, fieldData);
                                   _chainData.ChainList[ricName].TemplateType = templateEnum;
                                   nextric = (fieldData as ChainBrLink).BR_NEXTLR;
                                   prevric = (fieldData as ChainBrLink).BR_PREVLR;
                               }
                               break;

                               default:
                               {
                                   RaiseErrorEvent(DateTime.Now,"Found Unexpected Template Enum {templateEnum}");
                               }
                               break;
                            }
                            var msgBuilder=new StringBuilder();
                         
                            msgBuilder.Append(
                                $"Process Refresh message for RIC Name: {ricName} [{(message.Fields.ContainsKey("DSPLY_NAME") ? message.Fields["DSPLY_NAME"] : "DSPLY_NAME")}] Previous RIC is {(string.IsNullOrEmpty(prevric)?"Empty":prevric)}");
                            msgBuilder.Append(string.IsNullOrEmpty(nextric) ? $" <= Final RIC" : $" Next RIC is {(string.IsNullOrEmpty(nextric) ? "Empty" : nextric)}");
                            Out(msgBuilder.ToString());
                            if (_isSequentialMode)
                            {
                                if (!string.IsNullOrEmpty(nextric) && !fieldData.IsLast &&
                                    !_chainList.ContainsKey(nextric))
                                {
                                    _websocketMarketDataMgr.SendMarketPriceRequest(nextric, _streamId++, false)
                                        .GetAwaiter().GetResult();
                                }

                                if (!string.IsNullOrEmpty(prevric) && !fieldData.IsFirst &&
                                    !_chainList.ContainsKey(prevric))
                                {
                                    _websocketMarketDataMgr.SendMarketPriceRequest(prevric, _streamId++, false)
                                        .GetAwaiter().GetResult();
                                }

                            } 

                        }
                    }
                    break;
                case MessageTypeEnum.Update:
                    {
                        // We do not expect to received update message because we use snapshot request.
                        var message = jsonData.ToObject<MarketPriceUpdateMessage>();
                        message.MsgType = MessageTypeEnum.Update;
                        var ricName = MarketDataUtils.StringListToString(message.Key.Name);
                        Out($"Unexpected update message received for Ric Name:{ricName}");
                    
                    }
                    break;
                case MessageTypeEnum.Status:
                    { 
                        var message = jsonData.ToObject<StatusMessage>();
                        var ricName = message.Key==null?string.Empty:MarketDataUtils.StringListToString(message.Key.Name);

                        if (string.IsNullOrEmpty(ricName))
                            return;
                        // Console.WriteLine($"Status message Ric Name:{ricName} {message.State.Text}");
                        //Check if item stream is closed or closed recover and resend item request again if Login still open.

                        if(_isSequentialMode)
                            RaiseExtractionStatusEvent(DateTime.Now, message);

                        if (message.State.Stream == StreamStateEnum.Closed ||
                            message.State.Stream == StreamStateEnum.ClosedRecover)
                        {
                            if (_chainList.ContainsKey(ricName)) _chainList[ricName] = ChainRequestStatusEnum.NotFound;
                            if (ricName == _chainData.StartChainRic)
                            {

                                GenerateResult(_chainData.ChainList, $"Chain Extraction failed {message.State.Text}",false);
                                return;
                            }
                            else
                            {
                                if (_isSequentialMode)
                                {
                                    GenerateResult(_chainData.ChainList,$"Chain Extraction stop because it can't retrieve next ric {ricName}. Code:{message.State.Code}",false);
                                    return;
                                }
                                
                                
                            }
                        }
                    }
                    break;
            }

            // Verify if extraction is completed
            if ( _chainData.Count > 0  && AllReceived(_chainList))
            {
                if ((_chainData.ChainList.Any(item=>item.Value.IsFirst)) && _chainData.ChainList.Any(item=>item.Value.IsLast))
                {
                    GenerateResult(_chainData.ChainList, "Extraction completed successful.");
                    return;
                }
               

                if (!_isSequentialMode) 
                {
                    // First batch received and used to evaluate if the chain use hex or dec and what should be proper incementValue
                    if (_processRound == 1)
                    {
                        if (MaxBatchSize <= 0 && _chainList!=null)
                        {
                            if (_chainList[$"A#{_subRic}"] == ChainRequestStatusEnum.Received)
                                _useHex = true;

                            if (_chainList[$"A#{_subRic}"] == ChainRequestStatusEnum.NotFound &&
                                _chainList[$"10#{_subRic}"] == ChainRequestStatusEnum.NotFound)
                                _batchSize = 10;
                            else
                            if (_chainList[$"60#{_subRic}"] == ChainRequestStatusEnum.NotFound &&
                                _chainList[$"3C#{_subRic}"] == ChainRequestStatusEnum.NotFound)
                                _batchSize = 50;
                            else
                            if (_chainList[$"95#{_subRic}"] == ChainRequestStatusEnum.NotFound &&
                                _chainList[$"5F#{_subRic}"] == ChainRequestStatusEnum.NotFound)
                                _batchSize = 90;
                            else
                            if (_chainList[$"1F4#{_subRic}"] == ChainRequestStatusEnum.NotFound &&
                                _chainList[$"500#{_subRic}"] == ChainRequestStatusEnum.NotFound)
                                _batchSize = 500;
                            else
                            if (_chainList[$"3E8#{_subRic}"] == ChainRequestStatusEnum.NotFound &&
                                _chainList[$"1000#{_subRic}"] == ChainRequestStatusEnum.NotFound)
                                _batchSize = 1000;

                            _stopIndex = _batchSize;
                        }
                        else
                        {
                            _batchSize = MaxBatchSize;
                        }

                        _processRound++;
                    }
                    else
                    {
                        var nextRicinTheBatch = GetNextRicInCurrentBatch();
                           
                        if (_chainList.ContainsKey(nextRicinTheBatch) && !string.IsNullOrEmpty(nextRicinTheBatch) && _chainList[nextRicinTheBatch] == ChainRequestStatusEnum.NotFound)
                        {                      
                            GenerateResult(_chainData.ChainList, $"\n\nChain Extraction stop because application unable to get data for Next RIC {nextRicinTheBatch} and it return status : NotFound",false);
                            return;
                        }
                    }
                   

                    var itemList = GenItemList(_startIndex, _stopIndex, _subRic, _useHex);

                    var batchList = new StringBuilder();
                    foreach (var item in itemList)
                    {
                        if (_chainList.ContainsKey(item)) continue;

                        _chainList.Add(item, ChainRequestStatusEnum.Wait);
                        batchList.Append(item);
                        if (item != itemList.Last())
                            batchList.Append(",");
                        
                        
                    }

                    _websocketMarketDataMgr.SendMarketPriceRequest(batchList.ToString(), _streamId++, false).GetAwaiter().GetResult();
                    _startIndex = _stopIndex + 1;
                    _stopIndex += _batchSize;
                }
            }
           
        }

        private string GetNextRicInCurrentBatch()
        {
            var receiveList = _chainList.Where(y => y.Value == ChainRequestStatusEnum.Received);
            if (!receiveList.Any()) return string.Empty;
            if (!_chainData.ChainList.ContainsKey(receiveList.Last().Key)) return null;
            switch (_chainData.ChainList[receiveList.Last().Key].TemplateType)
            {
                case ChainTemplateEnum.None:
                    throw new InvalidOperationException();
                case ChainTemplateEnum.LinkEnum:
                    return (_chainData.ChainList[receiveList.Last().Key] as ChainLink).NEXT_LR;
                case ChainTemplateEnum.LongLinkEnum:
                    return (_chainData.ChainList[receiveList.Last().Key] as ChainLongLink).LONGNEXTLR;
                case ChainTemplateEnum.BrLinkEnum:
                    return ((ChainBrLink)_chainData.ChainList[receiveList.Last().Key]).BR_NEXTLR;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
   
        private void GenerateResult(SortedDictionary<string,IChain> data,string message,bool isSuccess=true)
        {
            var itemList = new List<string>();
            try
            {

                foreach (var constituent in data.Values)
                {
                    itemList.AddRange(constituent.Constituents);
                }

                var chainList = _chainList.Where(x => x.Value == ChainRequestStatusEnum.Received).Select(y => y.Key).ToList();
                IsOperationCompleted = true;
                RaiseExtractionCompleteEvent(DateTime.Now, itemList,chainList, isSuccess,
                    message);
            }
            catch (Exception ex)
            {
                RaiseErrorEvent(DateTime.Now, $"{ex.Message} {ex.StackTrace}");
            }
   
        }
        protected void RaiseExtractionCompleteEvent(DateTime timestamp,IEnumerable<string> items,IEnumerable<string> chains,bool isSuccess,string message)
        {
            var messageCallback = new ChainMessageEventArgs() {ChainList = chains,ItemList = items, TimeStamp = timestamp, IsSuccess=isSuccess, Message=message};
            OnMessage(messageCallback);
        }
        protected void RaiseExtractionStatusEvent(DateTime timestamp,StatusMessage status)
        {
            var statusCallback = new ChainStatusMsgEventArgs() { Status=status, TimeStamp = timestamp };
            OnStatus(statusCallback);
        }
        protected void RaiseLoginMessageEvent(DateTime timestamp, IMessage message)
        {
            var messageCallback = new LoginMessageEventArgs() { Message= message, TimeStamp = timestamp };
            OnLoginMessage(messageCallback);
        }

        protected void RaiseErrorEvent(DateTime timestamp, string errorMsg)
        {
            var errorCallback = new ChainErrorEventArgs() {TimeStamp = timestamp, ErrorMessage = errorMsg};
            OnError(errorCallback);
        }
        protected virtual void OnMessage(ChainMessageEventArgs e)
        {
            var handler = OnExtractionCompleteEvent;
            handler?.Invoke(this, e);
        }

        protected virtual void OnError(ChainErrorEventArgs e)
        {
            var handler = OnExtractionErrorEvent;
            handler?.Invoke(this, e);
        }
        protected virtual void OnStatus(ChainStatusMsgEventArgs e)
        {
            var handler = OnExtractionStatusEvent;
            handler?.Invoke(this, e);
        }
        protected virtual void OnLoginMessage(LoginMessageEventArgs e)
        {
            var handler = LoginMessageEvent;
            handler?.Invoke(this, e);
        }
        public event EventHandler<ChainStatusMsgEventArgs> OnExtractionStatusEvent;
        public event EventHandler<ChainMessageEventArgs> OnExtractionCompleteEvent;
        public event EventHandler<LoginMessageEventArgs> LoginMessageEvent;
        public event EventHandler<ChainErrorEventArgs> OnExtractionErrorEvent;
    }
}
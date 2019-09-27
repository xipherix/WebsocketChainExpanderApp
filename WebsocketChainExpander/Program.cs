using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChainExpander.Models.Enum;
using ChainExpander.Models.Message;
using ChainExpander.Utils;
using WebsocketAdapter;

namespace WebsocketChainConsumerApp
{
    internal partial class Program
    {
        private static  Uri WebsocketUri = new Uri("ws://172.20.33.21:15000/WebSocket");
        private static readonly string Clientname = "apitest1";
        private static  string DACS_User = "apitest";
        private static  string AppId = "256";
        private static  string Login_Position = "127.0.0.1/net";
        //Test Chain:>> "0#TWINTL=TWO"<= Has Problem no final ric. 0#CL:";//"0#UNIVERSE.PK"; <= Long chain //0#EURCURVES "0#UNIVERSE.NB";//"0#.DJI" "0#UNIVERSE.NB";//"0#.CACT";//"0#.SETI";//".AV.O";//"15#.SETI";"0#.CACT//.AV.CA)
        private static  string chainRic = "0#UNIVERSE.PK";
        private static readonly Stopwatch stopwatch = new Stopwatch();
        private static StringBuilder consoleOutputMsg=new StringBuilder();
        private static List<string> AvailableChainList = new List<string>();
        private static List<string> CompleteItemList = new List<string>();
        private static List<string> SubscribedChainList = new List<string>();
        private static bool useSequential = false;
        private static void Main(string[] args)
        {
            var appConfig = new Config();
            if (!ShowsConfigCommand(args, ref appConfig))
                return;

            WebsocketUri = new Uri(appConfig.WsServer);
            DACS_User = appConfig.DacsUserName;
            AppId = appConfig.AppId;
            Login_Position = appConfig.DacsPosition;
            chainRic = appConfig.ChainRic;
            useSequential = appConfig.SequentialMode;
            if (appConfig.ChainRic.Contains(','))
            {
                Out($"{appConfig.ChainRic} is invalid RIC it contains ','");
                return;
            }

            if (string.IsNullOrWhiteSpace(appConfig.ChainRic))
            {
                Out($"{appConfig.ChainRic} contains whitespace");
                return;
            }

            stopwatch.Reset();
     
            Out("Starting Chain Extractor application. Press Ctrl+C to cancel operation.\n");
            var websocket = new WebsocketConnectionClient(Clientname, WebsocketUri, "tr_json2")
            {
                Cts = new CancellationTokenSource()
            };

            var chainManager = new ChainExpander.ChainExpander(websocket)
            {
                OverrideStopIndexValue = appConfig.StopIndex,
                MaxBatchSize = appConfig.MaxBatchSize,
                Verbose = appConfig.Verbose,
                PrintJson = appConfig.PrintJson
            };

            chainManager.OnExtractionCompleteEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                if (!e.IsSuccess)
                    consoleOutputMsg.Append($"{e.Message}\n\n");

                SubscribedChainList.AddRange(e.ChainList);

                var tempList = e.ItemList.Where(ChainUtils.IsChainRIC).ToList();
                AvailableChainList.AddRange(tempList);
                if (tempList.Any())
                {
                    consoleOutputMsg.Append($"\nReceived {tempList.Count()} underlying Chain RICs from the list\n\n");
                    consoleOutputMsg.Append(string.Join("\n", e.ItemList.Where(ChainUtils.IsChainRIC)));
                    AvailableChainList.AddRange((tempList));
                }

                var tempItemList = e.ItemList.Where(item => !ChainUtils.IsChainRIC(item)).ToList();
                if(tempItemList.Any())
                     CompleteItemList.AddRange(tempItemList);
              

                if (AvailableChainList.Any())
                {
                    var firstItem = AvailableChainList.First();
                    AvailableChainList.Remove(firstItem);
                    chainManager.RunExtraction(firstItem, appConfig.SequentialRecursiveMode).GetAwaiter().GetResult();
 
                }
                else
                {
                    stopwatch.Stop();
                    consoleOutputMsg.Append($"\nOperation Completed in {stopwatch.ElapsedMilliseconds} MS.\n");
                    if (SubscribedChainList.Any())
                    {
                        consoleOutputMsg.Append(
                            $"\nBelow is a list of Chain RIC application has requested from the server\n\n");
                        consoleOutputMsg.Append($"{string.Join(",", SubscribedChainList)}\n\n");
                    }

                    if (CompleteItemList.Any())
                    {
                        consoleOutputMsg.Append(
                            $"\nReceived {CompleteItemList.Count()} constituents/instruments from the Chains\n\n");
                        consoleOutputMsg.Append(string.Join("\n",
                           CompleteItemList.Where(item => !ChainUtils.IsChainRIC(item)).OrderBy(ric=>ric)));
                    }

                    consoleOutputMsg.Append("\n");
                    Out(consoleOutputMsg.ToString(), true);
                    if (!string.IsNullOrEmpty(appConfig.OutputFilename))
                    {
                        if(SaveListToFile(appConfig.OutputFilename,
                            CompleteItemList.Where(item => !ChainUtils.IsChainRIC(item)).OrderBy(ric => ric).ToList()).GetAwaiter().GetResult())
                            Console.WriteLine($"Write RIC list to {appConfig.OutputFilename} completed.");
                    }
                    chainManager.CloseLogin().GetAwaiter().GetResult();
                    websocket.Stop = true;
                }
               

            };
            chainManager.OnExtractionErrorEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                consoleOutputMsg.Append("******************* Process Error Events **********************\n");
                consoleOutputMsg.Append($"TimeStamp:{e.TimeStamp}\n");
                consoleOutputMsg.Append($"{e.ErrorMessage}\n");
                consoleOutputMsg.Append("*********************************************************************\n");
                Out(consoleOutputMsg.ToString(),appConfig.Verbose);
          
            };
            chainManager.OnExtractionStatusEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                consoleOutputMsg.Append("******************* Process Market Data Status Events **********************\n");
                consoleOutputMsg.Append($"Received {e.Status.MsgType} {e.TimeStamp}\n");
                consoleOutputMsg.Append($"Stream State:{e.Status.State.Stream}\n");
                if (e.Status.Key != null)
                    consoleOutputMsg.Append($"Item Name:{e.Status.Key.Name.FirstOrDefault()}\n");
                consoleOutputMsg.Append($"Data State:{e.Status.State.Data}\n");
                consoleOutputMsg.Append($"State Code:{e.Status.State.Code}\n");
                consoleOutputMsg.Append($"Status Text:{e.Status.State.Text}\n");
                consoleOutputMsg.Append("*********************************************************************\n");
                Out(consoleOutputMsg.ToString(),appConfig.Verbose);
                
            };
            chainManager.LoginMessageEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                consoleOutputMsg.Append("******************* Process Login Message Events **********************\n");
                consoleOutputMsg.Append($"{e.TimeStamp}  received {e.Message.MsgType}\n");
                switch (e.Message.MsgType)
                {
                    case MessageTypeEnum.Refresh:
                        {
                            var message = (RefreshMessage)e.Message;
                            consoleOutputMsg.Append($"Login name:{message.Key.Name.FirstOrDefault()}\n");

                            consoleOutputMsg.Append(
                                    $"Login Refresh stream:{message.State.Stream} state:{message.State.Data} code:{message.State.Code} status text:{message.State.Text}\n");
                            consoleOutputMsg.Append("*********************************************************************\n");
                            consoleOutputMsg.Append("\n");
                            Out(consoleOutputMsg.ToString(), appConfig.Verbose);
                            if (message.State.Stream == StreamStateEnum.Open &&
                                message.State.Data != DataStateEnum.Suspect)
                            {
                                stopwatch.Start();
                                chainManager.RunExtraction(chainRic, useSequential).GetAwaiter().GetResult();

                            }
                        }
                        break;

                    case MessageTypeEnum.Status:
                        {
                            var message = (StatusMessage)e.Message;
                            consoleOutputMsg.Append($"Login name:{message.Key.Name.FirstOrDefault()}\n");
                            consoleOutputMsg.Append(
                                $"Login Status stream:{message.State.Stream} state:{message.State.Data} code:{message.State.Code} status text:{message.State.Text}\n");
                            consoleOutputMsg.Append("*********************************************************************\n");
                            consoleOutputMsg.Append("\n");
                            Out(consoleOutputMsg.ToString(), appConfig.Verbose);
                            if (message.State.Stream == StreamStateEnum.Closed ||
                                message.State.Stream == StreamStateEnum.ClosedRecover)
                            {
                                websocket.Stop = true;
                            }
                        }
                        break;
                }
               
            };
            websocket.ErrorEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                consoleOutputMsg.Append("******************* Process Websocket Error Events **********************\n");
                consoleOutputMsg.Append($"OnConnectionError {e.TimeStamp} {e.ClientWebSocketState} {e.ErrorDetails}\n");
                consoleOutputMsg.Append("*********************************************************************\n");
                Out(consoleOutputMsg.ToString(),true);
                websocket.Stop = true;

            };

            websocket.ConnectionEvent += (sender, e) =>
            {
                consoleOutputMsg.Clear();
                consoleOutputMsg.Append("******************* Process Websocket Connection Events **********************\n");
                consoleOutputMsg.Append($"OnConnection Event Received:{MarketDataUtils.TimeStampToString(e.TimeStamp)}\n");

                consoleOutputMsg.Append($"Connection State:{e.State}\n");
                consoleOutputMsg.Append($"Message:{e.StatusText}\n");
                if (e.State == WebSocketState.Open)
                {
                    chainManager.SendLogin(DACS_User, Login_Position, AppId, 1).GetAwaiter().GetResult();
                }
                consoleOutputMsg.Append("*********************************************************************\n");
                Out(consoleOutputMsg.ToString(),true);
            };
            Console.CancelKeyPress += (sender, e) =>
            {
                if (e.SpecialKey != ConsoleSpecialKey.ControlC) return;
                Out("Ctrl+C Pressed");
                chainManager.CloseLogin().GetAwaiter().GetResult();
                websocket.Stop = true;


            };
            websocket.Run().GetAwaiter().GetResult();
            while (!websocket.Stop) ;

            Out("Quit the app");

        }

        private static async Task<bool> SaveListToFile(string filepath,List<string> riclist)
        {
            try
            {
                using (var tw = new StreamWriter(filepath))
                {
                    await tw.WriteLineAsync(string.Join("\n", riclist));
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Writing RIC to file {filepath} error. {ex.Message}");
                return false;
            }

            return true;
        }
        private static void Out(string msg,bool verbose = true,bool writeToFile=false)
        {
            if(verbose)
                Console.WriteLine(msg);
        }


    }
}

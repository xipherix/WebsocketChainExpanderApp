
using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;
using CommandLine.Text;

namespace WebsocketChainConsumerApp
{
    
    internal partial class Program
    {
        private class Options
        {
            [Option('s',"server", SetName = "set1", Required = true, HelpText =
                "Use to specify a websocket server e.g. ws://172.20.33.24:15000/WebSocket")]
            public string WsServer { get; set; }


            [Option('u',"username", SetName = "set1", Required = true,
                HelpText =
                    "Specify DACS Username used on TREP Server")]
            public string DacsUsername { get; set; }

            [Option('i', "item", SetName = "set1", Required = true,
                HelpText =
                    "Specify a valid chain RIC name e.g. 0#FTSE, 0#SETI")]
            public string RicName { get; set; }


            [Option('p', "position", Default = "127.0.0.1/net", Required = false,
                HelpText =
                    "Specify DACS poistion in valid format e.g. 10.42.62.43/net")]

            public string DACSPosition { get; set; }

            [Option("appid", Default = "256", Required = false,
                HelpText =
                    "Application Id for DACS Authentication. Default is 256.")]

            public string AppId { get; set; }

            [Option("batchsize", Default = 0, Required = false, Hidden = true,
                HelpText = "Max batch size used by heuristic algorithm to generate a list of chain in batch request.\n" +
                           "Default is 0 which means unset. Note that it does not used by Sequential mode.")]

            public int MaxBatchSize { get; set; }

            [Option("stopindex", Default = 50, Required = false, Hidden = true,
                HelpText = "Override default stop index value used by heuristic algorithm.")]

            public int StopIndex { get; set; }

            [Option('o',"outputfile", Default = "", Required = false, 
                HelpText = "Specify a filename or absolute file path to write the RIC list extracted from Chain RIC.")]

            public string OutputFilename { get; set; }

            // Omitting long name, defaults to name of property, ie "--verbose"
            [Option(Default = false, HelpText = "Print additional logs to console output.")]
            public bool Verbose { get; set; }
            [Option(Default = false, HelpText = "Print Json message to console output.")]
            public bool PrintJson { get; set; }
            // Omitting long name, defaults to name of property, ie "--verbose"
            [Option("seq",Default = false, HelpText = "Use sequential mode to expand chain RIC one by one.")]

            public bool SequentialMode { get; set; }

            [Option("seqrecursive", Default = false, HelpText = "Use sequential mode to expanding Recursive RIC")]
            public bool SequentialRecursiveMode { get; set; }

            [Usage(ApplicationAlias = "WebsocketChainExpander")]

            public static IEnumerable<Example> Examples => new List<Example>
            {
               
                new Example($"Connecting to Websocket Server",
                new Options {WsServer = "ws://192.168.27.46:15000/WebSocket",DacsUsername = "user1",RicName = "0#.SETI"})
            };
        }

        public class Config
        {
            public string WsServer { get; set; }
            public string DacsUserName { get; set; }
            public string DacsPosition { get; set; }
            public string AppId { get; set; }
            public string ChainRic { get; set; } 
            public int MaxBatchSize { get; set; }
            public int StopIndex { get; set; }
            public bool Verbose { get; set; }
            public bool PrintJson { get; set; }
            public string OutputFilename { get; set; }
            public bool SequentialMode { get; set; }
            public bool SequentialRecursiveMode { get; set; }
        }

        private static int RunOptionsAndReturnExitCode(Options options, out Config config)
        {
            config = new Config
            {
                Verbose = options.Verbose,
                WsServer = options.WsServer,
                DacsUserName = options.DacsUsername,
                AppId = options.AppId,
                DacsPosition = options.DACSPosition,
                ChainRic = options.RicName,
                MaxBatchSize = options.MaxBatchSize,
                StopIndex = options.StopIndex,
                SequentialMode = options.SequentialMode,
                PrintJson = options.PrintJson,
                OutputFilename = options.OutputFilename,
                SequentialRecursiveMode=options.SequentialRecursiveMode
            };

            return 1;
        }
       
        /// <summary>Used to shows command line arguments required by the application with it's default values.</summary>
        public static bool ShowsConfigCommand(string[] arguments, ref Config commandConfig)
        {
            var config = new Config();
            var configResult = Parser.Default.ParseArguments<Options>(arguments)
                .WithParsed(opts => RunOptionsAndReturnExitCode(opts, out config));
            commandConfig = config;
            return configResult.Tag != ParserResultType.NotParsed;
        }
    }
}
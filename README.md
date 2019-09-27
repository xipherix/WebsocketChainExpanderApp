# Building a Chain Expander application using Elektron Websocket API and .NET Core C# 

[Elektron WebSocket API](https://developers.refinitiv.com/elektron/WebSocket-api/learning) is a server-side API which provides an interface to create direct WebSocket access to any OMM Content via ADS. The API leverages standard JSON and WebSocket protocols to be easy to implement and understand. It does mean the software developer can use any programming language with the WebSocket API. It requires a JSON parser with a Client WebSocket library for connecting to the server and sends or receive data using a messages specification provided on [WebSocket API Developer Guide](https://docsdevelopers.refinitiv.com/1563871102906/14977/). 
 
This article provides a sample application which illustrates how to use Elektron Websocket API to retrieve a Chain Records and get underlying RIC symbols. The use case is a solution for one of the popular questions regarding how can we automatically retrieving a list of all RIC symbols available on Elektron Realtime data feed. There is no capability on the data feed to provide all RIC symbols available for the user. However, there is a choice for the user to expanding  Chain RIC for specific market index or Stock Exchange and then get a list of RIC symbols instead. Anyway, the user has to know Chain RIC in advance. If you are not familiar with the Chain, you can find additional details from [About Chain ariticle](https://developers.refinitiv.com/article/elektron-article-1#AboutChains). It is a well-explained article about Chain RIC and its usage. Our sample application applies the methods described in the article with the WebSocket API to expanding Chain RIC.  

This sample application also utilizes C# codes from WebsocketAdapter project from  [the MRNWebSocketViewer Github repository](https://github.com/Refinitiv-API-Samples/Example.WebSocketAPI.CSharp.MRNWebSocketViewer), 
to manage WebSocket client connection and send or receive messages. Thus this article will describe only the detail about the Chain expanding logic and related implementation. It will not provide dept details regarding how it uses C# ClientWebsocket class to communicate with the server.

## Prerequisites

* User must have access to existing TREP 3.2.1 or higher which provide a WebSocket connection from Elektron service. The user must have permission to request the Chains instrument using Market Price domain.
* Understand concepts of Chain and how to retrieve it. Please read [About Chain ariticle](https://developers.refinitiv.com/article/elektron-article-1#AboutChains).
* Understand [WebSocket API Usages.](https://docs-developers.refinitiv.com/1563871102906/14977/).
* Understand [usage of ClientWebsocket class.](https://docs.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket?view=netcore-2.2).
* [.NET Core 2.2 or later version](https://dotnet.microsoft.com/download/dotnet-core/) 
* Visual Studio 2017 or 2019 or [Visual Studio Code](https://code.visualstudio.com/) to open project, compile and build a solution. 

### What is a Chain RIC?

We will provide you summary details of the Chain before we move to the next topics about the chain processing logic and .NET core implementation. Chain Records are used to hold RIC symbols that have a common association. It's a legacy of an older data distribution protocol called Marketfeed and still available to request from Elektron by using Market Price domain based on Reuters Domain Models (RDM). The Chain itself does not provide a price or market movement. Instead, it gives a list of particular RIC/constituent such as a list of RIC for a specific market, e.g., a list of strike prices for a particular option contract. 

The following RIC is a sample Chain RIC.

```
Chain 0#UNIVERSE.NB provides Nasdaq Basic RIC, and it also provides Best Bid and Offer and Last Sale information for all U.S. exchange-listed securities based on liquidity within the Nasdaq market center, as well as trades,  reported to the FINRA/Nasdaq Reporting Facility TM(TRFTM).

Chain  0#AMEXCONS.K provide Amex consolidated RICs for NYSE.
Chain  0#ARCACONS.K provide Arca consolidated RICs for NYSE.
Chain  0#.SETI provide RICs for Stock Exchange of Thailand.
Chain 0#UNIVERSE.PK provides the Pink sheet market.
```

Below is a Chains structure for Down Jones Industrial Average Index Chain(0#.DJI)
![Chain Structure](https://developers.refinitiv.com/sites/default/files/03%20-%20ChainDataStructure_1.png)

In this particular example, the chain composed of 3 instruments (the three green boxes) called Chain Records or underlying Chain RIC. These Chain Record, are linked together and constitute the complete chain. You can identify whether or not the Chain Record is the last one by checking if the Record contains an EMPTY value for LONGNEXTLR field. Chain Records made of a specific type of MarketPrice instrument specially designed for building chains. From the above picture, an application has to subscribe to the three Chain Record (0#.DJI,1#.DJI, and 2#.DJI) to retrieve all underlying RIC begin from ".DJI" to "XOM.N".

Chains only contain the names of their underlying RIC, not their values(e.g., AAPL.OQ from the Red arrow in the above picture). If the application wants to get a price or specific data from the underlying RIC list, it has to send an item request to retrieve data separately. The sample application provides only the result as underlying RIC list, and it does not send item request to get a price for a RIC in the list.

The sample application will apply the algorithms described in [About chain article](https://developers.refinitiv.com/article/simple-chain-objects-ema-part-1) to create the sample application, so you need to understand the Chain structure at the first step. 

## How to expanding Chains?

There are two approaches we used to process a Chains Records in this article. There are algorithms based on the suggestion from [About chain article](https://developers.refinitiv.com/article/simple-chain-objects-ema-part-1). The first method uses a sequential approach to request RIC and process a Chain Records. The second method is a heuristic algorithm to optimize a Chain processing logic. It can handle the chain faster than the first method, especially with long Chain Records. 

Using the sequential approach, we ensure that the application will get all Chain Records. It is because the app sends a new snapshot request to request data for the next chain RIC one by one until we found the last Chain RIC. Also, we can compare the result from the second approach with a sequential method to make sure that it returns precisely the same result.

### __A sequential method__

The algorithm is quite straightforward with additional logic to handle the case that input Chain RIC may not start from the first index("0#") and instead it starts from another index, e.g., "4#"," A#". Opening a chain is quite simple and usually done sequentially to make sure that the application will retrieve all Chain RIC from the first Chain record. Below is pseudo-code we use to implements application logic for the sequential approach. 

```
CurrentChainRecord = Input Chain Record
Open the CurrentChainRecord
do 
    If the Stream is Closed Then Stop
    Get the names of the elements referenced by the valid Link fields.
    NextChainRecord = Record referenced by the Next field
    PrevChainRecord = Record referenced by the Prev field
    If NextChainRecord is not Empty Then Open NextChainRecord
    If PrevChainRecord is not Empty Then Open PrevChainRecord
while NextChainRecord is not empty And PrevChainRecord is not Empty 
```
Note that the application has to stop when it receives a Close status while expanding a Chain. We assume that we can't get the most of underlying Chains records under the condition.


### A heuristic method to optimize a Chain expanding speed

The main issue for the first method is speed and turnaround time because it has to open the Chain Record one by one. Some long Chains may take time more than an hour to retrieve all Chain Records.

The heuristic algorithm was created based on a suggestion from [About chain article](https://developers.refinitiv.com/article/elektron-article-1#BetterPerformances) section "Better performance when opening long chains". It starts from extract the name of RIC root from the input Chain Record(e.g., 0#.DJI RIC root is ".DJI"). And then generate a list of possible Chain Record  0#<RIC root>, 1#<RIC root>, 2#<RIC root>… , n#<RIC root>.

The application can utilize a batch request feature from Elektron Websocket API to open only one request by passing a list of Chain Record in one batch request. The application process individual response for each item in the batch and skip the response message which the item stream state is Closed. The application can check the completion by verifying whether or not the subscription list contains First Record(Prev Link is Empty) and Last Record (Next Link is Empty). It can do after receives all item in the batch.

The index of the Chain Record(e.g., 0#,10#, F#) could be a series of Base10 or Base16 number. Therefore, we need to add a Base16 number "A#" to generate item list in the first batch request, and then it helps us identifying if the Chain use Base10 or Base16.

```
extract RIC root from Input Chain Record
StartIndex is 0
EndIndex is 10
BatchCount is MagicNumber(e.g. 50)
IsComplete is False
UseBase16 is False
ChainBatch is StartIndex#<RIC root> ...  EndIndex#<RIC root>, A#<RIC root>
Open ChainBatch using batch snapshot request
do
    Get RIC name from the response message
    If item Stream is Closed 
    Then 
        add RIC name to Subscription list with Notfound status
    else
        add RIC name with Link fields to SubscriptionList
        If RIC name is A#<RiC root>
            UseBase16 is True

    If SubscriptionList contains all RIC in ChainBatch
    Then 
       If SubscriptionList contains First Record and Last Record 
       Then
            IsComplete is True 
       Else
            StartIndex is EndIndex+1
            EndIndex is EndIndex + BatchCount
            If UseBase16 is True
                Convert StartIndex and EndIndex to Base16 number

            ChainBatch = StartIndex#<RIC root> ...  EndIndex#<RIC root>
            Open ChainBatch using batch snapshot request

while IsComplete is False
```

## .NET Core Implementation

We will describe the core implementation of the sample application in this section. The sample application reuses WebSocket client codes from WebSocketAdapter project so we will add a new library named "ChainExpander" to the primary solution. It's a .NET core library which focuses on Chain processing logic. Also, it was designed to manage a data structure used by the algorithms we talk earlier.

The WebSocket API sends or receives a message using JSON format, so when the application receives a response message, the application has to parse a  response message type at the first step.  And then convert JSON message to the object type of MarketPriceRefreshMessage or StatusMessage class, depending on the message type. The application does not need to handle the update message because we use only snapshot request so the app will receive a single Refresh or Status message.

### How the application verifies valid Link fields?

Once it receives a Refresh message, the application needs to check if the field list contains a valid Chain data by checking field names in the list. Thus, we have created ChainTemplateEnum identify the type of Chain, and it would return None if it's not a Chain RIC. We have created  GetChainTemplate method to check Chain type, and it requires input as a KeyValue pair of the IDictionary<FieldName, Value>.  

Below is snippet codes of GetChainTemplate method.

```c#
 public enum ChainTemplateEnum
    {
        None=0,
        LinkEnum=80,
        LongLinkEnum=85,
        BrLinkEnum=32766
    }

  public static ChainTemplateEnum GetChainTemplate(IDictionary<string, dynamic> fieldlist)
        {
            if (fieldlist.ContainsKey("REF_COUNT") && fieldlist.ContainsKey("LINK_1")
                                                   && fieldlist.ContainsKey("LINK_5") &&
                                                   fieldlist.ContainsKey("LINK_14")
                                                   && fieldlist.ContainsKey("NEXT_LR") &&
                                                   fieldlist.ContainsKey("PREV_LR"))
                return ChainTemplateEnum.LinkEnum;
            else if (fieldlist.ContainsKey("REF_COUNT") && fieldlist.ContainsKey("LONGLINK1")
                                                        && fieldlist.ContainsKey("LONGLINK5") &&
                                                        fieldlist.ContainsKey("LONGLINK14")
                                                        && fieldlist.ContainsKey("LONGNEXTLR") &&
                                                        fieldlist.ContainsKey("LONGPREVLR"))
                return ChainTemplateEnum.LongLinkEnum;
            else if (fieldlist.ContainsKey("REF_COUNT") && fieldlist.ContainsKey("BR_LINK1")
                                                        && fieldlist.ContainsKey("BR_LINK5") &&
                                                        fieldlist.ContainsKey("BR_LINK14")
                                                        && fieldlist.ContainsKey("BR_NEXTLR") &&
                                                        fieldlist.ContainsKey("BR_PREVLR"))
                return ChainTemplateEnum.BrLinkEnum;

            return ChainTemplateEnum.None;
        }
```
### How the application handle a Chain Data?

After checking a Chain, Template application needs to convert a field list to a class which implements IChain interface. The class contains only property and methods required to process chain data. IsLast and IsFirst are methods to determine if the data in this object is the first or Last Chain Record. Both methods may return true if the Chain has only one Chain Record.

```c#
 internal interface IChain
    {
        int StreamId { get; set; }
        string RDNDISPLAY { get; set; }
        string DSPLY_NAME { get; set; }
        int REF_COUNT { get; set; }
        int RECORD_TYPE { get; set; }
        string PREF_DISP { get; set; }
        // Return list of underlying constituent or ric name under chain ric
        IEnumerable<string> Constituents { get; } 
        bool IsLast { get; }
        bool IsFirst { get; }
        ChainTemplateEnum TemplateType { get; set; }
    }
```
All Classes and the Interfaces to handle Chain data was designed based on below table from About chain article.
![chaintemplate](https://raw.githubusercontent.com/Refinitiv-API-Samples/Article.WebSocketAPI.DotNETCore.ChainExpanderApp/master/images/chaintemplate.JPG)

The following ChainLongLink class used to handle Chain RIC which use LONGLINK template. Please find the additional Classes from full source files on GitHub.

```c#
 internal class ChainLongLink : IChain
    {
        public int StreamId { get; set; }
        public string RDNDISPLAY { get; set; }
        public string DSPLY_NAME { get; set; }
        public int REF_COUNT { get; set; }
        public int RECORD_TYPE { get; set; }
        public string PREF_DISP { get; set; }
        public IEnumerable<string> Constituents
        {
            get => // Return RIC list from LONGLINK1 to LONGLINK14
        }
        public bool IsLast => string.IsNullOrEmpty(LONGNEXTLR);
        public bool IsFirst => string.IsNullOrEmpty(LONGPREVLR);
        public ChainTemplateEnum TemplateType { get; set; }
        public string LONGLINK1 { get; set; }
        public string LONGLINK2 { get; set; }
        public string LONGLINK3 { get; set; }
        public string LONGLINK4 { get; set; }
        public string LONGLINK5 { get; set; }
        public string LONGLINK6 { get; set; }
        public string LONGLINK7 { get; set; }
        public string LONGLINK8 { get; set; }
        public string LONGLINK9 { get; set; }
        public string LONGLINK10 { get; set; }
        public string LONGLINK11 { get; set; }
        public string LONGLINK12 { get; set; }
        public string LONGLINK13 { get; set; }
        public string LONGLINK14 { get; set; }
        public string LONGPREVLR { get; set; }
        public string LONGNEXTLR { get; set; }
        public string PREF_LINK { get; set; }
        public string PREV_DISP { get; set; }
    }
```

### How to verify completion of the Chain?

The ChainExpander library has a ChainData class to caches the Chain Record data. It uses SoredDictionary to holds a KeyValue pair of RIC name with object implements with IChain interface.

We use SortedDictionary in order to make it fast to access First and Last element when we check the completion of the Chain Records.

```c#
 internal class ChainData
    {
        public string StartChainRic { get; set; }
        private readonly SortedDictionary<string,IChain> _chains=new SortedDictionary<string, IChain>(new ChainComparer());
        //...
       
        public SortedDictionary<string, IChain> ChainList => _chains;
        public void Clear()
        {
            _chains.Clear();
        }

        public int Count => _chains.Count;
        public bool Add(string chain_ric,IChain data){...}
        public bool Remove(string chain_ric){...}
        public bool Update(string chain_ric, IChain dataP){...}
        
    }
```
To verify the status of the item request we have created ChainRequestStatusEnum to keep track of the request status. 

```c#
 internal enum ChainRequestStatusEnum
    {
        Received = 0,
        Wait = 1,
        NotFound = 2
    }
```
The main class also use SoredDictionary to keep the item status. Once the library sends item request for a specific item, it will add KeyValue pair of RIC name and initial Wait status to the SoredDictionary. There is a _chainData object from sample codes below copied from the main ChainExpander class.

```c#
  public class ChainExpander
    {
        private readonly WebsocketMarketDataManager _websocketMarketDataMgr;
        private readonly ChainData _chainData=new ChainData();
        private readonly SortedDictionary<string,ChainRequestStatusEnum> _chainList=new SortedDictionary<string, ChainRequestStatusEnum>(new ChainComparer());
        public ChainExpander(WebsocketConnectionClient websocketAdapter)
        {
            var websocketAdapter1 = websocketAdapter ?? throw new ArgumentNullException(nameof(websocketAdapter));
            _websocketMarketDataMgr = new WebsocketMarketDataManager(websocketAdapter);
            websocketAdapter1.MessageEvent += this.ProcessWebsocketMessage;
        }
        private void ProcessMessage(JToken jsonData, MessageTypeEnum msgType, DomainEnum domain)
        {
            switch (domain)
            {
                case DomainEnum.Login:
                    ProcessLogin(jsonData, msgType);
                    break;
                case DomainEnum.MarketPrice:
                    //Core Chain Processing logic
                    ProcessChainResponseMessage(jsonData, msgType);
                    break;
                default:
                    Console.WriteLine("Unsupported Domain");
                    RaiseErrorEvent(DateTime.Now,$"Received response message for unhandled domain model");
                    break;
            }
        }
}
```

The primary data processing logic was created based on the pseudo-codes from section "how to expanding chain". It uses the ChainData object and status from _chainList object to verify if the subscription is complete or it has any error. 

Below is snippet codes from the ProcessChainResponseMessage, it used to check if all subscription is complete and we found the first and last element then it will generate an underlying RIC list and send the result back to the application layer. 

```c#
 if ( _chainData.Count > 0  && AllReceived(_chainList))
{
    if ((_chainData.ChainList.Any(item=>item.Value.IsFirst)) && _chainData.ChainList.Any(item=>item.Value.IsLast))
    {
        GenerateResult(_chainData.ChainList, "Extraction completed successful.");
        return;
    }
    ...
}
```

## Building and running the sample application

The sample application is a .NET Core console application. You can download full solution projects from [GitHub](https://github.com/Refinitiv-API-Samples/Article.WebSocketAPI.DotNETCore.ChainExpanderApp). And then you can build and run the app on Platforms which supports .NET Core 2.2 or later version.

### Building the application

You can open a solution file WebsocketChainExpander.sln on Visual Studio 2017 or 2019 and then build or publish(menu Build->Publish WebsocketChainExpander) the console application.

If you do not have Visual Studio, you can install the .NET Core SDK on your OS, and you may follow the following steps to build the application.

1) Run the Windows command line or using the terminal on macOS or Linux. Change folder to the repository from GitHub. You should see WebsocketChainExpander.sln in that folder. Then change folder to WebsocketChainExpander folder, which is the primary app project folder. 

2) Make sure that you are running with .NET Core 2.2 or later version. Just check by running **dotnet --version**. 


3) Run **dotnet build** and then you should see it generate WebsocketChainExpander.dll under the folder "bin\Debug\netcoreapp<.NET version>".

There is a choice for you to generate the executable file by using [Self Contained deployment](https://docs.microsoft.com/en-us/dotnet/core/deploying/).
You can run **dotnet publish** command as below command where "-c release" is for release build and "release_build" is the name of the output folder.

```
dotnet publish -c release -r win-x64 -o ./release_build
```
You should see folder release_build with an executable file WebsocketChainExpander.exe and required DLLs under folder WebsocketChainExpander.

You can change **win-x64** to another OS, and you can find the list from [rid-catalog page](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).

### Running the application

You can run the WebsocketChainExpander.exe with the following options.

```
WebsocketChainExpander.exe -s ws://<ADS Sercer name/Ip>:<Websocket Port>/WebSocket -u <DACS User> -i <Chain RIC> --verbose
```
For examples,
```
WebsocketChainExpander.exe -s ws://wsserver1:15000/WebSocket -u apitest -i 0#.DJI  --verbose
```
Add --printjson will shows incomming and outgoing JSON messages.
```
D:\ChainExpander\WebsocketChainExpander.exe -s ws://wsserver1:15000/WebSocket -u apitest -i .AV.O  --verbose --printjson
``` 
Below is a sample JSON message and we can use this options to review JSON message if we found issue.

```JSON
=====================================================
Message Received:26-09-2019 16:03:44.559
================ Original JSON Data =================
Data:
[
  {
    "ID": 13,
    "Type": "Status",
    "Key": {
      "Service": "API_TEST_ORACLE1",
      "Name": "6#.AV.O"
    },
    "State": {
      "Stream": "Closed",
      "Data": "Suspect",
      "Code": "NotFound",
      "Text": "The record could not be found"
    }
  },
   ...
  {
    "ID": 8,
    "Type": "Refresh",
    "Key": {
      "Service": "API_TEST_ORACLE1",
      "Name": "1#.AV.O"
    },
    "State": {
      "Stream": "NonStreaming",
      "Data": "Ok",
      "Text": "All is well"
    },
    "Qos": {
      "Timeliness": "Realtime",
      "Rate": "TickByTick"
    },
    "PermData": "AwEBdMA=",
    "SeqNumber": 64352,
    "Fields": {
      "PROD_PERM": 74,
      "RDNDISPLAY": 173,
      "DSPLY_NAME": "TOP 25 BY VOLUME",
      "RDN_EXCHID": "   ",
      "TIMACT": "09:03:35",
      "ACTIV_DATE": "2019-09-26",
      "NUM_MOVES": 155,
      "REF_COUNT": 11,
      "RECORDTYPE": 117,
      "LONGLINK1": "ADXS.O",
      "LONGLINK2": "AMD.O",
      "LONGLINK3": "NFLX.O",
      "LONGLINK4": "CTRP.O",
      "LONGLINK5": "PDD.O",
      "LONGLINK6": "ONVO.O",
      "LONGLINK7": "NVDA.O",
      "LONGLINK8": "HDS.O",
      "LONGLINK9": "MOR.O",
      "LONGLINK10": "ENTA.O",
      "LONGLINK11": "VOD.O",
      "LONGLINK12": null,
      "LONGLINK13": null,
      "LONGLINK14": null,
      "LONGPREVLR": ".AV.O",
      "LONGNEXTLR": null,
      "PREF_DISP": 0,
      "PREF_LINK": null,
      "RDN_EXCHD2": "NAQ",
      "PREV_DISP": 0,
      "TIMACT1": "09:03:35",
      "CONTEXT_ID": 3339,
      "DDS_DSO_ID": 8288,
      "SPS_SP_RIC": ".[SPSNSDQVAE1"
    }
  }
]
``` 
You can find additional options by using --help

### Sample output

Running the app with the following command to expanding 0#.SETI 
```
D:\ChainExpander\WebsocketChainExpander.exe -s ws://wsserver1:15000/WebSocket -u apitest -i 0#.SETI  --verbose
```
It will return the following result.

```bash

Start retrieving 0#.SETI
Process Refresh message for RIC Name: 4#.SETI [SET INDEX] Previous RIC is 3#.SETI Next RIC is 5#.SETI
Process Refresh message for RIC Name: 6#.SETI [SET INDEX] Previous RIC is 5#.SETI Next RIC is 7#.SETI
Process Refresh message for RIC Name: 1#.SETI [SET INDEX] Previous RIC is 0#.SETI Next RIC is 2#.SETI
Process Refresh message for RIC Name: 9#.SETI [SET INDEX] Previous RIC is 8#.SETI Next RIC is 10#.SETI
Process Refresh message for RIC Name: 2#.SETI [SET INDEX] Previous RIC is 1#.SETI Next RIC is 3#.SETI
Process Refresh message for RIC Name: 5#.SETI [SET INDEX] Previous RIC is 4#.SETI Next RIC is 6#.SETI
Process Refresh message for RIC Name: 7#.SETI [SET INDEX] Previous RIC is 6#.SETI Next RIC is 8#.SETI
Process Refresh message for RIC Name: 10#.SETI [SET INDEX] Previous RIC is 9#.SETI Next RIC is 11#.SETI
Process Refresh message for RIC Name: 3#.SETI [SET INDEX] Previous RIC is 2#.SETI Next RIC is 4#.SETI
Process Refresh message for RIC Name: 0#.SETI [SET INDEX] Previous RIC is Empty Next RIC is 1#.SETI
Process Refresh message for RIC Name: 8#.SETI [SET INDEX] Previous RIC is 7#.SETI Next RIC is 9#.SETI
Process Refresh message for RIC Name: 35#.SETI [SET INDEX] Previous RIC is 34#.SETI Next RIC is 36#.SETI
Process Refresh message for RIC Name: 37#.SETI [SET INDEX] Previous RIC is 36#.SETI Next RIC is 38#.SETI
Process Refresh message for RIC Name: 33#.SETI [SET INDEX] Previous RIC is 32#.SETI Next RIC is 34#.SETI
Process Refresh message for RIC Name: 31#.SETI [SET INDEX] Previous RIC is 30#.SETI Next RIC is 32#.SETI
Process Refresh message for RIC Name: 39#.SETI [SET INDEX] Previous RIC is 38#.SETI <= Final RIC
Process Refresh message for RIC Name: 15#.SETI [SET INDEX] Previous RIC is 14#.SETI Next RIC is 16#.SETI
Process Refresh message for RIC Name: 34#.SETI [SET INDEX] Previous RIC is 33#.SETI Next RIC is 35#.SETI
Process Refresh message for RIC Name: 36#.SETI [SET INDEX] Previous RIC is 35#.SETI Next RIC is 37#.SETI
Process Refresh message for RIC Name: 13#.SETI [SET INDEX] Previous RIC is 12#.SETI Next RIC is 14#.SETI
Process Refresh message for RIC Name: 25#.SETI [SET INDEX] Previous RIC is 24#.SETI Next RIC is 26#.SETI
Process Refresh message for RIC Name: 17#.SETI [SET INDEX] Previous RIC is 16#.SETI Next RIC is 18#.SETI
Process Refresh message for RIC Name: 30#.SETI [SET INDEX] Previous RIC is 29#.SETI Next RIC is 31#.SETI
Process Refresh message for RIC Name: 38#.SETI [SET INDEX] Previous RIC is 37#.SETI Next RIC is 39#.SETI
Process Refresh message for RIC Name: 23#.SETI [SET INDEX] Previous RIC is 22#.SETI Next RIC is 24#.SETI
Process Refresh message for RIC Name: 27#.SETI [SET INDEX] Previous RIC is 26#.SETI Next RIC is 28#.SETI
Process Refresh message for RIC Name: 32#.SETI [SET INDEX] Previous RIC is 31#.SETI Next RIC is 33#.SETI
Process Refresh message for RIC Name: 21#.SETI [SET INDEX] Previous RIC is 20#.SETI Next RIC is 22#.SETI
Process Refresh message for RIC Name: 29#.SETI [SET INDEX] Previous RIC is 28#.SETI Next RIC is 30#.SETI
Process Refresh message for RIC Name: 11#.SETI [SET INDEX] Previous RIC is 10#.SETI Next RIC is 12#.SETI
Process Refresh message for RIC Name: 19#.SETI [SET INDEX] Previous RIC is 18#.SETI Next RIC is 20#.SETI
Process Refresh message for RIC Name: 22#.SETI [SET INDEX] Previous RIC is 21#.SETI Next RIC is 23#.SETI
Process Refresh message for RIC Name: 20#.SETI [SET INDEX] Previous RIC is 19#.SETI Next RIC is 21#.SETI
Process Refresh message for RIC Name: 28#.SETI [SET INDEX] Previous RIC is 27#.SETI Next RIC is 29#.SETI
Process Refresh message for RIC Name: 12#.SETI [SET INDEX] Previous RIC is 11#.SETI Next RIC is 13#.SETI
Process Refresh message for RIC Name: 18#.SETI [SET INDEX] Previous RIC is 17#.SETI Next RIC is 19#.SETI
Process Refresh message for RIC Name: 26#.SETI [SET INDEX] Previous RIC is 25#.SETI Next RIC is 27#.SETI
Process Refresh message for RIC Name: 16#.SETI [SET INDEX] Previous RIC is 15#.SETI Next RIC is 17#.SETI
Process Refresh message for RIC Name: 14#.SETI [SET INDEX] Previous RIC is 13#.SETI Next RIC is 15#.SETI
Process Refresh message for RIC Name: 24#.SETI [SET INDEX] Previous RIC is 23#.SETI Next RIC is 25#.SETI

Operation Completed in 1708 MS.

Below is a list of Chain RIC application has requested from the server

0#.SETI,1#.SETI,2#.SETI,3#.SETI,4#.SETI,5#.SETI,6#.SETI,7#.SETI,8#.SETI,9#.SETI,10#.SETI,11#.SETI,12#.SETI,13#.SETI,14#.SETI,15#.SETI,16#.SETI,17#.SETI,18#.SETI,19#.SETI,20#.SETI,21#.SETI,22#.SETI,23#.SETI,24#.SETI,25#.SETI,26#.SETI,27#.SETI,28#.SETI,29#.SETI,30#.SETI,31#.SETI,32#.SETI,33#.SETI,34#.SETI,35#.SETI,36#.SETI,37#.SETI,38#.SETI,39#.SETI


Received 548 constituents/instruments from the Chains

7UP.BK
A.BK
AAV.BK
ACC.BK
ADVANC.BK
AEC.BK
AEONTS.BK
AFC.BK
AH.BK
AHC.BK
AI.BK
...
WORK.BK
WORLD.BK
WP.BK
WPH.BK
WR.BK
YCI.BK
ZEN.BK
ZMICO.BK

Quit the app
```
The default mode is Heuristic mode and it the result from .NET StopWatch reports Operation Completed in 1708 MS which is around 1.7 second. But when running with sequential mode by add --seq to the command line.

```
D:\ChainExpander\WebsocketChainExpander.exe -s ws://wsserver1:15000/WebSocket -u apitest -i 0#.SETI  --seq --verbose
```
It retrieves a Chain record one by one, and it reports Operation Completed in 17099 MS which is around 17 second.  It takes more time to expand long Chain record when compare with default mode. 

Below is a result of sequential mode.

```bash

Process Refresh message for RIC Name: 0#.SETI [SET INDEX] Previous RIC is Empty Next RIC is 1#.SETI
Process Refresh message for RIC Name: 1#.SETI [SET INDEX] Previous RIC is 0#.SETI Next RIC is 2#.SETI
Process Refresh message for RIC Name: 2#.SETI [SET INDEX] Previous RIC is 1#.SETI Next RIC is 3#.SETI
Process Refresh message for RIC Name: 3#.SETI [SET INDEX] Previous RIC is 2#.SETI Next RIC is 4#.SETI
Process Refresh message for RIC Name: 4#.SETI [SET INDEX] Previous RIC is 3#.SETI Next RIC is 5#.SETI
Process Refresh message for RIC Name: 5#.SETI [SET INDEX] Previous RIC is 4#.SETI Next RIC is 6#.SETI
Process Refresh message for RIC Name: 6#.SETI [SET INDEX] Previous RIC is 5#.SETI Next RIC is 7#.SETI
Process Refresh message for RIC Name: 7#.SETI [SET INDEX] Previous RIC is 6#.SETI Next RIC is 8#.SETI
Process Refresh message for RIC Name: 8#.SETI [SET INDEX] Previous RIC is 7#.SETI Next RIC is 9#.SETI
Process Refresh message for RIC Name: 9#.SETI [SET INDEX] Previous RIC is 8#.SETI Next RIC is 10#.SETI
Process Refresh message for RIC Name: 10#.SETI [SET INDEX] Previous RIC is 9#.SETI Next RIC is 11#.SETI
Process Refresh message for RIC Name: 11#.SETI [SET INDEX] Previous RIC is 10#.SETI Next RIC is 12#.SETI
Process Refresh message for RIC Name: 12#.SETI [SET INDEX] Previous RIC is 11#.SETI Next RIC is 13#.SETI
Process Refresh message for RIC Name: 13#.SETI [SET INDEX] Previous RIC is 12#.SETI Next RIC is 14#.SETI
Process Refresh message for RIC Name: 14#.SETI [SET INDEX] Previous RIC is 13#.SETI Next RIC is 15#.SETI
Process Refresh message for RIC Name: 15#.SETI [SET INDEX] Previous RIC is 14#.SETI Next RIC is 16#.SETI
Process Refresh message for RIC Name: 16#.SETI [SET INDEX] Previous RIC is 15#.SETI Next RIC is 17#.SETI
Process Refresh message for RIC Name: 17#.SETI [SET INDEX] Previous RIC is 16#.SETI Next RIC is 18#.SETI
Process Refresh message for RIC Name: 18#.SETI [SET INDEX] Previous RIC is 17#.SETI Next RIC is 19#.SETI
Process Refresh message for RIC Name: 19#.SETI [SET INDEX] Previous RIC is 18#.SETI Next RIC is 20#.SETI
Process Refresh message for RIC Name: 20#.SETI [SET INDEX] Previous RIC is 19#.SETI Next RIC is 21#.SETI
Process Refresh message for RIC Name: 21#.SETI [SET INDEX] Previous RIC is 20#.SETI Next RIC is 22#.SETI
Process Refresh message for RIC Name: 22#.SETI [SET INDEX] Previous RIC is 21#.SETI Next RIC is 23#.SETI
Process Refresh message for RIC Name: 23#.SETI [SET INDEX] Previous RIC is 22#.SETI Next RIC is 24#.SETI
Process Refresh message for RIC Name: 24#.SETI [SET INDEX] Previous RIC is 23#.SETI Next RIC is 25#.SETI
Process Refresh message for RIC Name: 25#.SETI [SET INDEX] Previous RIC is 24#.SETI Next RIC is 26#.SETI
Process Refresh message for RIC Name: 26#.SETI [SET INDEX] Previous RIC is 25#.SETI Next RIC is 27#.SETI
Process Refresh message for RIC Name: 27#.SETI [SET INDEX] Previous RIC is 26#.SETI Next RIC is 28#.SETI
Process Refresh message for RIC Name: 28#.SETI [SET INDEX] Previous RIC is 27#.SETI Next RIC is 29#.SETI
Process Refresh message for RIC Name: 29#.SETI [SET INDEX] Previous RIC is 28#.SETI Next RIC is 30#.SETI
Process Refresh message for RIC Name: 30#.SETI [SET INDEX] Previous RIC is 29#.SETI Next RIC is 31#.SETI
Process Refresh message for RIC Name: 31#.SETI [SET INDEX] Previous RIC is 30#.SETI Next RIC is 32#.SETI
Process Refresh message for RIC Name: 32#.SETI [SET INDEX] Previous RIC is 31#.SETI Next RIC is 33#.SETI
Process Refresh message for RIC Name: 33#.SETI [SET INDEX] Previous RIC is 32#.SETI Next RIC is 34#.SETI
Process Refresh message for RIC Name: 34#.SETI [SET INDEX] Previous RIC is 33#.SETI Next RIC is 35#.SETI
Process Refresh message for RIC Name: 35#.SETI [SET INDEX] Previous RIC is 34#.SETI Next RIC is 36#.SETI
Process Refresh message for RIC Name: 36#.SETI [SET INDEX] Previous RIC is 35#.SETI Next RIC is 37#.SETI
Process Refresh message for RIC Name: 37#.SETI [SET INDEX] Previous RIC is 36#.SETI Next RIC is 38#.SETI
Process Refresh message for RIC Name: 38#.SETI [SET INDEX] Previous RIC is 37#.SETI Next RIC is 39#.SETI
Process Refresh message for RIC Name: 39#.SETI [SET INDEX] Previous RIC is 38#.SETI <= Final RIC

Operation Completed in 17099 MS.

Below is a list of Chain RIC application has requested from the server

0#.SETI,1#.SETI,2#.SETI,3#.SETI,4#.SETI,5#.SETI,6#.SETI,7#.SETI,8#.SETI,9#.SETI,10#.SETI,11#.SETI,12#.SETI,13#.SETI,14#.SETI,15#.SETI,16#.SETI,17#.SETI,18#.SETI,19#.SETI,20#.SETI,21#.SETI,22#.SETI,23#.SETI,24#.SETI,25#.SETI,26#.SETI,27#.SETI,28#.SETI,29#.SETI,30#.SETI,31#.SETI,32#.SETI,33#.SETI,34#.SETI,35#.SETI,36#.SETI,37#.SETI,38#.SETI,39#.SETI


Received 548 constituents/instruments from the Chains

7UP.BK
A.BK
AAV.BK
ACC.BK
ADVANC.BK
AEC.BK
AEONTS.BK
AFC.BK
AH.BK
AHC.BK
AI.BK
...
WORK.BK
WORLD.BK
WP.BK
WPH.BK
WR.BK
YCI.BK
ZEN.BK
ZMICO.BK

Quit the app
```

Also, by running the app on the same server as ADS, the default mode can be expanding a long chain such as 0#UNIVERSE.PK which has around 1222 Chain Records and contains 17000 RIC within 5072 MS.

### Limitation of the sample application

The following list is a known limitation we found while testing the app.

1) The sample app supports only one input Chain RIC, and it can not retrieve all Chain Record if one of the records is not found or has an issue. First record and Last record must appear in the list. 

2) We found an issue with some RIC such as "0#TRTSYIL=IS", it appears that Next field constructs with difference RIC name ("LONGNEXTLR": TRTSYILQ2=), and it returns the result as below list.
```
0#TRTSYIL=IS,TRTSYILQ10=,TRTSYILQ11=,TRTSYILQ12=,TRTSYILQ13=,TRTSYILQ14=,TRTSYILQ15=,TRTSYILQ16=,TRTSYILQ2=,TRTSYILQ3=,TRTSYILQ4=,TRTSYILQ5=,TRTSYILQ6=,TRTSYILQ7=,TRTSYILQ8=,TRTSYILQ9=
```
This kind of RIC will not work with RIC guessing algorithm, and you need to run with sequential mode instead.

3) This app can expand recursive Chain RIC(e.g., 0#EURCURVES), but we do not guarantee that it will work with all Chain. And it quite slow. Also, the app will retrieve a child RIC using heuristic mode by default. You can try using sequential mode when using recursive Chain RIC by adding __--seqrecursive__ to the command line.

Please note that this application does not design to works with TREP-RT on EDP(Elektron Data Platform) which require additional steps to manage a Login Token.

## Summary

This article explains how to apply the algorithms describes in [About chain article](https://developers.refinitiv.com/article/simple-chain-objects-ema-part-1) to create Elektron Websocket API consumer application to expanding Chain RIC. There are two modes in the app. The first one is a sequential mode which sends item request one by one until it found the last record. The second approach which is faster one, it applies suggestion from the article to send a batch request with a list of Chain Record generated by RIC guessing algorithm. From a test result, it faster and reduce the number of a request message that the application needs to send to the server. Anyway, the sequential mode still useful in a scenario that the number of Chain Record is not much. And we need to ensure that application is retrieving all Chain record under the Root Chain RIC.

## Contributing

Please read [CONTRIBUTING.md](https://gist.github.com/PurpleBooth/b24679402957c63ec426) for details on our code of conduct, and the process for submitting pull requests to us.

## Authors

* **Moragodkrit Chumsri** - Release 1.0.  *Initial work*

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## References

* [About Chain article](https://developers.refinitiv.com/article/simple-chain-objects-ema-part-1)
* [Elektron WebSocket API](https://developers.refinitiv.com/elektron/WebSocket-api/learning)
* [WebSocket API Developer Guide](https://docs-developers.refinitiv.com/1563871102906/14977/)
* [.NET Core RID Catalog](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).
* [Dotnet Core Publish Command](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish?tabs=netcore21)
* [.NET Commandlineparser](https://github.com/commandlineparser/commandline)
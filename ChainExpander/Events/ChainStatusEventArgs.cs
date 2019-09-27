using System;
using ChainExpander.Models.Message;
using ChainExpander.Models.Enum;

namespace ChainExpander.Events
{
    public class ChainStatusMsgEventArgs
    {
        public DateTime TimeStamp { get; set; }
        public StatusMessage Status { get; set; }

    }
}

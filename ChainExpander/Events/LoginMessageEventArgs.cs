using System;
using ChainExpander.Models.Data;

namespace ChainExpander.Events
{
    public class LoginMessageEventArgs
    {
        public DateTime TimeStamp { get; set; }
        public IMessage Message { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace telegramvkbridge
{
    internal static class StaticStuff
    {
        public enum UserState
        {
            NoAuth = 0,
            EnteringCredentials = 1,
            ChatChoice = 2,
            Connected = 3,

        }
    }
}

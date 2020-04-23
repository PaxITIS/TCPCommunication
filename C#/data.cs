using System;
using System.Collections.Generic;
using System.Text;

namespace tcptest
{
    [Serializable()]
    public class Packet
    {
        public Packet()
        {
            data = new byte[3*1024*1024];
        }
        public byte[] data { get; set; }

        public string Message { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Guid SessionGuid { get; set; }

        public new string ToString()
        {
            return Message;
        }
    }

    [Serializable()]
    public class ReturnPacket
    {
        public bool OK { get; set; }
        public string Message { get; set; }
    }
}

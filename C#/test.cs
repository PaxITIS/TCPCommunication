using System;
using System.Collections.Generic;
using System.Text;
using Networking;
using System.Threading;

namespace tcptest
{
    public class Program
    {
        private const int PORT = 99;
        public static string SERVER_IP = "127.0.0.1";


        public static int Main(String[] args)
        {
            Console.WriteLine("(S)erver mode     (C)lient mode     (A)utomatic test");
            ConsoleKey k = Console.ReadKey().Key ;
            if (k == ConsoleKey.S)
                servermode();
            else if (k == ConsoleKey.C) {
                Console.WriteLine("Input Server IP: (default 127.0.0.1):");
                string ret = Console.ReadLine();
                if (ret != string.Empty){
                    SERVER_IP = ret;
                }
                clientmode();
            }             
            else
                autotest();
            return 0;
        }

        private static void servermode()
        {
            Console.WriteLine();
            NetworkServer ns = new NetworkServer(PORT, new ProcessPayload(serverprocess));

            Console.WriteLine("\nHit 'ctrl+c' to end server");
            ns.Start();
            bool _done = false;
            
            while (!_done)
            {
                Thread.Sleep(1);
            }
            ns.Stop();
            return;
        }

        private static void autotest()
        {
            bool cont = true;
            int i = 0;
            NetworkClient nc = new NetworkClient(SERVER_IP, PORT);
            if (nc.Connect() == false)
            {
                Console.WriteLine("Server not available");
                return;
            }
            Packet p = new Packet();
            while (cont)
            {
                p.Message = "" + i++;
                ReturnPacket ret = nc.Send(p) as ReturnPacket;
                if (ret != null)
                {
                    string returndata = ret.Message;
                    Console.Write("This is what the host returned to you: {0}\r", returndata);
                }
                Thread.Sleep(1);
            }
        }

        private static void clientmode()
        {
            Console.WriteLine();
            NetworkClient nc = new NetworkClient(SERVER_IP, PORT);
            if (nc.Connect() == false)
            {
                Console.WriteLine("Server not available");
                return;
            }
            string DataToSend = "";
            while (DataToSend != "quit")
            {
                Console.WriteLine("\nType a text to be sent:");
                StringBuilder sb = new StringBuilder();

                DataToSend = Console.ReadLine();

                if (DataToSend.Length == 0) continue;
                Packet packet = new Packet();
                packet.Message = DataToSend;
                try
                {
                    nc.Connect();
                    ReturnPacket ret = nc.Send(packet) as ReturnPacket;
                    if (ret != null)
                    {
                        string returndata = ret.Message;
                        Console.WriteLine("This is what the host returned to you: \r\n{0}", returndata);
                    }
                    else
                    {
                        Console.WriteLine("return null");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return;
                }
            }
            nc.Close();
        }

        private static object serverprocess(object data)
        {
            Packet dp = data as Packet;
            if (dp != null)
                return HandlePacket(dp);
            
            Console.WriteLine("message not recognized");
            return new ReturnPacket();
        }

        static int  count = 0;
        static DateTime  dt = DateTime.Now;
        private static object HandlePacket(Packet dp)
        {
            ReturnPacket ret = new ReturnPacket();
            if (dp.SessionGuid == Guid.Empty)
            {
                // Authenticate username and password possibly with LDAP server
            }
            else
            {
                // check sessionguid valid -> if not return failed
            }
            ret.OK = true;
            ret.Message = "your msg : " + dp.Message + "\r\nreturn from server " + DateTime.Now;
            count++;
            if (DateTime.Now.Subtract(dt).TotalMilliseconds > 10000)
            {
                Console.WriteLine("count in 10 secs = " + count);
                count = 0;
                dt = DateTime.Now;
            }
            return ret;
        }
    }
}

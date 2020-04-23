using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

#if NETSERVER
using System.Threading;
using System.Collections;
using System.Net;
#endif

namespace Networking
{
    // remove and add log4net when required
    public static class log
    {
        public static void Debug(string from, object text)
        {
            //var sw = File.AppendText("errors.log");
            //sw.WriteLine(from + "|"+ DateTime.Now + " = " + text);
            //sw.Flush();
            //sw.Close();
            Console.WriteLine(from + " : " + text);
        }
    }

    public static class Config
    {
        public const int CHUNK_SIZE = 64 * 1 * 1024; // 32 kb packets
        public const int SEND_TIMEOUT =  5 * 1000; // 5 secs
        public const int CONNNECT_TIMEOUT = 1000; // 1 sec
        public const int NUM_OF_THREADS = 10;
        public const int PORT_NUM = 1111;
    }

    internal delegate object ProcessPayload(object data);

    #region [  NetworkCommon  ]

    internal class NetworkCommon
    {
        public static object RecievePacket(TcpClient tcp)
        {
            var ns = tcp.GetStream();
            var br = new BinaryReader(ns);
            DateTime dt = DateTime.Now;
            while (tcp.Available < 4)
            {
                Thread.Sleep(1);
                if (DateTime.Now.Subtract(dt).TotalMilliseconds > Config.SEND_TIMEOUT )
                    return null;
            } 
            int len = br.ReadInt32();
            if (len > 0 )
            {
                // read packet.datalen bytes into buffer
                var data = new byte[Config.CHUNK_SIZE];
                MemoryStream ms = new MemoryStream();
                int size = Config.CHUNK_SIZE;
                while(len>0)
                {
                    int read = ns.Read(data, 0, size);
                    ms.Write(data, 0, read);
                    len -= read;
                }
                ms.Seek(0, SeekOrigin.Begin);
                // deserialize buffer into object
                var bf = new BinaryFormatter();
                try
                {
                    return bf.Deserialize(ms);
                }
                catch (Exception ex)
                {
                    log.Debug("NetworkCommon", "deserialize failed" + ex);
                    return null;
                }
            }
            else
            {
                ns.Flush();
                ns.Close();
                return null;
            }
        }


        public static object SendPacket(bool waitforreturn, TcpClient tcp, object data)
        {
            var ns = tcp.GetStream();
            // serialize data into buffer
            var bf = new BinaryFormatter();
            var st = new MemoryStream();

            bf.Serialize(st, data);
            byte[] databytes = st.GetBuffer();
            int len = databytes.Length;
            // send packet header size
            var bw = new BinaryWriter(ns);
            bw.Write(len);
            bw.Flush();

            // send data bytes
            int offset = 0;
            int size = Config.CHUNK_SIZE;
            while (len > 0)
            {
                if (len < size)
                    size = len;
                ns.Write(databytes, offset, size );
                len -= size;
                offset += size;
            }
            ns.Flush();
            if (waitforreturn)
            {
                return RecievePacket(tcp);
            }
            return null;
        }
    }

    #endregion

    #region [  NetworkClient  ]

    internal class NetworkClient
    {
        private readonly string _host = "127.0.0.1";
        private readonly int _portNum = Config.PORT_NUM;
        private TcpClient _tcpclient;

        public NetworkClient(string host, int port)
        {
            _host = host;
            _portNum = port;
        }

        public bool Connected
        {
            get { return _tcpclient.Connected; }
        }

        public bool Connect()
        {
            try
            {
                if (_tcpclient != null)
                {
                    NetworkStream ns = _tcpclient.GetStream();
                    if (ns != null)
                        ns.Close();
                    if (_tcpclient != null)
                        _tcpclient.Close();
                }
                _tcpclient = new TcpClient();
                _tcpclient.SendBufferSize = Config.CHUNK_SIZE;
                _tcpclient.SendTimeout = Config.SEND_TIMEOUT;
                _tcpclient.ReceiveTimeout = _tcpclient.SendTimeout;
                _tcpclient.ReceiveBufferSize = _tcpclient.SendBufferSize;
                IAsyncResult res = _tcpclient.BeginConnect(_host, _portNum, null, null);
                bool ok = res.AsyncWaitHandle.WaitOne(Config.CONNNECT_TIMEOUT, true);
                if (!ok)
                {
                    NetworkStream ns = _tcpclient.GetStream();
                    if (ns != null)
                        ns.Close();
                    if (_tcpclient != null)
                        _tcpclient.Close();
                }

                return ok;
            }
            catch (Exception ex)
            {
                log.Debug("NetworkClient", "connect ex =" + ex);
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (_tcpclient != null)
                {
                    if (_tcpclient.Connected)
                    {
                        NetworkStream ns = _tcpclient.GetStream();
                        if (ns != null)
                            ns.Close();
                    }
                    _tcpclient.Close();
                }
            }
            catch (Exception ex)
            {
                log.Debug("NetworkClient", "close tcp ex = " + ex);
            }
        }

        public object Send(object data)
        {
            try
            {
                if (_tcpclient.Connected == false)
                    Connect();
                if (_tcpclient.Connected)
                {
                    return NetworkCommon.SendPacket(true, _tcpclient, data);
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Debug("NetworkClient", "send ex =" + ex);

                NetworkStream ns = _tcpclient.GetStream();
                if (ns != null)
                    ns.Close();
                if (_tcpclient != null)
                    _tcpclient.Close();

                return null;
            }
        }
    }

    #endregion

#if NETSERVER

    #region [  NetworkServer  ]

    internal class NetworkServer
    {
        #region [  ClientService  ]

        internal class ClientService
        {
            private ClientConnectionPool ConnectionPool;
            private bool ContinueProcess = false;
            private Thread[] ThreadTask = new Thread[Config.NUM_OF_THREADS];

            public ClientService(ClientConnectionPool ConnectionPool)
            {
                this.ConnectionPool = ConnectionPool;
            }

            public void Start()
            {
                ContinueProcess = true;
                // Start threads to handle Client Task
                for (int i = 0; i < ThreadTask.Length; i++)
                {
                    ThreadTask[i] = new Thread(new ThreadStart(Process));
                    if (i > 0)
                        ThreadTask[i].IsBackground = true;
                    ThreadTask[i].Start();
                }
            }

            private void Process()
            {
                while (ContinueProcess)
                {
                    ClientHandler client = null;
                    lock (ConnectionPool.SyncRoot)
                    {
                        if (ConnectionPool.Count > 0)
                            client = ConnectionPool.Dequeue();
                    }
                    if (client != null)
                    {
                        // if client still connect, schedufor later processingle it 
                        if (client.Alive)
                        {
                            client.Process(); // Provoke client
                            ConnectionPool.Enqueue(client);
                        }
                        else
                            client.Close();
                    }

                    Thread.Sleep(1);
                }
            }

            public void Stop()
            {
                ContinueProcess = false;

                // Close all client connections
                while (ConnectionPool.Count > 0)
                {
                    ClientHandler client = ConnectionPool.Dequeue();
                    client.Close();
                }

                for (int i = 0; i < ThreadTask.Length; i++)
                {
                    if (ThreadTask[i] != null && ThreadTask[i].IsAlive)
                        ThreadTask[i].Abort();
                }
            }
        } // class ClientService

        #endregion

        #region [  ClientHandler  ]

        internal class ClientHandler
        {
            //private static readonly ILog log = LogManager.GetLogger(typeof(ClientHandler));
            public ProcessPayload _ProcessPayload;
            private byte[] bytes; // Data buffer for incoming data.
            private TcpClient ClientSocket;
            private MemoryStream ms = new MemoryStream();
            private NetworkStream networkStream;
            public Guid ClientGuid = Guid.NewGuid();

            public ClientHandler(TcpClient ClientSocket, ProcessPayload payloadhandler)
            {
                ClientSocket.ReceiveTimeout = Config.SEND_TIMEOUT;
                ClientSocket.SendTimeout = ClientSocket.ReceiveTimeout;

                this.ClientSocket = ClientSocket;
                networkStream = ClientSocket.GetStream();
                bytes = new byte[ClientSocket.ReceiveBufferSize];
                _ProcessPayload = payloadhandler;
            }

            public bool Alive
            {
                get { return (ClientSocket != null ? ClientSocket.Connected : false); }
            }

            public void Process()
            {
                try
                {
                    if (ClientSocket.Available == 0)
                        return;
                    object obj = NetworkCommon.RecievePacket(ClientSocket);
                    if (obj != null)
                    {
                        if (_ProcessPayload != null)
                        {
                            // process deserialized object
                            object oo = _ProcessPayload(obj);
                            // create packet with return of process
                            NetworkCommon.SendPacket(false, ClientSocket, oo);
                            return;
                        }
                    }
                }
                catch (IOException)
                {
                    // All the data has arrived; put it in response.
                    Close();
                }
                catch (SocketException)
                {
                    // conection broken
                    Close();
                }
            }

            public void Close()
            {
                if (networkStream != null)
                    networkStream.Close();
                if (ClientSocket != null)
                    ClientSocket.Close();
                networkStream.Dispose();
                ClientSocket = null;
            }
        } // class ClientHandler 

        #endregion

        #region [  ClientConnectionPool  ]

        internal class ClientConnectionPool
        {
            // Creates a synchronized wrapper around the Queue.
            private Queue SyncdQ = Queue.Synchronized(new Queue());

            public int Count
            {
                get { return SyncdQ.Count; }
            }

            public object SyncRoot
            {
                get { return SyncdQ.SyncRoot; }
            }

            public void Enqueue(ClientHandler client)
            {
                SyncdQ.Enqueue(client);
            }

            public ClientHandler Dequeue()
            {
                return (ClientHandler)(SyncdQ.Dequeue());
            }
        } // class ClientConnectionPool

        #endregion

        //private static readonly ILog log = LogManager.GetLogger(typeof(NetworkServer));
        private ClientService _ClientService;
        private ClientConnectionPool _ConnectionPool = new ClientConnectionPool();
        private TcpListener _Listener;
        private int _portNum = Config.PORT_NUM;
        private ProcessPayload _processPayload;

        public NetworkServer(int port, ProcessPayload callback)
        {
            _portNum = port;
            _processPayload = callback;
        }

        public void Stop()
        {
            // cleanup server connections
            _ClientService.Stop();
            _Listener.Stop();
        }

        public void Start()
        {
            try
            {
                _Listener = new TcpListener(IPAddress.Any, _portNum);
                _Listener.Start();
                // Client Task to handle client requests
                _ClientService = new ClientService(_ConnectionPool);

                _ClientService.Start();

                // Start listening for connections.
                _Listener.BeginAcceptTcpClient(AcceptCallback, _Listener);
            }
            catch (Exception)
            {
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                var server = (TcpListener)ar.AsyncState;
                TcpClient tcp = server.EndAcceptTcpClient(ar);
                tcp.ReceiveBufferSize = Config.CHUNK_SIZE;
                tcp.SendTimeout = Config.SEND_TIMEOUT;
                tcp.ReceiveTimeout = tcp.SendTimeout;
                tcp.SendBufferSize = Config.CHUNK_SIZE;

                server.BeginAcceptTcpClient(AcceptCallback, server);

                if (tcp != null)
                {
                    // An incoming connection needs to be processed.
                    var ch = new ClientHandler(tcp, _processPayload);
                    _ConnectionPool.Enqueue(ch);
                }
            }
            catch
            {

            }
        }
    }

    #endregion

#endif
}
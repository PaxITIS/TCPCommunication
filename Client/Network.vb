Imports System
Imports System.IO
Imports System.Net.Sockets
Imports System.Runtime.Serialization.Formatters.Binary


Imports System.Threading
Imports System.Collections
Imports System.Net


Namespace tcptest
    Module log
        Sub Debug(ByVal from As String, ByVal text As Object)
            Console.WriteLine(from & " : " & text)
        End Sub
    End Module

    Module Config
        Public Const CHUNK_SIZE As Integer = 64 * 1 * 1024
        Public Const SEND_TIMEOUT As Integer = 5 * 1000
        Public Const CONNNECT_TIMEOUT As Integer = 1000
        Public Const NUM_OF_THREADS As Integer = 10
        Public Const PORT_NUM As Integer = 1111
    End Module

    Friend Delegate Function ProcessPayload(ByVal data As Object) As Object

#Region "NetworkCommon"

    Friend Class NetworkCommon
        Public Shared Function RecievePacket(ByVal tcp As TcpClient) As Object
            Dim ns = tcp.GetStream()
            Dim br = New BinaryReader(ns)
            Dim dt As DateTime = DateTime.Now

            While tcp.Available < 4
                Threading.Thread.Sleep(1)
                If DateTime.Now.Subtract(dt).TotalMilliseconds > Config.SEND_TIMEOUT Then Return Nothing
            End While

            Dim len As Integer = br.ReadInt32()

            If len > 0 Then
                Dim data = New Byte(65535) {}
                Dim ms As MemoryStream = New MemoryStream()
                Dim size As Integer = Config.CHUNK_SIZE

                While len > 0
                    Dim read As Integer = ns.Read(data, 0, size)
                    ms.Write(data, 0, read)
                    len -= read
                End While

                ms.Seek(0, SeekOrigin.Begin)
                Dim bf = New BinaryFormatter()

                Try
                    Return bf.Deserialize(ms)
                Catch ex As Exception
                    log.Debug("NetworkCommon", "deserialize failed" & ex.Message)
                    Return Nothing
                End Try
            Else
                ns.Flush()
                ns.Close()
                Return Nothing
            End If
        End Function

        Public Shared Function SendPacket(ByVal waitforreturn As Boolean, ByVal tcp As TcpClient, ByVal data As Object) As Object
            Dim ns = tcp.GetStream()
            Dim bf = New BinaryFormatter()
            Dim st = New MemoryStream()
            bf.Serialize(st, data)
            Dim databytes As Byte() = st.GetBuffer()
            Dim len As Integer = databytes.Length
            Dim bw = New BinaryWriter(ns)
            bw.Write(len)
            bw.Flush()
            Dim offset As Integer = 0
            Dim size As Integer = Config.CHUNK_SIZE

            While len > 0
                If len < size Then size = len
                ns.Write(databytes, offset, size)
                len -= size
                offset += size
            End While

            ns.Flush()

            If waitforreturn Then
                Return RecievePacket(tcp)
            End If

            Return Nothing
        End Function
    End Class
#End Region

#Region "NetworkClient"
    Friend Class NetworkClient
        Private ReadOnly _host As String = "127.0.0.1"
        Private ReadOnly _portNum As Integer = Config.PORT_NUM
        Private _tcpclient As TcpClient

        Public Sub New(ByVal host As String, ByVal port As Integer)
            _host = host
            _portNum = port
        End Sub

        Public ReadOnly Property Connected As Boolean
            Get
                Return _tcpclient.Connected
            End Get
        End Property

        Public Function Connect() As Boolean
            Try

                If _tcpclient IsNot Nothing Then
                    Dim ns As NetworkStream = _tcpclient.GetStream()
                    If ns IsNot Nothing Then ns.Close()
                    If _tcpclient IsNot Nothing Then _tcpclient.Close()
                End If

                _tcpclient = New TcpClient()
                _tcpclient.SendBufferSize = Config.CHUNK_SIZE
                _tcpclient.SendTimeout = Config.SEND_TIMEOUT
                _tcpclient.ReceiveTimeout = _tcpclient.SendTimeout
                _tcpclient.ReceiveBufferSize = _tcpclient.SendBufferSize
                Dim res As IAsyncResult = _tcpclient.BeginConnect(_host, _portNum, Nothing, Nothing)
                Dim ok As Boolean = res.AsyncWaitHandle.WaitOne(Config.CONNNECT_TIMEOUT, True)

                If Not ok Then
                    Dim ns As NetworkStream = _tcpclient.GetStream()
                    If ns IsNot Nothing Then ns.Close()
                    If _tcpclient IsNot Nothing Then _tcpclient.Close()
                End If

                Return ok
            Catch ex As Exception
                log.Debug("NetworkClient", "connect ex =" & ex.Message)
                Return False
            End Try
        End Function

        Public Sub Close()
            Try

                If _tcpclient IsNot Nothing Then

                    If _tcpclient.Connected Then
                        Dim ns As NetworkStream = _tcpclient.GetStream()
                        If ns IsNot Nothing Then ns.Close()
                    End If

                    _tcpclient.Close()
                End If

            Catch ex As Exception
                log.Debug("NetworkClient", "close tcp ex = " & ex.Message)
            End Try
        End Sub

        Public Function Send(ByVal data As Object) As Object
            Try
                If _tcpclient.Connected = False Then Connect()

                If _tcpclient.Connected Then
                    Return NetworkCommon.SendPacket(True, _tcpclient, data)
                End If

                Return Nothing
            Catch ex As Exception
                log.Debug("NetworkClient", "send ex =" & ex.Message)
                Dim ns As NetworkStream = _tcpclient.GetStream()
                If ns IsNot Nothing Then ns.Close()
                If _tcpclient IsNot Nothing Then _tcpclient.Close()
                Return Nothing
            End Try
        End Function
    End Class
#End Region

#Region "NetworkServer"
    Friend Class NetworkServer
#Region "ClientService"
        Friend Class ClientService
            Private ConnectionPool As ClientConnectionPool
            Private ContinueProcess As Boolean = False
            Private ThreadTask As Threading.Thread() = New Threading.Thread(Config.NUM_OF_THREADS - 1) {}

            Public Sub New(ByVal ConnectionPool As ClientConnectionPool)
                Me.ConnectionPool = ConnectionPool
            End Sub

            Public Sub Start()
                ContinueProcess = True

                For i As Integer = 0 To ThreadTask.Length - 1
                    ThreadTask(i) = New Threading.Thread(New ThreadStart(AddressOf Process))
                    If i > 0 Then ThreadTask(i).IsBackground = True
                    ThreadTask(i).Start()
                Next
            End Sub

            Private Sub Process()
                While ContinueProcess
                    Dim client As ClientHandler = Nothing

                    SyncLock ConnectionPool.SyncRoot
                        If ConnectionPool.Count > 0 Then client = ConnectionPool.Dequeue()
                    End SyncLock

                    If client IsNot Nothing Then

                        If client.Alive Then
                            client.Process()
                            ConnectionPool.Enqueue(client)
                        Else
                            client.Close()
                        End If
                    End If

                    Thread.Sleep(1)
                End While
            End Sub

            Public Sub [Stop]()
                ContinueProcess = False

                While ConnectionPool.Count > 0
                    Dim client As ClientHandler = ConnectionPool.Dequeue()
                    client.Close()
                End While

                For i As Integer = 0 To ThreadTask.Length - 1
                    If ThreadTask(i) IsNot Nothing AndAlso ThreadTask(i).IsAlive Then ThreadTask(i).Abort()
                Next
            End Sub
        End Class
#End Region

#Region "ClientHandler"
        Friend Class ClientHandler
            Public _ProcessPayload As ProcessPayload
            Private bytes As Byte()
            Private ClientSocket As TcpClient
            Private ms As MemoryStream = New MemoryStream()
            Private networkStream As NetworkStream
            Public ClientGuid As Guid = Guid.NewGuid()

            Public Sub New(ByVal ClientSocket As TcpClient, ByVal payloadhandler As ProcessPayload)
                ClientSocket.ReceiveTimeout = Config.SEND_TIMEOUT
                ClientSocket.SendTimeout = ClientSocket.ReceiveTimeout
                Me.ClientSocket = ClientSocket
                networkStream = ClientSocket.GetStream()
                bytes = New Byte(ClientSocket.ReceiveBufferSize - 1) {}
                _ProcessPayload = payloadhandler
            End Sub

            Public ReadOnly Property Alive As Boolean
                Get
                    Return (If(ClientSocket IsNot Nothing, ClientSocket.Connected, False))
                End Get
            End Property

            Public Sub Process()
                Try
                    If ClientSocket.Available = 0 Then Return
                    Dim obj As Object = NetworkCommon.RecievePacket(ClientSocket)

                    If obj IsNot Nothing Then

                        If _ProcessPayload IsNot Nothing Then
                            Dim oo As Object = _ProcessPayload(obj)
                            NetworkCommon.SendPacket(False, ClientSocket, oo)
                            Return
                        End If
                    End If

                Catch __unusedIOException1__ As IOException
                    Close()
                Catch __unusedSocketException2__ As SocketException
                    Close()
                End Try
            End Sub

            Public Sub Close()
                If networkStream IsNot Nothing Then networkStream.Close()
                If ClientSocket IsNot Nothing Then ClientSocket.Close()
                networkStream.Dispose()
                ClientSocket = Nothing
            End Sub
        End Class
#End Region

#Region "ClientConnectionPool"
        Friend Class ClientConnectionPool
            Private SyncdQ As Queue = Queue.Synchronized(New Queue())

            Public ReadOnly Property Count As Integer
                Get
                    Return SyncdQ.Count
                End Get
            End Property

            Public ReadOnly Property SyncRoot As Object
                Get
                    Return SyncdQ.SyncRoot
                End Get
            End Property

            Public Sub Enqueue(ByVal client As ClientHandler)
                SyncdQ.Enqueue(client)
            End Sub

            Public Function Dequeue() As ClientHandler
                Return CType((SyncdQ.Dequeue()), ClientHandler)
            End Function
        End Class
#End Region

        Private _ClientService As ClientService
        Private _ConnectionPool As ClientConnectionPool = New ClientConnectionPool()
        Private _Listener As TcpListener
        Private _portNum As Integer = Config.PORT_NUM
        Private _processPayload As ProcessPayload

        Public Sub New(ByVal port As Integer, ByVal callback As ProcessPayload)
            _portNum = port
            _processPayload = callback
        End Sub

        Public Sub [Stop]()
            _ClientService.[Stop]()
            _Listener.[Stop]()
        End Sub

        Public Sub Start()
            Try
                _Listener = New TcpListener(IPAddress.Any, _portNum)
                _Listener.Start()
                _ClientService = New ClientService(_ConnectionPool)
                _ClientService.Start()
                _Listener.BeginAcceptTcpClient(AddressOf AcceptCallback, _Listener)
            Catch __unusedException1__ As Exception
            End Try
        End Sub

        Private Sub AcceptCallback(ByVal ar As IAsyncResult)
            Try
                Dim server = CType(ar.AsyncState, TcpListener)
                Dim tcp As TcpClient = server.EndAcceptTcpClient(ar)
                tcp.ReceiveBufferSize = Config.CHUNK_SIZE
                tcp.SendTimeout = Config.SEND_TIMEOUT
                tcp.ReceiveTimeout = tcp.SendTimeout
                tcp.SendBufferSize = Config.CHUNK_SIZE
                server.BeginAcceptTcpClient(AddressOf AcceptCallback, server)

                If tcp IsNot Nothing Then
                    Dim ch = New ClientHandler(tcp, _processPayload)
                    _ConnectionPool.Enqueue(ch)
                End If

            Catch
            End Try
        End Sub
    End Class
#End Region

End Namespace

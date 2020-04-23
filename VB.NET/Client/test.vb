Imports System
Imports System.Collections.Generic
Imports System.Text
'Imports Networking
Imports System.Threading


Namespace tcptest
    Module Program
        Private Const PORT As Integer = 99
        Private SERVER_IP As String = "127.0.0.1"

        Public Function Main(ByVal args As String()) As Integer
            Console.WriteLine("(S)erver mode     (C)lient mode     (A)utomatic test")
            Dim k As ConsoleKey = Console.ReadKey().Key

            If k = ConsoleKey.S Then
                servermode()
            ElseIf k = ConsoleKey.C Then
                Console.WriteLine("Input Server IP: (default 127.0.0.1):")
                Dim ret As String = Console.ReadLine
                If ret <> String.Empty Then
                    SERVER_IP = ret
                End If
                clientmode()
            Else
                autotest()
            End If

            Return 0
        End Function

        Private Sub servermode()
            Console.WriteLine()
            Dim ns As NetworkServer = New NetworkServer(PORT, New ProcessPayload(AddressOf serverprocess))
            Console.WriteLine(vbLf & "Hit 'ctrl+c' to end server")
            ns.Start()
            Dim _done As Boolean = False

            While Not _done
                Thread.Sleep(1)
            End While

            ns.[Stop]()
            Return
        End Sub

        Private Sub autotest()
            Dim cont As Boolean = True
            Dim i As Integer = 0
            Dim nc As NetworkClient = New NetworkClient(SERVER_IP, PORT)

            If nc.Connect() = False Then
                Console.WriteLine("Server not available")
                Return
            End If

            Dim p As Packet = New Packet()

            While cont
                p.Message = "" & Math.Min(System.Threading.Interlocked.Increment(i), i - 1)
                Dim ret As ReturnPacket = TryCast(nc.Send(p), ReturnPacket)

                If ret IsNot Nothing Then
                    Dim returndata As String = ret.Message
                    Console.Write("This is what the host returned to you: {0}" & vbCr, returndata)
                End If

                Thread.Sleep(1)
            End While
        End Sub

        Private Sub clientmode()
            Console.WriteLine()
            Dim nc As NetworkClient = New NetworkClient(SERVER_IP, PORT)

            If nc.Connect() = False Then
                Console.WriteLine("Server not available")
                Return
            End If

            Dim DataToSend As String = ""

            While DataToSend <> "quit"
                Console.WriteLine(vbLf & "Type a text to be sent:")
                Dim sb As StringBuilder = New StringBuilder()
                DataToSend = Console.ReadLine()
                If DataToSend.Length = 0 Then Continue While
                Dim packet As Packet = New Packet()
                packet.Message = DataToSend

                Try
                    nc.Connect()
                    Dim ret As ReturnPacket = TryCast(nc.Send(packet), ReturnPacket)

                    If ret IsNot Nothing Then
                        Dim returndata As String = ret.Message
                        Console.WriteLine("This is what the host returned to you: " & vbCrLf & "{0}", returndata)
                    Else
                        Console.WriteLine("return null")
                    End If

                Catch ex As Exception
                    Console.WriteLine(ex)
                    Return
                End Try
            End While

            nc.Close()
        End Sub

        Private Function serverprocess(ByVal data As Object) As Object
            Dim dp As Packet = TryCast(data, Packet)
            If dp IsNot Nothing Then Return HandlePacket(dp)
            Console.WriteLine("message not recognized")
            Return New ReturnPacket()
        End Function

        Public count As Integer = 0
        Public dt As DateTime = DateTime.Now

        Private Function HandlePacket(ByVal dp As Packet) As Object
            Dim ret As ReturnPacket = New ReturnPacket()

            If dp.SessionGuid = Guid.Empty Then
                ' Authenticate username and password possibly with LDAP server
            Else
                '// check sessionguid valid -> if not return failed
            End If

            ret.OK = True
            ret.Message = "your msg : " & dp.Message & vbCrLf & "return from server " + DateTime.Now
            count += 1

            If DateTime.Now.Subtract(dt).TotalMilliseconds > 10000 Then
                Console.WriteLine("count in 10 secs = " & count)
                count = 0
                dt = DateTime.Now
            End If

            Return ret
        End Function
    End Module
End Namespace

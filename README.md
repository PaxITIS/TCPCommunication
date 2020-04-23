# TCPCommunication
[![Build status](https://ci.appveyor.com/api/projects/status/78g2a73dk4a9n5ox?svg=true)](https://ci.appveyor.com/project/PaxITIS/tcpcommunication)
![GitHub release (latest by date)](https://img.shields.io/github/v/release/PaxITIS/TCPCommunication)
![GitHub repo size](https://img.shields.io/github/repo-size/PaxITIS/TCPCommunication)
![GitHub](https://img.shields.io/github/license/PaxITIS/TCPCommunication)

This is a demo project, both C# and VB.NET, to implement one communication between front-end application and Service application created by [Mehdi Gholam](https://www.codeproject.com/Articles/156765/WCF-Killer) in C# and ported by Omar Pasini to VB:NET

## Screenshot ##

![](images/screen.jpg)


## Introduction ##

This is a piece of code that I have been using for a long time, which is simple to use, doesn't impose any additional work, and gets the job done for client server communications using the TCP protocol. You just include a single file in your project and get all the benefits.

## BackGround ##

This code came about after using named pipe communication for a long time and having the following problems, although arguably named pipes are faster at data transfer:

Named pipes require authentication on connection to a computer, which is very limiting in non-domain computers and requires the user to enter a valid system user and password on the destination computer to even work.
The named pipe implementation I was using was very large, code wise.
So for the above reasons, I set about creating a replacement which would fit the following requirements:

Simple to use in code, with minimal configuration.
Multi-threaded so it can scale to hundreds of simultaneous users.
Does not impose additional code "proxies" and "contracts" and should work with normal serializable objects.
Does not require computer level authentication, which is implicit in using the TCP protocol as it is handled at a lower level than the OS (unlike named pipes).
Flexibility to implement your own authentication on top of it.
Handle large data transfer objects, e.g., 3 MB data packets.

# Using the code #

#### Language:C# ####
```
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

```


#### Language: VB.NET ####
```
Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace tcptest
    <Serializable()>
    Public Class Packet
        Public Sub New()
            data = New Byte(3145727) {}
        End Sub

        Public Property data As Byte()
        Public Property Message As String
        Public Property Username As String
        Public Property Password As String
        Public Property SessionGuid As Guid

        Public Overloads Function ToString() As String
            Return Message
        End Function
    End Class

    <Serializable()>
    Public Class ReturnPacket
        Public Property OK As Boolean
        Public Property Message As String
    End Class
End Namespace

```

As you can see, the data structures are normal classes and not proxies; you also must put the Serializble attribute on the class so the .NET serializer can work with it.

For the server code, you do the following:

#### Language:C# ####
```
NetworkServer ns = new NetworkServer(99, new ProcessPayload(serverprocess));
// will listen on port 99

ns.Start();

private static object serverprocess(object data)
{
    Packet dp = data as Packet;
    if (dp != null)
        return HandlePacket(dp);
   
    Console.WriteLine("message not recognized");
    return new ReturnPacket();
}

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
    return ret;
}

```

#### Language:VB:NET ####
```
Dim ns As NetworkServer = New NetworkServer(PORT, New ProcessPayload(AddressOf serverprocess))
// will listen on port 99

ns.Start()

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
```

As you can see, it is pretty straightforward and simple.

For the client side, you do the following:

#### Language:C# ####
```
NetworkClient nc = new NetworkClient("127.0.0.1", 99);
// send to local host on port 99

nc.Connect();
Packet p = new Packet();
ReturnPacket ret = nc.Send(p) as ReturnPacket;

```

#### Language:VB.NET ####
```
Dim nc As NetworkClient = New NetworkClient("127.0.0.1", 99)
// send to local host on port 99

nc.Connect()
Dim packet As Packet = New Packet()
packet.Message = "Hello guys"
Dim ret As ReturnPacket = TryCast(nc.Send(packet), ReturnPacket)

```

## What is up to you ##

The following is up to you to implement yourself as you see fit for your own application:

Data Packet Definitions: what you want to send over the wire
Authentication: handling authentication is up to you if you need it
Session Management: related to authentication is session management

## Performance tests ##
Here is the performance test results done on an AMD K625 1.5ghz, 4GB RAM, Windows 7 Home, win rating of 3.9 Notebook (CPU usage above 88%):

| Number of simultaneous clients  | Data packet size | Chunk size	| Request in 10 secs |
| ------------- | ------------- | ------------- | ------------- |
| 5  | ~12kb  | 32kb  | 12681  |
| 5  | ~12kb  | 16kb  |  12291 |
| 5  | ~12kb  | 4kb  | 11089  |
| 5  | ~3mb  | 4kb  | 141  |
| 5  | ~3mb  | 16kb  | 207  |
| 5  | ~3mb  | 32kb  | 220  |

As you can see, the best performance is with a CHUNK_SIZE of 32 KB for both small and large data packets. You can go higher, e.g., 64 KB, but you get diminishing returns, and possibly (I don't know for sure), you may have problems with some switches and routers in the network as they might block large packet sizes.

## Points of interest ##
I have put the network server code in a NETSERVER conditional compilation block so you will have to add that to your project definition. There is a Config class in the NetWorkClient.cs file which has some predefined options that I have tweaked to maximum performance; one note worthy option is NUM_OF_THREADS which is how many threads the server creates to handle a request. The default is 10, which should be enough for most applications; I have used it for handling 50+ clients applications in real world circumstances.

## What you can do at home or further directions ##
Below is a list of possibilities you can do at home if you feel adventurous:

- Progress Events: events to publish the progress of data for the client application to hook on to and display UI for.
- Encrypted Communication: using SslStream instead of NetworkStream for encrypted data transfer.
- LDAP Authentication of User: check the username and password embedded in the packet with an LDAP server (see the comments in the test app).
- Session Management: handle client sessions and authenticated connections (see the comments in the test app).
- Maybe replace BinarySerializer: for greater performance, you could look at the protocol serializer from Google which is incredibly fast and compact.

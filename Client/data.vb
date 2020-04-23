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

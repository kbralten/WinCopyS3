Namespace WinCopyS3
    Public Interface ILogger
        Sub Info(message As String)
        Sub [Error](message As String)
        ReadOnly Property Recent As IEnumerable(Of String)
    End Interface

    Public Class InMemoryLogger
        Implements ILogger

        Private ReadOnly _messages As New List(Of String)()
        Private ReadOnly _lockObj As New Object()

        Public Sub Info(message As String) Implements ILogger.Info
            Add($"INFO  {DateTime.Now:HH:mm:ss}  {message}")
        End Sub

        Public Sub [Error](message As String) Implements ILogger.Error
            Add($"ERROR {DateTime.Now:HH:mm:ss}  {message}")
        End Sub

        Private Sub Add(msg As String)
            SyncLock _lockObj
                _messages.Add(msg)
                If _messages.Count > 100 Then
                    _messages.RemoveAt(0)
                End If
            End SyncLock
        End Sub

        Public ReadOnly Property Recent As IEnumerable(Of String) Implements ILogger.Recent
            Get
                SyncLock _lockObj
                    Return _messages.TakeLast(10).ToArray()
                End SyncLock
            End Get
        End Property
    End Class
End Namespace

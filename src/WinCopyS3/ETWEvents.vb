Imports System.Diagnostics.Tracing

Namespace WinCopyS3
    ' Explicit ETW provider GUID so external tools can subscribe by GUID.
    ' Generate or choose a stable GUID for this provider. We'll expose it as ProviderGuid constant.
    <EventSource(Name:="WinCopyS3-Events")>
    Public NotInheritable Class ETWEvents
        Inherits EventSource

        ' Shared instance used by the app
        Public Shared ReadOnly Log As ETWEvents = New ETWEvents()

        ' Expose the provider GUID discovered from the EventSource instance at runtime
        Public Shared ReadOnly ProviderGuid As String = Log.Guid.ToString()

        Private Sub New()
        End Sub

        <[Event](1, Level:=EventLevel.Informational, Message:="Start watching: {0}")>
        Public Sub StartWatching(path As String)
            If IsEnabled() Then WriteEvent(1, path)
        End Sub

        <[Event](2, Level:=EventLevel.Informational, Message:="Stop watching")>
        Public Sub StopWatching()
            If IsEnabled() Then WriteEvent(2)
        End Sub

        <[Event](3, Level:=EventLevel.Informational, Message:="File event: {0} {1}")>
        Public Sub FileEvent(eventType As String, path As String)
            If IsEnabled() Then WriteEvent(3, eventType, path)
        End Sub

        <[Event](4, Level:=EventLevel.Informational, Message:="Renamed: {0} -> {1}")>
        Public Sub FileRenamed(oldPath As String, newPath As String)
            If IsEnabled() Then WriteEvent(4, oldPath, newPath)
        End Sub

        <[Event](5, Level:=EventLevel.Verbose, Message:="Debounce scheduled for {0}")>
        Public Sub DebounceScheduled(path As String)
            If IsEnabled() Then WriteEvent(5, path)
        End Sub

        <[Event](13, Level:=EventLevel.Informational, Message:="Cache rebuild started")>
        Public Sub CacheRebuildStarted()
            If IsEnabled() Then WriteEvent(13)
        End Sub

        <[Event](14, Level:=EventLevel.Informational, Message:="Cache rebuild completed: {0} entries")>
        Public Sub CacheRebuildCompleted(count As Integer)
            If IsEnabled() Then WriteEvent(14, count)
        End Sub

        <[Event](15, Level:=EventLevel.Informational, Message:="Recent activity: {0}")>
        Public Sub RecentActivity(msg As String)
            If IsEnabled() Then WriteEvent(15, msg)
        End Sub

        <[Event](6, Level:=EventLevel.Informational, Message:="File ready: {0}")>
        Public Sub FileReady(path As String)
            If IsEnabled() Then WriteEvent(6, path)
        End Sub

        <[Event](7, Level:=EventLevel.Informational, Message:="Upload started: {0} -> {1}")>
        Public Sub UploadStarted(path As String, key As String)
            If IsEnabled() Then WriteEvent(7, path, key)
        End Sub

        <[Event](8, Level:=EventLevel.Informational, Message:="Upload completed: {0} -> {1}")>
        Public Sub UploadCompleted(path As String, key As String)
            If IsEnabled() Then WriteEvent(8, path, key)
        End Sub

        <[Event](9, Level:=EventLevel.Error, Message:="Upload failed: {0} -> {1} : {2}")>
        Public Sub UploadFailed(path As String, key As String, err As String)
            If IsEnabled() Then WriteEvent(9, path, key, err)
        End Sub

        <[Event](10, Level:=EventLevel.Warning, Message:="Retry enqueued: {0}")>
        Public Sub RetryEnqueued(key As String)
            If IsEnabled() Then WriteEvent(10, key)
        End Sub

        <[Event](11, Level:=EventLevel.Informational, Message:="Retry attempt: {0} attempt {1}")>
        Public Sub RetryAttempt(key As String, attempt As Integer)
            If IsEnabled() Then WriteEvent(11, key, attempt)
        End Sub

        <[Event](12, Level:=EventLevel.Error, Message:="General error: {0}")>
        Public Sub GeneralError(msg As String)
            If IsEnabled() Then WriteEvent(12, msg)
        End Sub
    End Class
End Namespace

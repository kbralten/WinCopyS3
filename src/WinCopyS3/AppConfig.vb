Imports System.IO
Imports System.Text.Json

Namespace WinCopyS3
    Public Class AppConfig
        Public Property LocalFolder As String = String.Empty
        Public Property BucketName As String = String.Empty
        ' Optional: custom S3 service URL (for MinIO/local testing). If empty, SDK uses region.
        Public Property ServiceURL As String = String.Empty
        Public Property Region As String = "us-east-1"
        Public Property AccessKeyId As String = String.Empty
        Public Property SecretAccessKey As String = String.Empty
    ' Controls whether the application should attempt to rebuild the local cache from S3 on normal startup
    Public Property RebuildCacheOnStartup As Boolean = True
    ' Controls whether the smoketest run should attempt to rebuild the cache from S3 prior to testing
    Public Property RebuildCacheOnSmoketest As Boolean = True

        Public ReadOnly Property IsConfigured As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace(LocalFolder) AndAlso
                       Not String.IsNullOrWhiteSpace(BucketName) AndAlso
                       Not String.IsNullOrWhiteSpace(Region) AndAlso
                       Not String.IsNullOrWhiteSpace(AccessKeyId) AndAlso
                       Not String.IsNullOrWhiteSpace(SecretAccessKey)
            End Get
        End Property

        Public Shared Function GetConfigPath() As String
            Dim dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinCopyS3")
            Directory.CreateDirectory(dir)
            Return Path.Combine(dir, "config.json")
        End Function

        Public Sub Save()
            Dim path = GetConfigPath()
            Dim json = JsonSerializer.Serialize(Me, New JsonSerializerOptions With {.WriteIndented = True})
            File.WriteAllText(path, json)
        End Sub

        Public Shared Function Load() As AppConfig
            Try
                Dim path = GetConfigPath()
                If File.Exists(path) Then
                    Dim json = File.ReadAllText(path)
                    Dim cfg = JsonSerializer.Deserialize(Of AppConfig)(json)
                    If cfg IsNot Nothing Then Return cfg
                End If
            Catch
            End Try
            Return New AppConfig()
        End Function
    End Class
End Namespace

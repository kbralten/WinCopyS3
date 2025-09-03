Imports System.IO
Imports System.Threading.Tasks
Imports Amazon
Imports Amazon.S3
Imports Amazon.S3.Model
Imports Amazon.Runtime

Namespace WinCopyS3
    Public Class CacheStore
        Private ReadOnly _logger As ILogger
        Private ReadOnly _cachePath As String
        Private ReadOnly _set As HashSet(Of String)

        Public Sub New(logger As ILogger)
            _logger = logger
            Dim dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinCopyS3")
            Directory.CreateDirectory(dir)
            _cachePath = Path.Combine(dir, "cache.txt")
            _set = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Load()
        End Sub

        Public Function CacheFileExists() As Boolean
            Return File.Exists(_cachePath)
        End Function

        Public Function IsEmpty() As Boolean
            Return _set.Count = 0
        End Function

        ''' <summary>
        ''' Rebuilds the local cache by listing all objects in the target bucket specified by <paramref name="cfg"/>.
        ''' This will populate the in-memory set and write the cache file atomically.
        ''' </summary>
        Public Async Function RebuildFromS3Async(cfg As AppConfig) As Task
            Try
                If cfg Is Nothing OrElse String.IsNullOrWhiteSpace(cfg.BucketName) Then
                    _logger.Info("RebuildFromS3Async: missing config or bucket name")
                    Return
                End If

                Dim creds = New BasicAWSCredentials(cfg.AccessKeyId, cfg.SecretAccessKey)
                Dim s3Config As AmazonS3Config = Nothing
                If Not String.IsNullOrWhiteSpace(cfg.ServiceURL) Then
                    s3Config = New AmazonS3Config() With {
                        .ServiceURL = cfg.ServiceURL,
                        .ForcePathStyle = True
                    }
                Else
                    s3Config = New AmazonS3Config() With {
                        .RegionEndpoint = RegionEndpoint.GetBySystemName(cfg.Region)
                    }
                End If

                Dim allKeys As New List(Of String)()
                Using client As New AmazonS3Client(creds, s3Config)
                    Dim req As New ListObjectsV2Request() With {
                        .BucketName = cfg.BucketName,
                        .MaxKeys = 1000
                    }

                    Do
                        Dim resp = Await client.ListObjectsV2Async(req)
                        For Each obj In resp.S3Objects
                            If Not String.IsNullOrWhiteSpace(obj.Key) Then
                                allKeys.Add(obj.Key)
                            End If
                        Next
                        req.ContinuationToken = resp.NextContinuationToken
                    Loop While Not String.IsNullOrWhiteSpace(req.ContinuationToken)
                End Using

                ' Update in-memory set and persist
                SyncLock _set
                    _set.Clear()
                    For Each k In allKeys
                        _set.Add(k)
                    Next
                End SyncLock

                Try
                    ' Write to a temp file first, then atomically replace the cache file to avoid partial reads
                    Dim tmp = _cachePath & ".tmp"
                    File.WriteAllLines(tmp, allKeys)
                    If File.Exists(_cachePath) Then
                        File.Replace(tmp, _cachePath, Nothing)
                    Else
                        File.Move(tmp, _cachePath)
                    End If
                    _logger.Info($"Rebuilt cache with {allKeys.Count} entries from bucket '{cfg.BucketName}'")
                Catch ex As Exception
                    _logger.Error($"Failed to write cache file: {ex.Message}")
                End Try
            Catch ex As Exception
                _logger.Error($"RebuildFromS3Async failed: {ex.Message}")
            End Try
        End Function

        Private Sub Load()
            If File.Exists(_cachePath) Then
                For Each line In File.ReadAllLines(_cachePath)
                    If Not String.IsNullOrWhiteSpace(line) Then _set.Add(line.Trim())
                Next
            End If
        End Sub

        Public Function Contains(key As String) As Boolean
            Return _set.Contains(key)
        End Function

        Public Sub Add(key As String)
            SyncLock _set
                If _set.Add(key) Then
                    Try
                        ' Append atomically: write existing contents + new key to a temp file and replace
                        Dim tmp = _cachePath & ".tmp"
                        If File.Exists(_cachePath) Then
                            ' read existing and write to temp along with the new key
                            Dim existing = File.ReadAllLines(_cachePath)
                            Using sw As New IO.StreamWriter(tmp, False)
                                For Each l In existing
                                    sw.WriteLine(l)
                                Next
                                sw.WriteLine(key)
                            End Using
                            File.Replace(tmp, _cachePath, Nothing)
                        Else
                            ' no existing file; just write the key
                            File.WriteAllText(tmp, key & Environment.NewLine)
                            File.Move(tmp, _cachePath)
                        End If
                    Catch ex As Exception
                        _logger.Error($"Failed to append to cache file: {ex.Message}")
                    End Try
                End If
            End SyncLock
        End Sub
    End Class
End Namespace

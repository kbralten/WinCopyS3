Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports Amazon
Imports Amazon.S3
Imports Amazon.S3.Model
Imports Amazon.Runtime

Namespace WinCopyS3
    Public Class StatusChangedEventArgs
        Inherits EventArgs
        Public ReadOnly Property StatusText As String
        Public Sub New(text As String)
            StatusText = text
        End Sub
    End Class

    Public Interface IFileWatcherService
        Event StatusChanged As EventHandler(Of StatusChangedEventArgs)
        Sub StartWatching()
        Sub StopWatching()
    End Interface

    Public Interface IS3Uploader
        Function UploadAsync(localPath As String, bucket As String, key As String, cfg As AppConfig) As Task
    End Interface

    Public Class FileWatcherService
        Implements IFileWatcherService

        Private ReadOnly _logger As ILogger
        Private ReadOnly _config As AppConfig
        Private ReadOnly _cache As CacheStore
        Private ReadOnly _uploader As IS3Uploader
        Private _watcher As FileSystemWatcher
        ' Retry queue for files that timed out waiting for readiness
        Private ReadOnly _retryQueue As New Queue(Of String)()
        Private ReadOnly _queueLock As New Object()
        Private _queueCts As CancellationTokenSource
        Private _queueTask As Task
        Private ReadOnly _retryAttempts As New Dictionary(Of String, Integer)()
        Private ReadOnly _debounceMap As New Dictionary(Of String, DateTime)()
        Private ReadOnly _debounceLock As New Object()
        Private _debounceIntervalMs As Integer = 1000

        Public Event StatusChanged As EventHandler(Of StatusChangedEventArgs) Implements IFileWatcherService.StatusChanged

        Public Sub New(logger As ILogger, config As AppConfig, cache As CacheStore, uploader As IS3Uploader)
            _logger = logger
            _config = config
            _cache = cache
            _uploader = uploader
        End Sub

        Public Sub StartWatching() Implements IFileWatcherService.StartWatching
            If String.IsNullOrWhiteSpace(_config.LocalFolder) OrElse Not Directory.Exists(_config.LocalFolder) Then
                RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Invalid folder"))
                Return
            End If

            _watcher = New FileSystemWatcher(_config.LocalFolder)
            _watcher.IncludeSubdirectories = True
            _watcher.NotifyFilter = NotifyFilters.FileName Or NotifyFilters.DirectoryName Or NotifyFilters.CreationTime
            AddHandler _watcher.Created, AddressOf OnCreated
            AddHandler _watcher.Changed, AddressOf OnChanged
            _watcher.EnableRaisingEvents = True
            ' start retry queue processor
            _queueCts = New CancellationTokenSource()
            _queueTask = Task.Run(Function() ProcessRetryQueueAsync(_queueCts.Token))
            ' Scan existing files on startup
            Task.Run(Function() ScanExistingFilesAsync())
            _logger.Info("Monitoring started")
            RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Monitoring"))
        End Sub

        Public Sub StopWatching() Implements IFileWatcherService.StopWatching
            If _watcher IsNot Nothing Then
                _watcher.EnableRaisingEvents = False
                RemoveHandler _watcher.Created, AddressOf OnCreated
                RemoveHandler _watcher.Changed, AddressOf OnChanged
                _watcher.Dispose()
                _watcher = Nothing
                _logger.Info("Monitoring stopped")
                RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Idle"))
            End If
            If _queueCts IsNot Nothing Then
                _queueCts.Cancel()
                Try
                    _queueTask?.Wait(2000)
                Catch ex As Exception
                    ' ignore
                End Try
                _queueCts = Nothing
                _queueTask = Nothing
            End If
        End Sub

        Private Sub OnCreated(sender As Object, e As FileSystemEventArgs)
            Try
                ' Skip directories, process files only
                If Directory.Exists(e.FullPath) Then Return

                ' Debounce and schedule processing; actual upload logic will re-check cache and readiness
                DebounceProcess(e.FullPath)
            Catch ex As Exception
                _logger.Error($"OnCreated handler error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnChanged(sender As Object, e As FileSystemEventArgs)
            Try
                If Directory.Exists(e.FullPath) Then Return
                DebounceProcess(e.FullPath)
            Catch ex As Exception
                _logger.Error($"OnChanged handler error: {ex.Message}")
            End Try
        End Sub

        Private Sub DebounceProcess(fullPath As String)
            SyncLock _debounceLock
                _debounceMap(fullPath) = DateTime.UtcNow
            End SyncLock

            Task.Run(Async Function()
                         Await Task.Delay(_debounceIntervalMs)
                         Dim last As DateTime
                         SyncLock _debounceLock
                             If Not _debounceMap.TryGetValue(fullPath, last) Then Return
                             If (DateTime.UtcNow - last).TotalMilliseconds < _debounceIntervalMs Then Return
                             _debounceMap.Remove(fullPath)
                         End SyncLock
                         Try
                             Await EnsureFileReadyAndUploadAsync(fullPath)
                         Catch ex As Exception
                             _logger.Error($"DebounceProcess error for {fullPath}: {ex.Message}")
                         End Try
                     End Function)
        End Sub

        Private Async Function EnsureFileReadyAndUploadAsync(fullPath As String) As Task(Of Boolean)
            Try
                ' Skip directories
                If Directory.Exists(fullPath) Then Return True

                If Not File.Exists(fullPath) Then
                    _logger.Info($"File not found when attempting upload: {fullPath}")
                    Return True
                End If

                Dim relative As String = Path.GetRelativePath(_config.LocalFolder, fullPath).Replace("\\", "/")
                If _cache.Contains(relative) Then
                    Return True
                End If

                ' Wait for file to be written and unlocked before uploading.
                Const maxWaitMs As Integer = 10000 ' total timeout (10s)
                Const pollIntervalMs As Integer = 500
                Dim waited As Integer = 0
                Dim fileReady As Boolean = False

                While waited < maxWaitMs
                    Try
                        If Not File.Exists(fullPath) Then
                            _logger.Info($"File disappeared before upload: {relative}")
                            Return True
                        End If

                        Dim fi As New FileInfo(fullPath)
                        If fi.Length = 0 Then
                            _logger.Info($"Waiting for non-zero size: {relative}")
                        Else
                            Using fs As FileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.None)
                                ' if we can open it exclusively, it's ready
                            End Using
                            fileReady = True
                            Exit While
                        End If
                    Catch ioex As IOException
                        _logger.Info($"File locked, retrying: {relative} ({ioex.Message})")
                    Catch ex As Exception
                        _logger.Error($"Error while checking file readiness for {relative}: {ex.Message}")
                        Return True
                    End Try

                    Await Task.Delay(pollIntervalMs)
                    waited += pollIntervalMs
                End While

                If Not fileReady Then
                    _logger.Error($"Timed out waiting for file to become ready: {relative}")
                    ' enqueue for retry
                    EnqueueRetry(relative)
                    RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Error"))
                    Return False
                End If

                _logger.Info($"Uploading {relative}...")
                RaiseEvent StatusChanged(Me, New StatusChangedEventArgs($"Uploading {Path.GetFileName(fullPath)}"))

                Await _uploader.UploadAsync(fullPath, _config.BucketName, relative, _config)
                _cache.Add(relative)
                _logger.Info($"Uploaded {relative}")
                RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Monitoring"))
                Return True
            Catch ex As Exception
                _logger.Error($"Upload failed: {ex.Message}")
                RaiseEvent StatusChanged(Me, New StatusChangedEventArgs("Error"))
                Return True
            End Try
        End Function

        Private Sub EnqueueRetry(relative As String)
            SyncLock _queueLock
                _retryQueue.Enqueue(relative)
                If Not _retryAttempts.ContainsKey(relative) Then
                    _retryAttempts(relative) = 0
                End If
            End SyncLock
        End Sub

        Private Async Function ProcessRetryQueueAsync(ct As CancellationToken) As Task
            Const maxAttempts As Integer = 5
            Try
                While Not ct.IsCancellationRequested
                    Dim toProcess As String = Nothing
                    SyncLock _queueLock
                        If _retryQueue.Count > 0 Then
                            toProcess = _retryQueue.Dequeue()
                        End If
                    End SyncLock

                    If toProcess Is Nothing Then
                        Await Task.Delay(1000, ct)
                        Continue While
                    End If

                    Dim attempts As Integer = 0
                    SyncLock _queueLock
                        attempts = _retryAttempts(toProcess)
                        _retryAttempts(toProcess) = attempts + 1
                    End SyncLock

                    If attempts >= maxAttempts Then
                        _logger.Error($"Dropping {toProcess} after {attempts} attempts")
                        SyncLock _queueLock
                            _retryAttempts.Remove(toProcess)
                        End SyncLock
                        Continue While
                    End If

                    Dim fullPath As String = Path.Combine(_config.LocalFolder, toProcess.Replace("/", Path.DirectorySeparatorChar))
                    Try
                        Dim ok = Await EnsureFileReadyAndUploadAsync(fullPath)
                        If Not ok Then
                            ' Not ready again: re-enqueue after delay
                            Await Task.Delay(5000, ct)
                            SyncLock _queueLock
                                _retryQueue.Enqueue(toProcess)
                            End SyncLock
                        Else
                            SyncLock _queueLock
                                _retryAttempts.Remove(toProcess)
                            End SyncLock
                        End If
                    Catch ex As Exception
                        _logger.Error($"Retry processing failed for {toProcess}: {ex.Message}")
                    End Try
                End While
            Catch ex As OperationCanceledException
                ' shutdown
            Catch ex As Exception
                _logger.Error($"ProcessRetryQueueAsync error: {ex.Message}")
            End Try
        End Function

        Private Async Function ScanExistingFilesAsync() As Task
            Try
                If String.IsNullOrWhiteSpace(_config.LocalFolder) OrElse Not Directory.Exists(_config.LocalFolder) Then Return
                For Each f In Directory.GetFiles(_config.LocalFolder, "*", SearchOption.AllDirectories)
                    Await EnsureFileReadyAndUploadAsync(f)
                Next
            Catch ex As Exception
                _logger.Error($"Error scanning existing files: {ex.Message}")
            End Try
        End Function
    End Class

    Public Class S3Uploader
        Implements IS3Uploader

        Private ReadOnly _logger As ILogger

        Public Sub New(logger As ILogger)
            _logger = logger
        End Sub

        Public Async Function UploadAsync(localPath As String, bucket As String, key As String, cfg As AppConfig) As Task Implements IS3Uploader.UploadAsync
            Try
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

                Using client As New AmazonS3Client(creds, s3Config)
                    ' Ensure bucket exists (create if necessary). This works for both AWS and S3-compatible endpoints like MinIO.
                    Try
                        Dim exists = Await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, bucket)
                        If Not exists Then
                            _logger.Info($"Bucket '{bucket}' does not exist; creating...")
                            Dim putBucketReq = New PutBucketRequest() With {
                                .BucketName = bucket
                            }
                            Await client.PutBucketAsync(putBucketReq)
                            _logger.Info($"Bucket '{bucket}' created.")
                        End If
                    Catch ex As Exception
                        _logger.Info($"Error checking/creating bucket '{bucket}': {ex.Message}")
                        ' continue and attempt upload - the PutObject call will surface errors if bucket is missing
                    End Try

                    Dim putReq As New PutObjectRequest() With {
                        .BucketName = bucket,
                        .Key = key,
                        .FilePath = localPath
                    }

                    ' Retry loop with exponential backoff for transient errors
                    Const maxAttempts As Integer = 5
                    Dim backoffMs As Integer = 500
                    Dim lastEx As Exception = Nothing
                    For attempt As Integer = 1 To maxAttempts
                        Dim shouldRetry As Boolean = False
                        Try
                            Dim resp = Await client.PutObjectAsync(putReq)
                            _logger.Info($"S3 PutObject HTTP {resp.HttpStatusCode} for {key} on attempt {attempt}")
                            lastEx = Nothing
                            Exit For
                        Catch ex As AmazonS3Exception
                            lastEx = ex
                            ' Consider 5xx and throttling as transient
                            If (CInt(ex.StatusCode) >= 500 Or ex.StatusCode = System.Net.HttpStatusCode.TooManyRequests) And attempt < maxAttempts Then
                                shouldRetry = True
                                _logger.Info($"Transient S3 error (attempt {attempt}): {ex.Message}. Will retry after {backoffMs}ms")
                            Else
                                _logger.Error($"S3 error uploading {key}: {ex.Message}")
                                Throw
                            End If
                        Catch ex As Exception
                            lastEx = ex
                            If attempt < maxAttempts Then
                                shouldRetry = True
                                _logger.Info($"Transient error uploading (attempt {attempt}): {ex.Message}. Will retry after {backoffMs}ms")
                            Else
                                _logger.Error($"Error uploading {key}: {ex.Message}")
                                Throw
                            End If
                        End Try

                        If shouldRetry Then
                            Await Task.Delay(backoffMs)
                            backoffMs *= 2
                        End If
                    Next
                End Using
            Catch ex As AmazonS3Exception
                _logger.Error($"S3 error uploading {key}: {ex.Message}")
                Throw
            Catch ex As Exception
                _logger.Error($"Error uploading {key}: {ex.Message}")
                Throw
            End Try
        End Function
    End Class
End Namespace

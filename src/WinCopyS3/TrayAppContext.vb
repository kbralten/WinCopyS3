Imports System.ComponentModel
Imports System.Windows.Forms
Imports System.Drawing

Namespace WinCopyS3
    Public Class TrayAppContext
        Inherits ApplicationContext

        Private ReadOnly _notifyIcon As NotifyIcon
        Private ReadOnly _statusItem As ToolStripMenuItem
        Private ReadOnly _recentItem As ToolStripMenuItem
        Private ReadOnly _setupItem As ToolStripMenuItem
        Private ReadOnly _exitItem As ToolStripMenuItem

        Private ReadOnly _logger As ILogger
        Private ReadOnly _config As AppConfig
        Private ReadOnly _cache As CacheStore
        Private ReadOnly _uploader As IS3Uploader
    Private _watcher As IFileWatcherService
    Private ReadOnly _uiInvoker As Control

        Public Sub New()
            _logger = New InMemoryLogger()
            _config = AppConfig.Load()
            _cache = New CacheStore(_logger)
            _uploader = New S3Uploader(_logger)
            _watcher = New FileWatcherService(_logger, _config, _cache, _uploader)

            Dim cms = New ContextMenuStrip()
            _statusItem = New ToolStripMenuItem("Status: Idle")
            _statusItem.Enabled = False
            _recentItem = New ToolStripMenuItem("Recent Activity", Nothing, AddressOf OnRecent)
            _setupItem = New ToolStripMenuItem("Setup", Nothing, AddressOf OnSetup)
            _exitItem = New ToolStripMenuItem("Exit", Nothing, AddressOf OnExit)
            cms.Items.AddRange(New ToolStripItem() {_statusItem, New ToolStripSeparator(), _recentItem, _setupItem, New ToolStripSeparator(), _exitItem})

            ' Try to load the application icon from the executable output (fallback to default if missing)
            Dim iconToUse As Icon = System.Drawing.SystemIcons.Application
            Try
                Dim exeDir = AppDomain.CurrentDomain.BaseDirectory
                Dim iconPath = IO.Path.Combine(exeDir, "WinCopyS3.ico")
                If IO.File.Exists(iconPath) Then
                    iconToUse = New Icon(iconPath)
                End If
            Catch ex As Exception
                ' fallback to default
            End Try

            _notifyIcon = New NotifyIcon() With {
                .Text = "S3 Folder Sync",
                .Icon = iconToUse,
                .Visible = True,
                .ContextMenuStrip = cms
            }
            ' Hidden control used to marshal actions back onto the UI thread
            _uiInvoker = New Control()
            _uiInvoker.CreateControl()
            AddHandler _notifyIcon.DoubleClick, AddressOf TrayIcon_DoubleClick

            AddHandler _watcher.StatusChanged, AddressOf OnStatusChanged

            ' Start services if configured
            If _config.IsConfigured Then
                ' Ensure cache is populated from S3 if configured and missing so we don't re-upload existing objects
                If _config.RebuildCacheOnStartup AndAlso (Not _cache.CacheFileExists() OrElse _cache.IsEmpty()) Then
                    ' Run rebuild on a background task to avoid blocking the UI thread (tray menu responsiveness)
                    UpdateStatus("Rebuilding cache...")
                    ETWEvents.Log.CacheRebuildStarted()
                    Task.Run(Sub()
                                 Try
                                     _cache.RebuildFromS3Async(_config).GetAwaiter().GetResult()
                                     ETWEvents.Log.CacheRebuildCompleted(If(_cache.IsEmpty(), 0, -1))
                                 Catch ex As Exception
                                     _logger.Error($"Cache rebuild on startup failed: {ex.Message}")
                                     ETWEvents.Log.GeneralError($"Cache rebuild failed: {ex.Message}")
                                 Finally
                                    ' When finished, update UI and start watcher on the UI thread
                                    Try
                                        _uiInvoker.BeginInvoke(New MethodInvoker(AddressOf StartWatcherOnUI))
                                    Catch ex2 As Exception
                                        _logger.Error($"Failed to start watcher after cache rebuild: {ex2.Message}")
                                    End Try
                                 End Try
                             End Sub)
                Else
                    ' No rebuild needed; start watcher immediately
                    _watcher.StartWatching()
                End If
            Else
                UpdateStatus("Not Configured")
                ShowSetup()
            End If
        End Sub

        Private Sub OnStatusChanged(sender As Object, e As StatusChangedEventArgs)
            UpdateStatus(e.StatusText)
        End Sub

        Private Sub UpdateStatus(text As String)
            _statusItem.Text = $"Status: {text}"
            _notifyIcon.Text = $"S3 Folder Sync - {text}"
        End Sub

        Private Sub OnRecent(sender As Object, e As EventArgs)
            Using frm As New RecentActivityForm(CType(_logger, InMemoryLogger))
                frm.ShowDialog()
            End Using
        End Sub

        Private Sub OnSetup(sender As Object, e As EventArgs)
            ShowSetup()
        End Sub

        Private Sub TrayIcon_DoubleClick(sender As Object, e As EventArgs)
            ShowSetup()
        End Sub

        Private Sub ShowSetup()
            Using frm As New SetupForm(_config)
                If frm.ShowDialog() = DialogResult.OK Then
                    _config.Save()
                    _watcher.StopWatching()
                    _watcher = New FileWatcherService(_logger, _config, _cache, _uploader)
                    AddHandler _watcher.StatusChanged, AddressOf OnStatusChanged
                    _watcher.StartWatching()
                End If
            End Using
        End Sub

        Private Sub StartWatcherOnUI()
            Try
                UpdateStatus("Monitoring")
                ' Report actual cache size if available
                Try
                    ETWEvents.Log.CacheRebuildCompleted(_cache.EntryCount())
                Catch
                End Try
                _watcher.StartWatching()
            Catch ex As Exception
                _logger.Error($"Error starting watcher on UI thread: {ex.Message}")
            End Try
        End Sub

        Private Sub OnExit(sender As Object, e As EventArgs)
            _notifyIcon.Visible = False
            _watcher.StopWatching()
            Application.Exit()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _notifyIcon.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace

Imports System
Imports System.Windows.Forms

Friend Module Program
    <STAThread>
    Sub Main()
        Dim args = Environment.GetCommandLineArgs()
        If args.Contains("--smoketest") Then
            Console.WriteLine("Running smoketest (headless)...")
            Dim cfg = WinCopyS3.AppConfig.Load()
            If Not cfg.IsConfigured Then
                Console.WriteLine("AppConfig is not configured. Smoketest requires LocalFolder and Bucket configured.")
                Environment.Exit(2)
            End If

            ' Ensure local folder exists
            If Not IO.Directory.Exists(cfg.LocalFolder) Then
                Console.WriteLine($"LocalFolder does not exist: {cfg.LocalFolder}")
                Environment.Exit(2)
            End If

            Dim logger = New WinCopyS3.InMemoryLogger()
            Dim cache = New WinCopyS3.CacheStore(logger)
            ' If configured, rebuild cache during smoketest when the cache file is missing or empty
            If cfg.RebuildCacheOnSmoketest AndAlso (Not cache.CacheFileExists() OrElse cache.IsEmpty()) Then
                Console.WriteLine("Cache missing or empty; rebuilding from S3...")
                Try
                    cache.RebuildFromS3Async(cfg).GetAwaiter().GetResult()
                Catch ex As Exception
                    Console.WriteLine($"Cache rebuild failed: {ex.Message}")
                End Try
            End If
            Dim uploader = New WinCopyS3.S3Uploader(logger)
            Dim watcher = New WinCopyS3.FileWatcherService(logger, cfg, cache, uploader)

            ' Start watching in-process
            watcher.StartWatching()

            ' Create a temporary file AFTER watcher starts
            Dim tmpName = $"smoketest-{Guid.NewGuid().ToString()}.txt"
            Dim fullPath = IO.Path.Combine(cfg.LocalFolder, tmpName)
            Try
                IO.File.WriteAllText(fullPath, "smoketest")
            Catch ex As Exception
                Console.WriteLine($"Failed to create temp file: {ex.Message}")
                watcher.StopWatching()
                Environment.Exit(2)
            End Try

            Dim relative = IO.Path.GetRelativePath(cfg.LocalFolder, fullPath).Replace("\", "/")
            Dim waited = 0
            Dim timeoutMs = 30000
            Dim pollMs = 500
            Dim success = False
            While waited < timeoutMs
                If cache.Contains(relative) Then
                    success = True
                    Exit While
                End If
                Threading.Thread.Sleep(pollMs)
                waited += pollMs
            End While

            ' stop watcher
            watcher.StopWatching()

            If success Then
                Console.WriteLine("Smoketest succeeded: file uploaded and cache updated.")
                Environment.Exit(0)
            Else
                Console.WriteLine("Smoketest failed: timeout waiting for cache entry.")
                Environment.Exit(3)
            End If
        End If

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New WinCopyS3.TrayAppContext())
    End Sub
End Module

S3 Folder Sync for VB.NET
=========================

A simple, lightweight Windows tray application written in VB.NET to synchronize a local folder with an AWS S3 bucket. It operates on a "write-once" basis, making it ideal for archiving new files.

Overview
--------

S3 Folder Sync monitors a specified local directory for new files and folders. When a new item is detected, it is automatically uploaded to a designated AWS S3 bucket. The application is designed to be unobtrusive, running quietly in the system tray and providing status updates at a glance.

This tool does **not** track changes to existing files. It only uploads files that have not been previously synced, making it a perfect tool for collecting logs, photos, or documents as they are created.

Features
--------

*   **One-Way Sync:** Automatically uploads new files and folders from a local directory to AWS S3.
    
*   **Write-Once Logic:** Only new files are synced. File modifications or deletions are ignored.
    
*   **Local Caching:** Maintains a local cache file to keep track of successfully uploaded items, ensuring efficiency and preventing duplicate uploads.
    
*   **Smart Cache Initialization:** If the cache is missing, the app intelligently rebuilds it by scanning the contents of the target S3 bucket.
    
*   **System Tray Icon:** Runs in the background with an icon in the Windows system tray.
    
*   **Easy Status Checks:** Hover over the tray icon to see the current status (e.g., "Idle," "Syncing," "Error").
    
*   **Recent Activity Log:** Right-click the tray icon to view a list of the most recently uploaded files.
    
*   **Simple Configuration:** A straightforward setup screen to configure your AWS credentials, S3 bucket, and the local folder to watch.
    

How It Works
------------

1.  **Monitoring:** The application uses a FileSystemWatcher to monitor the specified local folder for any new files or subdirectories that are created.
    
2.  **Cache Check:** Before uploading, the app checks its local cache (.s3sync\_cache file) to see if the file has already been synced.
    
3.  **Upload:** If the file is not in the cache, the app uses the AWS SDK for .NET to upload the file to the specified S3 bucket, preserving the relative folder structure.
    
4.  **Cache Update:** Upon a successful upload, the file's path is added to the local cache.
    

### Cache Initialization

On the first run, or if the local cache file is deleted, the application will perform a one-time listing of all objects in your S3 bucket. It uses this list to build the initial cache, ensuring that it doesn't re-upload files that are already present in the bucket. This process prevents data duplication and unnecessary bandwidth usage.

Getting Started
---------------

### Prerequisites

*   Windows Operating System
    
*   [.NET 8 SDK or Runtime (WinForms support) — recommended] (https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
    
*   An AWS Account with an S3 bucket.
    
*   AWS IAM User credentials (Access Key ID and Secret Access Key) with permissions to ListBucket and PutObject for the target bucket.
    

### Installation

1.  Download the latest release from the (link to your releases page).
    
2.  Run the installer or unzip the application files to a directory of your choice.
    
3.  Launch S3FolderSync.exe.
    

### Configuration

1.  On the first launch, the **Setup** screen will appear. You can also access it by right-clicking the tray icon and selecting "Setup".
    
2.  Enter the following details:
    
    *   **Local Folder Path:** The full path to the local directory you want to monitor.
        
    *   **AWS S3 Bucket Name:** The name of your target S3 bucket.
        
    *   **AWS Region:** The region your S3 bucket is located in (e.g., us-east-1).
        
    *   **AWS Access Key ID:** Your IAM user's access key.
        
    *   **AWS Secret Access Key:** Your IAM user's secret key.
        
3.  Click **Save**. The application will validate the settings and begin monitoring.
    

Usage
-----

The application runs primarily from the system tray.

*   **Left-Click:** (Optional: Define an action, like opening the local folder).
    
*   **Right-Click:** Opens a context menu with the following options:
    
    *   **Status:** Shows the current sync status.
        
    *   **Recent Activity:** Displays the last 10 files that were uploaded.
        
    *   **Open Setup:** Launches the configuration window.
        
    *   **Exit:** Closes the application.
        
*   **Hover:** Displays a tooltip with the current status (e.g., "Monitoring...", "Uploading document.pdf...", "Sync complete").
    

Technology Stack
----------------

*   **Language:** VB.NET
    
*   **Framework:** Windows Forms (WinForms on .NET 8)
    
*   **SDK:** AWS SDK for .NET
    

Contributing
------------

Contributions are welcome! Please feel free to fork the repository, make changes, and submit a pull request. For major changes, please open an issue first to discuss what you would like to change.

License
-------

This project is licensed under the [MIT License](https://gemini.google.com/app/LICENSE.md).

Smoke Test (Local MinIO)
------------------------

For development and CI you can run a local S3-compatible server (MinIO) and execute a headless smoke test that verifies the end-to-end file detection, upload, and cache behavior.

The repository includes `build.ps1`, a PowerShell helper that:

- Downloads and runs `minio.exe` (if not present) and starts a local MinIO server on `http://localhost:9000`.
- Creates a test bucket named `wincopys3-test` (using the AWS CLI if available or the MinIO `mc` client).
- Writes `config.json` into `%APPDATA%\WinCopyS3\config.json` so the app points to the local MinIO and uses a `test\watch` folder inside the repo as the monitored folder.
- Builds the application and runs it in a headless `--smoketest` mode which:
    - Starts the file-watcher in-process,
    - Creates a temporary test file in the configured `LocalFolder`,
    - Waits up to 30 seconds for the watcher to upload the file and add the key to the local cache,
    - Exits with a success (0) or failure code (3 on timeout, 2 on config/folder errors).

How to run the local smoke test

From the repository root on Windows PowerShell:

```powershell
# Start MinIO (download if necessary), build, and run headless smoke test
.\build.ps1 -RunMinio -Build -SmokeTest

# When finished, cleanup downloaded artifacts and stop MinIO
.\build.ps1 -Cleanup
```

What to check after the test

- MinIO API should be available at `http://localhost:9000` (console UI on `:9001` when using the bundled binary).
- App config is written to `%APPDATA%\WinCopyS3\config.json` and contains the MinIO credentials and `ServiceURL`.
- The app's local cache lives at `%APPDATA%\WinCopyS3\cache.txt` — the test file's relative key should be appended there on successful upload.
- The `test\watch` folder in the repo root will contain the temporary test file created for the smoke test.

Exit codes

- `0` — smoketest succeeded (file uploaded and cache updated).
- `2` — configuration error (missing/invalid `AppConfig` or `LocalFolder`).
- `3` — smoketest timeout (file was not processed within 30 seconds).

Notes

- The smoke test uses a headless, in-process `FileWatcherService` so it does not require launching the tray UI.
- The test relies on `minioadmin:minioadmin` credentials for the local MinIO server (default for the downloaded binary). If you change credentials or ports, update `build.ps1` accordingly.
- Because credentials are stored in `%APPDATA%\WinCopyS3\config.json` for testing, be careful not to commit real AWS credentials into source control.

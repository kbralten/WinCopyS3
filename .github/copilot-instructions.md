# Copilot Coding Agent Onboarding Instructions

## Repository Summary
This repository contains **WinCopyS3**, a lightweight Windows tray application written in VB.NET for synchronizing a local folder with an AWS S3 bucket. The application monitors a specified folder for new files and uploads them to S3, maintaining a local cache to prevent duplicate uploads. It is designed for "write-once" use cases, such as archiving logs or documents.

## High-Level Repository Information
- **Project Type**: Windows desktop application (WinForms)
- **Primary Language**: VB.NET
- **Framework**: .NET 8
- **Target Runtime**: Windows
- **Repository Size**: Small to medium
- **Key Dependencies**:
  - AWS SDK for .NET (`AWSSDK.S3`)
  - MinIO (for local S3-compatible testing)

## Build and Validation Instructions
### Prerequisites
- **Operating System**: Windows
- **Required Tools**:
  - .NET 8 SDK: [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - PowerShell
  - Internet access (to download MinIO and dependencies)

### Build Steps
1. Ensure the .NET 8 SDK is installed and available in your PATH.
2. Open a PowerShell terminal and navigate to the repository root.
3. Run the following command to build the project:
   ```powershell
   dotnet build .\src\WinCopyS3\WinCopyS3.vbproj -c Debug
   ```
   - **Expected Outcome**: The project should build successfully, producing binaries in `src\WinCopyS3\bin\Debug`.

### Smoke Test
1. Run the provided `build.ps1` script to start MinIO, build the app, and execute the smoke test:
   ```powershell
   .\build.ps1 -RunMinio -Build -SmokeTest
   ```
2. **Expected Behavior**:
   - MinIO starts locally on `http://localhost:9000`.
   - A test bucket (`wincopys3-test`) is created.
   - The app runs in `--smoketest` mode, creates a temporary file in `test\watch`, uploads it to MinIO, and updates the cache.
   - Exit code `0` indicates success; `2` or `3` indicates failure.

### Cleanup
To stop MinIO and remove temporary files:
```powershell
.\build.ps1 -Cleanup
```

### Validation Steps
- Verify `%APPDATA%\WinCopyS3\config.json` contains the correct MinIO credentials and `ServiceURL`.
- Check `%APPDATA%\WinCopyS3\cache.txt` for the uploaded file's relative path.
- Confirm the temporary file is created in `test\watch` and uploaded to MinIO.

## Project Layout
### Key Files and Directories
- **`src/WinCopyS3`**: Contains the main application source code.
  - `Program.vb`: Entry point; handles `--smoketest` logic.
  - `Services.vb`: Implements `FileWatcherService` and `S3Uploader`.
  - `AppConfig.vb`: Manages application configuration.
  - `CacheStore.vb`: Handles the local cache.
- **`build.ps1`**: PowerShell script for building, running MinIO, and executing smoke tests.
- **`README.md`**: Project documentation.
- **`.github/copilot-instructions.md`**: This file.

### Configuration Files
- **`%APPDATA%\WinCopyS3\config.json`**: Stores app settings (e.g., `LocalFolder`, `BucketName`, `ServiceURL`).
- **`%APPDATA%\WinCopyS3\cache.txt`**: Tracks uploaded files to prevent duplicates.

### Validation Pipelines
- No CI/CD pipelines are currently configured. Validation is manual via `build.ps1` and smoke tests.

## Additional Notes
- **MinIO**: The repository uses MinIO for local S3-compatible testing. The `build.ps1` script handles downloading and running MinIO automatically.
- **Testing**: The `--smoketest` mode in `Program.vb` runs a headless test of the file-watching and upload logic.
- **Error Handling**: Common issues include:
  - Missing .NET SDK: Ensure it is installed and in PATH.
  - MinIO startup issues: Check for port conflicts on `9000`.

## Agent Guidance
- **Trust these instructions**: Follow the documented steps for building, testing, and validating changes.
- **Search only if necessary**: Use code search only if the instructions are incomplete or produce errors.
- **Focus Areas**:
  - For file-watching logic, see `FileWatcherService` in `Services.vb`.
  - For S3 uploads, see `S3Uploader` in `Services.vb`.
  - For configuration, see `AppConfig.vb`.
  - For cache handling, see `CacheStore.vb`.

By following these instructions, you can efficiently implement, validate, and test changes in this repository.
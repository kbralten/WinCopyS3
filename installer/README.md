WinCopyS3 Installer (WiX)

This folder contains a WiX-based MSI project and a helper script to produce an installer for the WinCopyS3 application. The helper script publishes the app as a single-file application and invokes the local WiX Toolset `wix` CLI (if available) to build the MSI.

Prerequisites
- .NET 8 SDK
- WiX Toolset (recommended). You can download the toolset from https://wixtoolset.org/ or install a recent `wix` CLI on your PATH.
- If you prefer to build in CI, a Windows runner with WiX installed or the appropriate `Wix`/`Wix.Toolset` packages are needed.

Quick build (recommended when `wix` CLI is available)
Open PowerShell from the repository root and run:

```powershell
cd installer
./build-installer.ps1 -Configuration Release -Runtime win-x64
```

This script does two things:
- Runs `dotnet publish` for the `WinCopyS3` project into a `publish\` folder.
- If it finds a `wix` CLI on PATH, it runs `wix build` and produces an MSI in `installer\output\WinCopyS3.msi`.

If WiX CLI is not present the script will still produce the `publish\` artifacts and leave `Product.wxs` and other installer sources in `installer/` for you or CI to finish the build.

How to enable the "Run at startup" option

This installer exposes a property `INSTALLSTARTUP` which controls whether the application will be registered to run at user logon (per-user Run key). By default the property is `0` (disabled).

Examples:
- Interactive install with startup enabled (prompts, typical UIs):

```powershell
msiexec /i "installer\output\WinCopyS3.msi" INSTALLSTARTUP=1
```

- Silent install with startup enabled (no UI):

```powershell
msiexec /i "installer\output\WinCopyS3.msi" INSTALLSTARTUP=1 /qn
```

Notes about the generated MSI
- Output: `installer\output\WinCopyS3.msi` (after running the build script with a functioning WiX CLI).
- The installer places the main executable in `Program Files\WinCopyS3` and creates a Start Menu shortcut.
- The startup option adds a per-user Registry Run key under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the installed executable.

Customizations & CI
- Update `Product.wxs` to change `Manufacturer`, `UpgradeCode`, version, and to add/remove desktop shortcuts or other resources.
- For CI, use a Windows runner that has the WiX Toolset installed or use a preinstalled `wix` CLI image/runner and run the same `build-installer.ps1` script from the workflow.

Troubleshooting
- If the build script publishes but `wix build` fails, run `wix build -h` to check available options and ensure the `publish\` folder contains the executable referenced by `Product.wxs`.
- If the MSI does not create the startup entry, verify you passed `INSTALLSTARTUP=1` during install (the installer will not enable startup by default).

If you want, I can:
- Add a UI dialog to the MSI that exposes a "Run at startup" checkbox rather than requiring the property be passed on the command line.
- Add a Desktop shortcut or change the Start Menu placement.
- Produce a GitHub Actions workflow that builds the MSI on a Windows runner and uploads the MSI as a build artifact.

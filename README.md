# RunCat365-personal

**Personal private derivative** of [runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365).

Upstream is a cute running cat animation on the Windows Taskbar (`C#` / Win32 / `.NET 9.0`).

## Attribution & license

- **Upstream:** [https://github.com/runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365)
- **License:** Apache License 2.0 (see [LICENSE](./LICENSE)) — same as upstream
- This repository is a personal fork/copy for private use and small fixes. It does **not** claim authorship of the upstream project (copyright remains with Takuto Nakamura / Studio Kyome and upstream contributors).

## Personal changes

Local derivative with up to four tray indicators (CPU / GPU / Memory / Temperature), GPU dedicated usage (used only), all ready fixed drives in storage info, and temperature→speed mapping. See commit history for details.

## Build / run

- Requirement: Windows 10 version 19041.0 or higher, .NET 9 SDK / Visual Studio
- Open `RunCat365.sln` and build, or: `dotnet build RunCat365.sln`

Official Microsoft Store listing (upstream): https://apps.microsoft.com/detail/9nw5lpnvwfwj

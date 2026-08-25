# Yazi Terminal

Windows WPF host for the ordinary Yazi terminal file manager. The project is
published as **Yazi Terminal** under the `yazi-terminal` repository name.
`EasyWindowsTerminalControl` 1.0.38 is the selected backend for the current
implementation. It hosts the Windows Terminal renderer and ConPTY through a
native terminal control; production packaging gates remain open.

Start with the [user manual](docs/user-manual.md) for the supported environment,
setup, validated capabilities, limitations, and license.

## User installation

1. Download the latest Windows x64 archive from the
   [GitHub Releases](https://github.com/hironei/yazi-terminal/releases) page.
2. Install the .NET 10 Windows Desktop runtime if it is not already installed.
3. Extract the archive and start `YaziTerminal.exe`.
4. Install the optional Yazi bridge plugin using the
   [plugin installation guide](docs/user-manual.md#plugin-installation).

The archive contains `YaziTerminal.exe`, the required native terminal files,
the MIT `LICENSE`, this documentation, and the Yazi bridge plugin.

### Binary release status

The `CI.Microsoft.Terminal.Wpf` repackage has version-specific provenance
 evidence tied to Microsoft Terminal commit `9ae724aa...`; the full upstream
 Microsoft Terminal `LICENSE` and `NOTICE.md` are included in the release ZIP
 and validated by SHA-256. This is not a legal conclusion; see
 [the third-party notices](THIRD-PARTY-NOTICES.md) for the evidence. The
 v0.1.8 Release is retained, and its ZIP is replaced by a notice-complete
 repackaging with no executable-code changes.

## Developer setup

The following commands are for building and testing from source. They are not
required for users installing a published release.

Restore and build the solution:

```powershell
dotnet restore YaziDesktopHost.slnx
dotnet build YaziDesktopHost.slnx --no-restore
```

Run the executable test suite:

```powershell
dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore
```

Run the host with the .NET 10 SDK from the repository root:

```powershell
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

The normal executable invocation creates a new window. Pass a directory to set
the initial Yazi directory, or pass an existing file to reveal and open it
using Yazi's configured opener/editor. To reuse the most recently launched Yazi
Terminal window, use `--last-instance`:

```powershell
YaziTerminal.exe C:\work\project
YaziTerminal.exe C:\work\project\README.md
YaziTerminal.exe --last-instance C:\work\other-project
YaziTerminal.exe --last-instance C:\work\project\README.md
```

If the last instance is not available, `--last-instance` starts a new window
with the requested directory.

From Git Bash, quote the executable and directory when either path contains
spaces:

```bash
"/c/Tools/Yazi Terminal/YaziTerminal.exe" --last-instance "C:/work/other-project"
```

Set `YAZI_PATH` when `yazi.exe` is not available through `PATH`. Later Shell
integration phases must not be implemented by parsing terminal screen text.

For an opt-in terminal color diagnostic, set `YAZI_DESKTOP_HOST_VT_FIXTURE=1`
before launching. The fixture is intended for manual validation only.

## License

Yazi Terminal is released under the [MIT License](LICENSE).

The application also uses third-party components, including
[`EasyWindowsTerminalControl`](https://github.com/mitchcapper/EasyWindowsTerminalControl),
the Microsoft Windows Terminal WPF control, and ConPTY packages. Their terms
and required notices are separate from this project's license. See the
[user manual](docs/user-manual.md) for the current dependency and distribution
notes.

# Yazi Terminal

Windows WPF host for the ordinary Yazi terminal file manager. The project is
published as **Yazi Terminal** under the `yazi-terminal` repository name.
`EasyWindowsTerminalControl` 1.0.38 is the selected backend for the current
implementation. It hosts the Windows Terminal renderer and ConPTY through a
native terminal control; production packaging gates remain open.

Start with the [user manual](docs/user-manual.md) for the supported environment,
setup, validated capabilities, limitations, and license.

Run the host with the .NET 10 SDK from the repository root:

```powershell
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

For a packaged Windows x64 build, download the latest archive from the
[GitHub Releases](https://github.com/hironei/yazi-terminal/releases) page.
The release archive includes `YaziTerminal.exe`, the required native terminal
files, this documentation, and the Yazi bridge plugin.

Set `YAZI_PATH` when `yazi.exe` is not available through `PATH`. Later Shell
integration phases must not be implemented by parsing terminal screen text.

For an opt-in terminal color diagnostic, set `YAZI_DESKTOP_HOST_VT_FIXTURE=1`
before launching. The fixture is intended for manual validation only.

For bridge-backed Shell integration, follow the
[Yazi plugin installation guide](docs/user-manual.md#plugin-installation).

## License

Yazi Terminal is released under the [MIT License](LICENSE).

The application also uses third-party components, including
[`EasyWindowsTerminalControl`](https://github.com/mitchcapper/EasyWindowsTerminalControl),
the Microsoft Windows Terminal WPF control, and ConPTY packages. Their terms
and required notices are separate from this project's license. See the
[user manual](docs/user-manual.md) for the current dependency and distribution
notes.

# Terminal host candidate evaluation

## Decision status

`EasyWindowsTerminalControl` 1.0.38 is now the first implementation candidate
for the next compatibility run. It is not adopted as the production terminal
backend yet. The package wraps the Microsoft Windows Terminal WPF control and
ConPTY, while keeping the terminal boundary replaceable without introducing
screen parsing. The earlier `VirtualTerminal.WPF` /
`VirtualTerminal.CommandLine` 1.8.1 run remains the baseline comparison.

The Easy candidate is intentionally provisional. Its published package still
depends on beta/low-level Windows Terminal components, and the native terminal
HWND introduces WPF airspace constraints. See the [NuGet package page](https://www.nuget.org/packages/easywindowsterminalcontrol/)
and the [upstream project](https://github.com/mitchcapper/EasyWindowsTerminalControl).

## Alternative comparison

Microsoft's repository contains a WPF `TerminalControl` under
`src/cascadia/WpfTerminalControl`. The control exposes a terminal connection,
rows/columns, theme configuration, resize-related state, and the Windows
Terminal rendering/input stack. Its project currently targets `net472` and
`net8.0-windows` and packages native x86, x64, and ARM64 binaries, so adopting
it would require a source-build or packaging strategy in this .NET 10 host.
See the [official WPF control source](https://github.com/microsoft/terminal/blob/main/src/cascadia/WpfTerminalControl/TerminalControl.xaml.cs)
and [project file](https://github.com/microsoft/terminal/blob/main/src/cascadia/WpfTerminalControl/WpfTerminalControl.csproj).

| Concern | EasyWindowsTerminalControl 1.0.38 | Microsoft Windows Terminal WPF control |
| --- | --- | --- |
| Initial integration | Low: NuGet control, official Windows Terminal backend, and ConPTY wrapper | High: source/native build and packaging boundary |
| Current target fit | Package restores and builds in this .NET 10 WPF host | Upstream project currently targets .NET Framework 4.7.2 and .NET 8 WPF |
| Renderer/input maturity | Native Windows Terminal renderer/input path; Yazi-specific matrix is partly passed | Same renderer/input stack, but the required ConPTY/process wrapper still must be designed |
| Resize surface | Real Yazi resize passed manually; `EasyTerminalControl` owns the terminal surface | Control exposes rows/columns and a connection abstraction |
| Child exit ownership | `TermPTY` exposes process/start/stop APIs but no public child-exit event was found | Connection implementation can be owned by this host, but the required process wrapper still must be designed |
| Distribution risk | Package includes native terminal/ConPTY assets; beta dependency and architecture review remain | Native architecture-specific artifacts and upstream source cadence must be controlled |

This comparison does not select the Easy candidate automatically. The current
recommendation is to continue with Easy as the leading candidate while the
child-exit observation, packaging, and later GUI-overlay constraints are
resolved. If a gate fails, compare it with the source-derived
Microsoft control; Microsoft's own sample documentation describes the WPF
console sample as a skeleton and the MiniTerm sample as experimental, so a
production choice still needs an owned connection and cleanup design. See the
[Microsoft Terminal samples](https://learn.microsoft.com/en-us/windows/terminal/samples).

## Evidence collected

| Capability | Result | Evidence or remaining action |
| --- | --- | --- |
| Package restore | PASS | `EasyWindowsTerminalControl` 1.0.38 and its Windows Terminal/ConPTY dependencies restored with the .NET 10 SDK. |
| WPF build | PASS | `dotnet build YaziDesktopHost.slnx --no-restore`, 0 warnings and 0 errors. |
| Real Yazi launch | PASS | The host launched the installed `yazi.exe` 26.5.6 through the embedded native terminal; no separate terminal window was opened. |
| Renderer / alternate screen | PASS | Yazi's full-screen surface rendered naturally in the manual run; the user reported less redraw stress than VirtualTerminal. A deterministic 24-bit color fixture remains pending. |
| Keyboard delivery | PASS | The user confirmed normal key operation. |
| Unicode/CJK/wide characters | PASS | The user confirmed CJK display. |
| IME composition | PASS | The user confirmed Japanese input, conversion, and commit with the Easy control; the candidate window appeared at the input position. |
| xterm mouse reporting | PASS | The user confirmed mouse operation in Yazi. Explorer-style Drag & Drop is not implemented and is a separate GUI/OLE requirement. |
| Resize and reflow | PASS | The user confirmed interactive resize. |
| Close cleanup | PASS | After the run, no Easy host or `yazi` process remained. |
| Unexpected child exit | PARTIAL | `TermPTY` exposes process/start/stop APIs but no public child-exit event was found. Close cleanup is implemented, but unexpected-exit observation remains unresolved. |

## Acceptance boundary

The automated run proves package integration, compilation, executable
resolution, host launch, bridge-environment scoping, and process cleanup. The
manual run proves the listed CJK, keyboard, mouse, resize, and IME behaviors.
Deterministic 24-bit color and unexpected child-exit observation remain
acceptance gates before selecting the candidate for production.

The unexpected-child-exit result is a separate lifecycle limitation. It must
be resolved by a documented session API guarantee or by replacing the
candidate with a backend that exposes reliable child-process observation.

## IME root-cause finding

The host-level IMM32 workaround was intentionally removed after a diagnostic
run. The host received no `WM_IME_STARTCOMPOSITION`, `WM_IME_COMPOSITION`, or
`WM_IME_NOTIFY` messages during Japanese conversion, even though committed
Japanese text was delivered. This indicates that the active input path is
WPF/TSF text composition rather than the IMM32 message path that
`ImmSetCandidateWindow` can control.

This matches the architectural difference from standard WPF text controls:
WPF's internal `ImmComposition` obtains a `TextEditor`/`ITextView` caret
rectangle, transforms it to device coordinates, and supplies a candidate
exclusion rectangle. `VirtualTerminal.WPF` exposes a custom drawing control
and `PreviewTextInput`, but no public text-store or microfocus API. Therefore,
further coordinate-only changes in `MainWindow` are not a reliable fix.

The earlier VirtualTerminal run showed a candidate window at the desktop's
upper-left corner despite successful Japanese commit. Easy hosts the official
Windows Terminal control in a native terminal HWND, and the manual run
confirmed that Japanese conversion candidates are positioned correctly. The
native HWND also means WPF overlay and airspace behavior needs a later GUI
integration test.

If any gate fails, compare the candidate against a WPF control derived from
Microsoft Windows Terminal, recording the same capability-by-capability
results before making an adoption decision.

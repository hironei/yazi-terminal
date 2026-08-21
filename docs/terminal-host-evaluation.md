# Terminal host candidate evaluation

## Decision status

`VirtualTerminal.WPF` / `VirtualTerminal.CommandLine` 1.8.1 is the first
implementation candidate. It is not adopted as the production terminal
backend yet. The package is behind the WPF/session boundary so a failed
compatibility gate can be replaced without introducing screen parsing.

The comparison candidate is a WPF Terminal Control derived from the Microsoft
Windows Terminal implementation. It is not implemented in this slice because
the first candidate has not yet produced a complete compatibility result.

## Alternative comparison

Microsoft's repository contains a WPF `TerminalControl` under
`src/cascadia/WpfTerminalControl`. The control exposes a terminal connection,
rows/columns, theme configuration, resize-related state, and the Windows
Terminal rendering/input stack. Its project currently targets `net472` and
`net8.0-windows` and packages native x86, x64, and ARM64 binaries, so adopting
it would require a source-build or packaging strategy in this .NET 10 host.
See the [official WPF control source](https://github.com/microsoft/terminal/blob/main/src/cascadia/WpfTerminalControl/TerminalControl.xaml.cs)
and [project file](https://github.com/microsoft/terminal/blob/main/src/cascadia/WpfTerminalControl/WpfTerminalControl.csproj).

| Concern | VirtualTerminal 1.8.1 | Microsoft Windows Terminal WPF control |
| --- | --- | --- |
| Initial integration | Low: NuGet WPF control plus ConPTY session | High: source/native build and packaging boundary |
| Current target fit | Directly documented for this .NET 10 project | Upstream project currently targets .NET Framework 4.7.2 and .NET 8 WPF |
| Renderer/input maturity | Must be proven against Yazi in this host | Reuses Windows Terminal's renderer/input stack, but still needs Yazi-specific proof |
| Resize surface | Session/control resize API is available | Control exposes rows/columns and a connection abstraction |
| Child exit ownership | Public session API did not expose a reliable child-exit event in the probe | Connection implementation can be owned by this host, but the required ConPTY/process wrapper still must be designed |
| Distribution risk | Package restore and local build currently work | Native architecture-specific artifacts and upstream source cadence must be controlled |

This comparison does not select the Microsoft control automatically. The
current recommendation is to keep `VirtualTerminal` as the provisional
implementation candidate while running the same manual Yazi matrix against
both controls if any VT, input, or lifecycle gate fails. Microsoft's own sample
documentation describes the WPF console sample as a skeleton and the MiniTerm
sample as experimental, so a production choice still needs an owned connection
and cleanup design; see the [Microsoft Terminal samples](https://learn.microsoft.com/en-us/windows/terminal/samples).

## Evidence collected

| Capability | Result | Evidence or remaining action |
| --- | --- | --- |
| Package restore | PASS | `VirtualTerminal.WPF` and `VirtualTerminal.CommandLine` 1.8.1 restored with the .NET 10 SDK. |
| WPF build | PASS | `dotnet build YaziDesktopHost.slnx --no-restore`, 0 warnings and 0 errors. |
| Real Yazi launch | PASS | The host launched the installed `yazi.exe` 26.5.6 through the embedded control; no separate terminal window was opened. |
| Keyboard delivery | PARTIAL | A keyboard action was delivered to the host window without an automation error. Semantic navigation movement still needs a human-observable acceptance check. |
| VT rendering / 24-bit color | NOT VERIFIED | Requires visual/manual inspection of a deterministic VT fixture inside the running host. |
| Alternate screen | NOT VERIFIED | Requires confirming Yazi's full-screen surface and restoration after exit. |
| Unicode/CJK/wide characters | NOT VERIFIED | Requires a visible Japanese and wide-character fixture in the running host. |
| IME composition | NOT VERIFIED | Requires interactive Windows IME composition and committed text. |
| xterm mouse reporting | NOT VERIFIED | Requires interactive mouse actions in Yazi and confirmation that reporting is preserved. |
| Resize and reflow | NOT VERIFIED | Requires interactive resize while Yazi is running and visual reflow confirmation. |
| Close cleanup | PASS | The host process and its `yazi` child were absent after the evaluation process was closed. |
| Unexpected child exit | PARTIAL | The host handles the session's public `Disconnected` notification, but an immediate child exit using `where.exe` did not raise that event. The candidate does not expose a public child-exit event, so direct exit observation remains unresolved. |

## Acceptance boundary

The automated run proves package integration, compilation, executable
resolution, host launch, and process cleanup. It does not prove the visual or
interactive terminal behaviors listed as `NOT VERIFIED`. Those remain manual
acceptance gates before selecting the candidate for production.

The unexpected-child-exit result is a separate lifecycle limitation. It must
be resolved by a documented session API guarantee or by replacing the
candidate with a backend that exposes reliable child-process observation.

If any gate fails, compare the candidate against a WPF control derived from
Microsoft Windows Terminal, recording the same capability-by-capability
results before making an adoption decision.

# Terminal host candidate evaluation

## Decision status

`VirtualTerminal.WPF` / `VirtualTerminal.CommandLine` 1.8.1 is the first
implementation candidate. It is not adopted as the production terminal
backend yet. The package is behind the WPF/session boundary so a failed
compatibility gate can be replaced without introducing screen parsing.

The comparison candidate is a WPF Terminal Control derived from the Microsoft
Windows Terminal implementation. It is not implemented in this slice because
the first candidate has not yet produced a complete compatibility result.

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

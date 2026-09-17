# Requirements Review: Issues #72-#83

## Accepted scope

The review confirmed the following current-main defects and maintenance gaps:

- A failed settings load must not cause the next close to overwrite the user's file.
- Settings writes must not expose a partially written JSON document.
- Shell and drag/drop target resolution must reject bridge snapshots older than one second.
- One malformed command-catalog entry must not disconnect the bridge.
- A rejected bridge connection must close so the plugin can reconnect.
- Last-instance request processing needs a server deadline shorter than the client fallback deadline.
- Bridge environment variables must stop leaking into unrelated host children after the Yazi child is ready.
- Unexpected dispatcher, task, and bridge-loop failures need bounded logging and user-visible handling.
- app.log needs bounded rotation.
- The context-menu rename flag and unused imports are unnecessary.

## Documented constraints

The current sideband protocol does not carry the terminal pointer position or
an acknowledgement that a particular hover event was consumed. Therefore the
host cannot prove that a right-click or left-button drag refers to the visual
item under the pointer when the user changes hover state between bridge polls.
The one-second freshness gate reduces long-lived stale targets, while the
remaining race and terminal text-selection interaction stay explicit manual
compatibility limitations.

The historical font requirements listed a small allowlist, while the shipped
implementation intentionally accepts any installed WPF family and sizes
1..32767. This record supersedes that allowlist; no compatibility range for
Windows versions or Yazi/ya versions is claimed.

## Acceptance boundary

Automated acceptance covers parser, reducer, settings, logging, timeout
constants, and pure target-resolution behavior. Live WPF, IME, native terminal
mouse reporting, Explorer/Shell extensions, focus activation, and destructive
file-operation behavior remain manual Windows gates.

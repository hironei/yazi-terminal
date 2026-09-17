# EasyWindowsTerminalControl 1.0.38 TermReady evidence

The package metadata pins repository commit
`0741a4b8853c47bcac4412d005ed4ae1d96d2c13`. In that source, the normal
`TermPTY.Start` path calls the process factory, assigns the returned process
and pseudoconsole streams, and only then invokes `TermReady`. The process
factory calls `CreateProcess` before returning, so the host's current
`TermReady` cleanup is ordered after child-process creation for this pinned
version.

This is source-level compatibility evidence, not a live acceptance result.
Windows testing is still required for bridge hello/reconnect, early child exit,
and confirming that a later unrelated host child does not inherit the temporary
bridge environment. Design-mode `TermReady` is not the supported product path.

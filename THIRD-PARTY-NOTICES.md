# Third-party notices and redistribution gate

This file is included with a release-eligible Yazi Terminal archive. It is an
engineering record of package metadata, not legal advice.

## EasyWindowsTerminalControl 1.0.38

The package nuspec declares the SPDX `MIT` license expression and identifies
MitchCapper as its author. The package's copyright/license notice is included
at `third-party/EasyWindowsTerminalControl-MIT.txt`.

## Microsoft.Windows.Console.ConPTY 1.24.260710001

The package nuspec declares the SPDX `MIT` license expression, identifies
Microsoft as the author, and links to the Microsoft Terminal project. The
copyright/license notice is included at
`third-party/Microsoft-Windows-Console-ConPTY-MIT.txt`.

## CI.Microsoft.Terminal.Wpf 1.25.260303002 — VERIFIED PROVENANCE

This repackage supplies `Microsoft.Terminal.Wpf.dll` and the native
`Microsoft.Terminal.Control.dll` that the `win-x64` output uses. The nuspec
pins Microsoft Terminal commit
`9ae724aa5b080aafbeea2bbf88db630b182cc802`, the official WPF pack pipeline and
source rebuild correlate the managed assembly, and the x64 native DLL hash
matches the corresponding Microsoft Terminal release asset. The full upstream
Microsoft Terminal MIT files are included without reduction at:

- `third-party/microsoft-terminal/LICENSE`
- `third-party/microsoft-terminal/NOTICE.md`

The release validator checks both files' exact SHA-256 values. This is an
engineering provenance record, not a legal conclusion about the independent
CI repackage publisher.

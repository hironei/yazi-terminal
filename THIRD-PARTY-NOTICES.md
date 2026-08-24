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

## CI.Microsoft.Terminal.Wpf 1.25.260303002 — RELEASE BLOCKED

This repackage supplies `Microsoft.Terminal.Wpf.dll` and the native
`Microsoft.Terminal.Control.dll` that the `win-x64` output uses. Its local
nuspec has no license expression, license URL/file, or repository URL, and the
package itself contains no LICENSE or NOTICE file. The upstream Microsoft
Terminal MIT LICENSE and NOTICE do not by themselves establish a
redistribution chain for this separately published repackage.

Until the repackage publisher (or another authoritative source) provides
version-specific redistribution terms and the required notices, do not publish
new binary archives. Do not treat the existing v0.1.8 archive as remediated;
an authorized maintainer must decide whether to replace or withdraw it after
the evidence is recorded.

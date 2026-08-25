# Compatibility Matrix and Distribution Gates

## Status

This document separates current release/source observations from a historical
manual fixture. It is not a promise that every Windows version, CPU
architecture, or Yazi release is supported.

## Current release and source observations

The following values were directly observed for the public v0.1.8 archive and
the matching current source checkout. The exact SHA-256, complete ZIP entry
list, executable version, source commit, and bridge plugin blob are in
[`compatibility-evidence/v0.1.8.json`](compatibility-evidence/v0.1.8.json).

| Area | Observed value | Status | Boundary |
| --- | --- | --- | --- |
| Release artifact | `YaziTerminal-v0.1.8-win-x64.zip`, SHA-256 `0A7B989316A837451A1EF8064BDDB43D96A7798E802D3F7209897917800FB38E`, 16 entries | OBSERVED | This is inspection of the published asset, not a new publish run. |
| Executable | `YaziTerminal.exe`, file version `0.1.8`, product version `0.1.8+c65ff9eef68ef1af3a338587ca3a1ceb67836de5` | OBSERVED | The archive uses the current assembly name, unlike the older historical publish record. |
| Current source | commit `c65ff9eef68ef1af3a338587ca3a1ceb67836de5` | OBSERVED | Evidence must be regenerated when source changes. |
| Bridge plugin | `main.lua` Git blob `d7db80883342fd04296d090c136980cdf044e7dc`; SHA-256 `D8CEBB2514D07596EE7D58D293BC2C19D85A48BC192E6206E69CBD4416C7C082` | OBSERVED | The v0.1.8 archive plugin bytes match this source revision. |
| Plugin command catalog/reconnect source | `hello` sends `json_commands(get_all_commands())`; reconnect loop resets sequence and retries | STATIC PASS | Lua syntax/source inspection only; not a live Yazi fixture. |
| Host bridge protocol | parser, command catalog, and reconnect executable tests | AUTOMATED PASS | Named-pipe host tests do not execute Yazi/Lua. |
| Live Yazi bridge/catalog/reconnect | Current plugin with real Yazi | NOT RUN | Local Yazi/`ya` is 26.8.15; the historical live fixture used 26.5.6. |
| Release `win-x64` publish | New current publish artifact | NOT RUN | Do not bypass the Issue #21 fail-closed redistribution gate to manufacture an artifact. |

## Historical validated fixture

| Area | Validated value | Status | Boundary |
| --- | --- | --- | --- |
| Windows OS | OS version/build `10.0.26200` | HISTORICAL PASS | Historical fixture only; Windows 10 2004/build `19041` is the minimum candidate floor for the embedded Windows Terminal path, pending a packaged-host test. |
| Runtime architecture | `win-x64`, AMD64 | HISTORICAL PASS | x86 and ARM64 are explicitly excluded because the selected Easy backend is x64-only. |
| .NET SDK | `10.0.400` | HISTORICAL PASS | The project targets `net10.0-windows`; no older SDK is claimed. |
| .NET runtime | `10.0.11`, Windows Desktop runtime | HISTORICAL PASS | Framework-dependent versus self-contained distribution remains a release decision. |
| Yazi | `26.5.6`, revision `aa52643` | HISTORICAL PASS | This exact version is the validated fixture; no compatibility range is claimed. |
| `ya` | `26.5.6`, revision `aa52643` | HISTORICAL PASS | The bridge requires the paired Yazi/`ya` fixture. |
| Bridge protocol | `yazi-desktop-host/1` | HISTORICAL PASS | Messages are instance-bound and use the Phase 2 framed sideband transport. |
| Bridge plugin | Repository revision `409cd5c2bc9298ee040fc2156ee86a0a2970fc12`; `main.lua` SHA-1 `1f3114030654b21ee49d23f46833519e7d62b325` | HISTORICAL PASS | The plugin was tested through a temporary Yazi configuration; it is not the current plugin revision. |
| Terminal host | `EasyWindowsTerminalControl` `1.0.38` | HISTORICAL PASS | Selected for the historical implementation; production packaging remains gated. |
| Windows Terminal dependency | `CI.Microsoft.Terminal.Wpf` `1.25.260303002` | HISTORICAL PASS | The product targets the package's `win-x64` native assets; x86 and ARM64 are excluded. |
| ConPTY dependency | `Microsoft.Windows.Console.ConPTY` `1.24.260710001` | HISTORICAL PASS | The package metadata declares MIT and describes Windows `10.0.17763.0` or newer; this is not yet the product minimum. |
| Native ConPTY assets | `conpty.dll` for `win-x64` | HISTORICAL PASS | The package's x86/ARM64 assets are not product targets. |

The historical framework-dependent `win-x64` publish passed with
`PublishReadyToRun=false` after restoring with the `win-x64` RID. The publish
directory contained `YaziDesktopHost.exe`,
`EasyWindowsTerminalControl.dll`, `Microsoft.Terminal.Control.dll`, and
`conpty.dll`. This proves package composition for the historical x64 fixture; it
does not prove current artifact composition or that a packaged host starts on
another machine.

The project now declares `PlatformTarget=AnyCPU` and the supported
`RuntimeIdentifiers` `win-x64`. An x86 self-contained startup probe was also
performed during the architecture review and failed before the window loaded:
the x86 host (`PE machine 0x014C`) cannot load the x64
`EasyWindowsTerminalControl.dll` (`PE machine 0x8664`). This is why x86 and
ARM64 are excluded from the product target.

## Historical manual capability result

The Windows run passed real Yazi launch, VT/alternate-screen rendering, 24-bit
color fixture output, CJK, keyboard, IME composition and commit, xterm mouse
reporting, resize/reflow, close cleanup, unexpected child exit handling, and
the required Explorer/Yazi drag-and-drop paths. Ctrl/Shift Copy/Move behavior
also passed. Ordinary keyboard navigation that changes `cwd` while an Explorer
drag is active is a v1 non-goal because the OLE drag loop does not deliver
those keys to Yazi.

## Distribution gates

The following gates remain open before claiming a distributable support range:

1. Declare the minimum Windows version and supported product editions. The
   current candidate floor is Windows 10 2004/build `19041`, based on the
   Windows Terminal prerequisite; it is not yet a product support claim.
2. Decide whether to ship framework-dependent or self-contained output.
3. Run the host on the claimed `win-x64` architecture, including native asset
   loading and ConPTY startup. x64 is manually passed.
4. Verify native HWND/WPF airspace behavior in the packaged application,
   including any overlays or future dialogs.
5. Record the supported Yazi/`ya` version policy and test at least one upgrade
   and one rejection path for an unsupported pair.
6. Before binary distribution, validate the Microsoft Terminal MIT `LICENSE` and
   `NOTICE` handling, native asset redistribution, and installation layout.
   Issue #21 makes the unresolved `CI.Microsoft.Terminal.Wpf` repackage a
   release blocker: no new binary archive is eligible until version-specific
   redistribution evidence and required notices are recorded.

Until these gates are closed, the support statement is limited to the
historical Windows `win-x64` fixture and its exact Yazi/`ya` pair. The current
release/source observations above do not expand that support statement.

## Updating release evidence

For each published release, download its ZIP to a temporary directory and run
the updater and validator described in
[`design-issue29-compatibility-evidence.md`](design-issue29-compatibility-evidence.md).
The updater records the complete archive listing and refuses to bind a release
to a checkout whose bridge plugin bytes differ. Archive inspection does not
replace a live Yazi fixture or the Issue #21 binary-release gate.

## Dependency license evidence

The fixed package metadata was inspected from the local NuGet cache:

| Package | License evidence | Distribution status |
| --- | --- | --- |
| `EasyWindowsTerminalControl` `1.0.38` | nuspec license expression `MIT`; upstream commit `0741a4b8853c47bcac4412d005ed4ae1d96d2c13` | Declared package terms; the MIT notice is included in a release-eligible archive. |
| `Microsoft.Windows.Console.ConPTY` `1.24.260710001` | nuspec license expression `MIT`; Microsoft Terminal project URL | Declared package terms; the MIT notice is included in a release-eligible archive. |
| `CI.Microsoft.Terminal.Wpf` `1.25.260303002` | The nuspec pins Microsoft Terminal commit `9ae724aa5b080aafbeea2bbf88db630b182cc802`; assembly metadata/source rebuild correlate the managed DLL, and the x64 native DLL hash matches the Microsoft release asset. The full upstream `LICENSE` and `NOTICE.md` are copied and hash-validated. | **VERIFIED** for the engineering release gate; see the [provenance record](compatibility-evidence/ci-microsoft-terminal-wpf-1.25.260303002.md). |

The initial Issue #21 assessment kept this dependency blocked because the
repackage had no package-local notices or independently checked source link.
The follow-up provenance record now includes the official WPF pack pipeline,
assembly metadata, an independent source build, the exact native hash match,
and full upstream `LICENSE`/`NOTICE.md` snapshots. This is an engineering
release-gate decision, not a legal conclusion; NuGet signatures and package
metadata remain distinct from a legal redistribution review. See the
[upstream repository](https://github.com/microsoft/terminal) and [WPF source
project](https://github.com/microsoft/terminal/tree/9ae724aa5b080aafbeea2bbf88db630b182cc802/src/cascadia/WpfTerminalControl)
for the pinned source.

## Explicit non-claims

- x86 and ARM64 are not product targets, even though the upstream package
  contains assets for those architectures.
- The current OS build does not establish a minimum Windows support version.
- A successful build does not prove Explorer, IME, Shell extension, elevation,
  cloud-provider, or packaged-overlay behavior.
- The host does not install or overwrite the user's persistent Yazi bridge
  configuration as part of the product implementation.

## External compatibility evidence

- The [Microsoft Terminal repository](https://github.com/microsoft/terminal)
  documents Windows 10 2004 (build 19041) or later for Windows Terminal.
- The fixed `Microsoft.Windows.Console.ConPTY` package metadata describes
  Windows `10.0.17763.0` or newer. This lower ConPTY floor does not override
  the higher embedded Windows Terminal candidate floor.
- The [NuGet package page](https://www.nuget.org/packages/CI.Microsoft.Terminal.Wpf/1.25.260303002)
  identifies the package owner as `CI2NugetRepackageTeam`, but does not provide
  license terms for the repackage. This is the Issue #21 binary-release
  blocker, not a confirmation of redistribution rights.

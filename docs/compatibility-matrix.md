# Compatibility Matrix and Distribution Gates

## Status

This document records the current validated fixture. It is not a promise that
every Windows version, CPU architecture, or Yazi release is supported.

## Current validated fixture

| Area | Validated value | Status | Boundary |
| --- | --- | --- | --- |
| Windows OS | OS version/build `10.0.26200` | PASS | The product edition and minimum supported Windows version are not declared. |
| Runtime architecture | `win-x64`, AMD64 | PASS | x86 and ARM64 assets are present but have not had a real host/Yazi run. |
| .NET SDK | `10.0.400` | PASS | The project targets `net10.0-windows`; no older SDK is claimed. |
| .NET runtime | `10.0.11`, Windows Desktop runtime | PASS | Framework-dependent versus self-contained distribution remains a release decision. |
| Yazi | `26.5.6`, revision `aa52643` | PASS | This exact version is the validated fixture; no compatibility range is claimed. |
| `ya` | `26.5.6`, revision `aa52643` | PASS | The bridge requires the paired Yazi/`ya` fixture. |
| Bridge protocol | `yazi-desktop-host/1` | PASS | Messages are instance-bound and use the Phase 2 framed sideband transport. |
| Bridge plugin | Repository revision `409cd5c2bc9298ee040fc2156ee86a0a2970fc12`; `main.lua` SHA-1 `1f3114030654b21ee49d23f46833519e7d62b325` | PASS | The plugin was tested through a temporary Yazi configuration. |
| Terminal host | `EasyWindowsTerminalControl` `1.0.38` | PASS | Selected for the current implementation; production packaging remains gated. |
| Windows Terminal dependency | `CI.Microsoft.Terminal.Wpf` `1.25.260303002` | PASS | Native assets are supplied for `win-x86`, `win-x64`, and `win-arm64`. |
| ConPTY dependency | `Microsoft.Windows.Console.ConPTY` `1.24.260710001` | PASS | The package metadata declares MIT and describes Windows `10.0.17763.0` or newer; this is not yet the product minimum. |
| Native ConPTY assets | `conpty.dll` for `win-x86`, `win-x64`, and `win-arm64` | PASS | Asset presence is not an architecture-specific runtime validation. |

The Release framework-dependent `win-x64` publish also passed with
`PublishReadyToRun=false` after restoring with the `win-x64` RID. The publish
directory contained `YaziDesktopHost.exe`,
`EasyWindowsTerminalControl.dll`, `Microsoft.Terminal.Control.dll`, and
`conpty.dll`. This proves package composition for the current x64 fixture; it
does not prove that the packaged host starts successfully on another machine.

## Manual capability result on the current fixture

The Windows run passed real Yazi launch, VT/alternate-screen rendering, 24-bit
color fixture output, CJK, keyboard, IME composition and commit, xterm mouse
reporting, resize/reflow, close cleanup, unexpected child exit handling, and
the required Explorer/Yazi drag-and-drop paths. Ctrl/Shift Copy/Move behavior
also passed. Ordinary keyboard navigation that changes `cwd` while an Explorer
drag is active is a v1 non-goal because the OLE drag loop does not deliver
those keys to Yazi.

## Distribution gates

The following gates remain open before claiming a distributable support range:

1. Declare the minimum Windows version and supported product editions.
2. Decide whether to ship framework-dependent or self-contained output.
3. Publish and run the host on each claimed architecture, including native
   asset loading and ConPTY startup.
4. Verify native HWND/WPF airspace behavior in the packaged application,
   including any overlays or future dialogs.
5. Record the supported Yazi/`ya` version policy and test at least one upgrade
   and one rejection path for an unsupported pair.
6. Resolve the `CI.Microsoft.Terminal.Wpf` licensing/redistribution evidence,
   then validate package notices, native asset redistribution, and installation
   layout before release.

Until these gates are closed, the support statement is limited to the current
Windows `win-x64` fixture and the exact Yazi/`ya` pair recorded above.

## Dependency license evidence

The fixed package metadata was inspected from the local NuGet cache:

| Package | License evidence | Distribution status |
| --- | --- | --- |
| `EasyWindowsTerminalControl` `1.0.38` | nuspec license expression `MIT`; upstream commit `0741a4b8853c47bcac4412d005ed4ae1d96d2c13` | Evidence recorded; verify notice obligations in the final package. |
| `Microsoft.Windows.Console.ConPTY` `1.24.260710001` | nuspec license expression `MIT`; Microsoft Terminal project URL | Evidence recorded; verify notice obligations in the final package. |
| `CI.Microsoft.Terminal.Wpf` `1.25.260303002` | nuspec has no license expression, license URL, or license file; repackage repository commit `9ae724aa5b080aafbeea2bbf88db630b182cc802` is recorded | **BLOCKED** until the upstream/repackage license and redistribution terms are confirmed. |

NuGet cache signatures and package metadata are not a substitute for a legal
redistribution review. The application must not claim a distributable package
until the CI package's terms and any required notices are resolved.

## Explicit non-claims

- Native assets for x86/ARM64 do not constitute a passing runtime test.
- The current OS build does not establish a minimum Windows support version.
- A successful build does not prove Explorer, IME, Shell extension, elevation,
  cloud-provider, or packaged-overlay behavior.
- The host does not install or overwrite the user's persistent Yazi bridge
  configuration as part of the product implementation.

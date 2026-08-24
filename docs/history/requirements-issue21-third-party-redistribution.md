# Issue #21 third-party redistribution requirements

## Goal

Prevent a new Yazi Terminal binary release until the license, notice, and
redistribution path of every published managed and native dependency has
evidence sufficient for the maintainer's release decision. This document is an
engineering release gate, not legal advice or a legal conclusion.

## Scope

- The `win-x64` framework-dependent publish path used by Yazi Terminal.
- `EasyWindowsTerminalControl` 1.0.38 and its resolved transitive packages:
  `CI.Microsoft.Terminal.Wpf` 1.25.260303002 and
  `Microsoft.Windows.Console.ConPTY` 1.24.260710001.
- The managed and native assets those packages place in the published output.
- Reproducible verification of a prospective ZIP/archive's notices and
  release-ready state.

## Non-goals

- Make a legal determination about the CI repackage.
- Change or delete the published v0.1.8 GitHub Release from this branch.
- Replace the terminal backend or upgrade dependencies.
- Claim that an upstream Microsoft Terminal license alone authorizes the
  separately published CI repackage.

## Requirements

1. Record the exact package versions, assets, and package-local license/notice
   evidence used by the `win-x64` publish graph.
2. Bundle the project license, the user documentation, the bridge plugin, and
   notices for dependencies whose license terms are evidenced into every
   eligible binary archive.
3. Make ordinary `dotnet publish` fail while any dependency has an unresolved
   redistribution gate, and provide a release script that performs the same
   gate before it creates an archive.
4. Provide a repeatable archive validator that checks the mandatory project,
   plugin, and third-party notice paths, and reports a blocked release state.
5. Record the v0.1.8 remediation policy without modifying the existing
   Release: do not publish a follow-up binary or represent that asset as
   remediated until the missing CI repackage evidence is obtained and its
   archive is replaced or withdrawn by an authorized maintainer.

## Acceptance criteria

- The manifest identifies every resolved third-party package and the assets it
  supplies to the current `win-x64` publish output.
- The source tree contains the notices supported by current package metadata
  for EasyWindowsTerminalControl and Microsoft Windows Console ConPTY.
- CI.Microsoft.Terminal.Wpf is explicitly marked `blocked`, with its missing
  package metadata and missing package notice recorded; a new binary publish
  fails by default.
- The validation scripts pass structural validation and demonstrably reject a
  release-ready check while the blocker is open.
- README and the user manual make the release gate and existing-release policy
  visible to maintainers/users without claiming a legal conclusion.

## Requirements review (round 1)

No BLOCKER or MAJOR finding. The only external dependency is confirmation from
the CI repackage publisher; it is an intentional release blocker rather than a
reason to infer permission. Residual MINOR: final legal/maintainer review must
decide whether v0.1.8 is replaced or withdrawn after evidence is received.

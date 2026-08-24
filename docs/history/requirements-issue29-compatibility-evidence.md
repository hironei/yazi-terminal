# Issue #29 compatibility-evidence requirements

## Goal

Make the release artifact and bridge-plugin evidence attributable to an exact
release archive, source commit, and plugin revision. Do not represent older
manual fixtures as validation of newer code.

## Scope

- The public `v0.1.8` `win-x64` ZIP and its exact contents.
- Current source commit `c65ff9eef68ef1af3a338587ca3a1ceb67836de5` and the
  `yazi-desktop-host.yazi/main.lua` Git blob it contains.
- Automated host protocol tests for bridge parsing, command catalogs, and
  reconnect handling.
- A repeatable artifact/evidence generation and validation procedure for later
  releases.

## Non-goals

- Treat static inspection or host-only tests as a live Yazi plugin run.
- Bypass the Issue #21 fail-closed binary-release gate or create a new release.
- Claim that a historical Yazi 26.5.6 fixture validates the locally installed
  Yazi 26.8.15.
- Change the bridge protocol, terminal backend, or plugin behavior.

## Requirements

1. Store release evidence containing the tag, archive SHA-256, complete entry
   list, executable file/product version, source commit, plugin Git blob, and
   plugin SHA-256.
2. Verify that an inspected release archive exactly matches the evidence and
   that its bundled plugin bytes match the recorded current source plugin.
3. Verify the current plugin's Lua syntax and bridge/catalog/reconnect source
   markers, while marking a real Yazi plugin fixture as not run unless it is
   actually performed.
4. Keep the historical manual fixture and its old plugin revision visible but
   explicitly historical; current observations must not use an unqualified
   `PASS` for unexecuted live/plugin/GUI behavior.
5. Document a release-by-release update procedure that starts from a downloaded
   archive and fails when the artifact and current source plugin differ.

## Acceptance criteria

- `v0.1.8.json` records the public archive and current source/plugin identity.
- The evidence validator succeeds for the matching source and archive and
  rejects source or archive drift.
- Existing executable host tests pass, including command-catalog and reconnect
  cases; the separate Lua parse check passes.
- Documentation states which evidence is observed, automated, historical, or
  not run.

## Requirements review (round 1)

No BLOCKER or MAJOR finding. The live Yazi integration gap is deliberately
expressed as `not-run`, not inferred from host tests or source inspection.
Residual MINOR: a future release must rerun the procedure after the Issue #21
release gate permits an authorized artifact to exist.

# Issue #29 compatibility-evidence design

## Evidence model

Each release has one JSON record under `compatibility-evidence/`. It binds an
archive by SHA-256 and complete ZIP entry list to a source commit and the raw
bytes/Git blob of the bundled bridge plugin. It records observations only;
automated host protocol and live-Yazi results remain separately classified.

```text
downloaded release ZIP
        │
        ├── Update-CompatibilityEvidence.ps1 ──> vX.Y.Z.json
        │          │                                  │
current HEAD/plugin ─────────────────────────────────┘
        │
        └── Test-CompatibilityEvidence.ps1
                 ├── source commit/blob/hash + bridge markers
                 └── ZIP SHA-256 + complete entry list
```

The update command refuses to create evidence when the archive's bundled
`main.lua` differs from the current source plugin. This makes an accidental
comparison between a release artifact and an unrelated checkout fail closed.

## Verification states

- `observed`: bytes or metadata were directly inspected.
- `PASS`: a named automated command was actually run and passed.
- `historical PASS`: an earlier, pinned manual fixture passed; it is not a
  current-source claim.
- `not-run`: no applicable runtime fixture was executed.

The initial v0.1.8 record uses `observed` for artifact/plugin bytes and
`not-run` for the live Yazi plugin fixture. Current host protocol tests are
reported in the compatibility matrix, not encoded as an artifact property.

## Release procedure

From a clean checkout at the release commit, download the published ZIP without
modifying the Release, then run:

```powershell
wsl gh release download vX.Y.Z --repo hironei/yazi-terminal --pattern 'YaziTerminal-vX.Y.Z-win-x64.zip' --dir <temporary-directory>
.\eng\Update-CompatibilityEvidence.ps1 -ReleaseTag vX.Y.Z -ArchivePath <temporary-directory>\YaziTerminal-vX.Y.Z-win-x64.zip
.\eng\Test-CompatibilityEvidence.ps1 -RequireCurrentSource -RequireArchive -ArchivePath <temporary-directory>\YaziTerminal-vX.Y.Z-win-x64.zip
```

Run the normal host test suite and an actual temporary Yazi configuration
fixture separately. Record that fixture as live PASS only after bridge frames,
command catalog, and reconnect behavior have all been observed. If the
Issue #21 redistribution gate blocks publish, retain the existing artifact
record and do not override the gate to manufacture new evidence.

## Design review (round 1)

No BLOCKER or MAJOR finding. The archive/source binding, complete entry list,
and explicit state labels avoid the stale-evidence failure without changing
release policy. Residual MINOR: release asset download requires GitHub access;
the evidence JSON preserves the result for offline re-validation when the ZIP
is available.

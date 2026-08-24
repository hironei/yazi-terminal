# Issue #30: Phase 2 Bridge Acceptance-Test Traceability

This record maps each line-level Phase 2 acceptance item in
`requirements-phase2-yazi-bridge.md` lines 138-145 to one named executable
test. The tests run only against the current source compiled into the
`YaziDesktopHost.Tests` artifact; they do not use terminal screen text,
rendered coordinates, or an `OutputText` property.

| Requirement | Named test | Primary acceptance layer | Coverage |
| --- | --- | --- | --- |
| 138 | `Phase 2 AC 138 parser accepts valid UTF-8 snapshot` | Parser | Valid UTF-8 JSON snapshot with CJK, spaces, and a surrogate-pair filename. |
| 139 | `Phase 2 AC 139 parser and frame reader reject invalid frames` | Frame reader, Parser | Oversized newline frame, malformed JSON, oversized parser input, protocol mismatch, and instance mismatch. |
| 140 | `Phase 2 AC 140 reducer rejects invalid path kinds and required fields` | Reducer | Unknown path kind and missing snapshot `cwd`; the envelope parser rejects missing envelope fields in AC 139. |
| 141 | `Phase 2 AC 141 reducer accepts first snapshot and ordered empty selection` | Reducer | Hello, first snapshot, exactly-next update, cleared hover, and empty selection. |
| 142 | `Phase 2 AC 142 reducer rejects duplicate out-of-order and gap updates` | Reducer | Duplicate, out-of-order, and gap sequences all invalidate state. |
| 143 | `Phase 2 AC 143 session rejects goodbye and error then requires a snapshot` | Session, Reducer | `goodbye` and `error` disconnect reasons; reconnect state update is rejected until a fresh hello/snapshot. |
| 144 | `Phase 2 AC 144 fixtures preserve CJK surrogate long and root paths` | Parser, Reducer | CJK/spaces, surrogate pair, 32,000-character path, and filesystem root. |
| 145 | `Phase 2 AC 145 fixtures preserve URLs without terminal output` | Parser, Reducer | Non-filesystem URL remains a URL DTO and test code does not inspect terminal output or emit path logs. |

The frame reader rejects oversize transport data before the parser runs. The
parser rejects UTF-8, JSON, envelope, protocol, and pending-instance failures.
The reducer rejects path shape and ordering/lifecycle violations. The session
converts goodbye/error/disconnect transport outcomes into unavailable state
before accepting a replacement connection. Real Yazi, ConPTY rendering,
current-user access checks from another account, and WPF lifecycle behavior
remain Windows manual acceptance items.

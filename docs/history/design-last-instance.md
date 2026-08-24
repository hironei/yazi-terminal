# Last-instance command routing design

## Composition

```text
App startup
├── CommandLineOptions
├── LastInstanceClient ──> last-instance metadata ──> per-window control pipe
└── MainWindow
    ├── LastInstanceControlServer
    ├── LastInstanceRegistry
    └── YaziDirectoryController ──> ya emit-to <client-id> cd <directory>
```

The existing Yazi state bridge remains a separate Yazi-plugin-to-host stream.
The control pipe is not mixed into that protocol.

## Startup and fallback

`App` removes `StartupUri` and handles command-line startup itself. A normal
invocation constructs a new `MainWindow`. A `--last-instance` invocation reads
the per-user endpoint record and sends one control request with a single
deadline covering connection, write, and acknowledgement read. If reading,
connecting, processing, or acknowledging the request fails, `App` constructs a
new window using the same requested directory. No error dialog is shown for a
missing or stale last instance.

## Endpoint registry

The registry stores only the current instance's random control pipe name under
the user's local application-data directory. Publication and conditional
removal are serialized by a user-local named mutex. Publication returns
failure when the mutex or metadata write cannot be completed; server startup
tears down its pipe and disables last-instance control while allowing the
normal window startup to continue. Removal compares the pipe name before
deleting, so an older window cannot remove a newer window's record.

Each control pipe uses `PipeOptions.CurrentUserOnly`. The endpoint name is
unguessable and the message parser still validates the protocol, command, and
path fields.

## Control protocol

Control messages are one UTF-8 JSON object per newline-delimited frame. Both
request and acknowledgement reads use a maximum byte count before allocating
or parsing a frame, and malformed or oversized frames receive a negative
acknowledgement when the connection is still writable:

```json
{"protocol":"yazi-desktop-host/control/1","command":"cd","path":"C:\\work"}
```

The server returns a small acknowledgement only after the WPF dispatch and
`ya.exe` command have completed successfully. Paths are passed to
`ProcessStartInfo.ArgumentList` for the documented Yazi
`ya emit-to <receiver> cd <path>` command; they are never concatenated into
shell syntax. The paired `ya.exe` is resolved beside the selected `yazi.exe`,
then from `PATH`.

## WPF/Yazi lifecycle

The control server starts before publishing its endpoint and runs until window
shutdown. Path requests are dispatched on the WPF thread and serialized as
complete transactions so concurrent requests cannot reorder `ya` operations or
split a file `reveal → open` pair. A request received while
the dispatcher is shutting down, or before Yazi and its paired `ya.exe` are
processable, receives a negative acknowledgement and therefore follows the
normal new-window fallback. Accepted requests activate and restore the window
before running the Yazi path transaction on the UI lifecycle.

The existing bridge environment and state session are unchanged. The random
numeric Yazi client ID is retained by the host so the official `ya emit-to`
route can address the active Yazi process without keystroke injection.

## Test seams

- Pure command-line parser tests cover normal, positional, and
  `--last-instance` forms.
- Pure control protocol tests cover valid frames, malformed JSON, unsupported
  protocol/command, empty paths, and line-break rejection.
- Registry tests use an injected storage path and mutex name.
- Client/server tests use an injectable pipe or local named-pipe integration.
- `ya emit-to`, WPF activation, and actual Yazi directory movement remain live
  Windows acceptance checks.

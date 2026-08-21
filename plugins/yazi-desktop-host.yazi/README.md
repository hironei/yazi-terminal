# yazi-desktop-host bridge probe

This is an opt-in compatibility probe for Yazi `26.5.6`. It is not enabled by
the WPF host automatically and is not yet the production plugin.

Copy this directory to `%AppData%\yazi\config\plugins\yazi-desktop-host.yazi`
and add the following to `%AppData%\yazi\config\init.lua`:

```lua
require("yazi-desktop-host"):setup {}
```

The host supplies `YAZI_DESKTOP_HOST_PIPE` and
`YAZI_DESKTOP_HOST_INSTANCE_ID` to the Yazi child. The plugin polls the Yazi
context in an async task, writes a hello followed by snapshots/state updates,
and stops when the pipe write fails. `path_kind = "url"` can be supplied for
non-filesystem Yazi URLs.

The implementation intentionally uses documented `ya.sync`, `ya.async`,
`cx.active.current`, `cx.active.selected`, and `fs.access` APIs. Opening a
Windows named pipe through Yazi's filesystem API still requires a real Windows
Yazi run; until that check passes, this remains a probe and the host must treat
bridge state as unavailable.

The polling interval defaults to 100 ms. It is deliberately conservative and
must be replaced by event-driven publication or an accepted performance budget
before production use.

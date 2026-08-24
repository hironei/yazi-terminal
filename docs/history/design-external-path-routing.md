# External path routing design

## Startup target

`CommandLineOptions` resolves the one positional path to a full path. An
existing file becomes `FilePath`, while `InitialDirectory` becomes its parent.
All other paths, including a missing positional path, remain directory targets.

`App` passes both values to `MainWindow` for a new window. The window starts
Yazi with `WorkingDirectory = InitialDirectory`. After `TermReady`, it sends
`reveal "<file>"` and then `open` through `ya emit-to`. Yazi owns the opener
selection, so the user's configured editor is used when the file's open rule
selects the edit opener.

## Last-instance control

The existing control protocol retains protocol version 1 and adds the `open`
command alongside `cd`. A request carries a fully qualified path and a typed
`LastInstanceControlCommand`. The client keeps the current directory overload
for compatibility and adds a request overload for file targets.

The WPF control server dispatches the typed request to the UI thread. Both
operations use the existing serialized request gate and foreground activation;
directory requests call the directory controller, while file requests call the
file controller. A failed reveal or open produces a negative acknowledgement,
so the caller follows the existing fallback behavior.

## Test seams and risks

- Parser tests use a temporary real file to verify parent-directory routing.
- Protocol tests cover both `cd` and `open` commands and their paths.
- Controller tests verify `ArgumentList` boundaries for reveal and open.
- A live Windows run is still required to verify `TermReady` timing, Yazi's
  configured editor, foreground activation, and behavior for non-text files.

using System.IO.Pipes;
using System.Reflection;
using System.Text;

using YaziDesktopHost;

var tests = new (string Name, Action Test)[]
{
    ("explicit path takes precedence", ExplicitPathTakesPrecedence),
    ("PATH lookup finds yazi.exe", PathLookupFindsExecutable),
    ("missing executable is classified", MissingExecutableIsClassified),
    ("bridge parser accepts a CJK snapshot", BridgeParserAcceptsCjkSnapshot),
    ("bridge parser rejects a wrong instance", BridgeParserRejectsWrongInstance),
    ("bridge reducer applies an ordered update", BridgeReducerAppliesOrderedUpdate),
    ("bridge reducer invalidates a sequence gap", BridgeReducerInvalidatesSequenceGap),
    ("bridge reducer requires a fresh snapshot after disconnect", BridgeReducerRequiresFreshSnapshot),
    ("bridge pipe round-trips a framed message", BridgePipeRoundTripsFrame),
    ("bridge session reconnects after disconnect", BridgeSessionReconnectsAfterDisconnect),
    ("Yazi command line uses bridge identity", YaziCommandLineUsesBridgeIdentity),
    ("bridge environment scope restores values", BridgeEnvironmentScopeRestoresValues),
    ("shell target prefers selected paths", ShellTargetPrefersSelectedPaths),
    ("shell target preserves multiple selection", ShellTargetPreservesMultipleSelection),
    ("shell target falls back to hovered path", ShellTargetFallsBackToHoveredPath),
    ("shell target normalizes Windows path separators", ShellTargetNormalizesWindowsPathSeparators),
    ("shell target normalizes file URI", ShellTargetNormalizesFileUri),
    ("shell target resolves current directory", ShellTargetResolvesCurrentDirectory),
    ("shell target rejects unavailable, URLs, and empty state", ShellTargetRejectsUnavailableUrlsAndEmptyState),
    ("shell context COM interfaces preserve native vtable order", ShellContextComInterfacesPreserveNativeVtableOrder),
    ("theme palettes keep dark defaults and distinct light colors", ThemePalettesKeepDistinctModes),
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception}");
    }
}

foreach (var failure in failures)
{
    Console.Error.WriteLine(failure);
}

return failures.Count == 0 ? 0 : 1;

static void ExplicitPathTakesPrecedence()
{
    var checkedPaths = new List<string>();
    var result = YaziExecutableResolver.Resolve(
        @"C:\custom\yazi.exe",
        @"C:\tools",
        path =>
        {
            checkedPaths.Add(path);
            return path.EndsWith(@"custom\yazi.exe", StringComparison.OrdinalIgnoreCase);
        });

    Assert(result.EndsWith(@"custom\yazi.exe", StringComparison.OrdinalIgnoreCase));
    Assert(checkedPaths.Count == 1);
}

static void PathLookupFindsExecutable()
{
    var result = YaziExecutableResolver.Resolve(
        null,
        @"C:\missing;C:\tools",
        path => path.Equals(@"C:\tools\yazi.exe", StringComparison.OrdinalIgnoreCase));

    Assert(result.Equals(@"C:\tools\yazi.exe", StringComparison.OrdinalIgnoreCase));
}

static void MissingExecutableIsClassified()
{
    try
    {
        YaziExecutableResolver.Resolve(null, @"C:\missing", _ => false);
        throw new InvalidOperationException("Expected YaziExecutableNotFoundException.");
    }
    catch (YaziExecutableNotFoundException)
    {
        // Expected.
    }
}

static void BridgeParserAcceptsCjkSnapshot()
{
    var instanceId = Guid.NewGuid();
    var frame = Frame(
        instanceId,
        1,
        "snapshot",
        new
        {
            tab = 0,
            cwd = new { kind = "filesystem", value = @"C:\資料" },
            hovered = new { kind = "filesystem", value = @"C:\資料\日本語.txt" },
            selected = Array.Empty<object>(),
        });

    var message = new YaziBridgeMessageParser().Parse(frame, instanceId);
    Assert(message.Kind == YaziBridgeMessageKind.Snapshot);
    Assert(message.Sequence == 1);
}

static void BridgeParserRejectsWrongInstance()
{
    var actualInstanceId = Guid.NewGuid();
    var wrongInstanceId = Guid.NewGuid();
    var frame = Frame(wrongInstanceId, 0, "hello", new { });

    Expect<YaziBridgeProtocolException>(() => new YaziBridgeMessageParser().Parse(frame, actualInstanceId));
}

static void BridgeReducerAppliesOrderedUpdate()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    reducer.Apply(parser.Parse(
        Frame(
            instanceId,
            2,
            "state",
            new
            {
                present = new[] { "hovered", "selected" },
                hovered = (object?)null,
                selected = new[] { new { kind = "filesystem", value = @"C:\資料\選択.txt" } },
            }),
        instanceId));

    Assert(reducer.State is not null);
    Assert(reducer.State!.Hovered is null);
    Assert(reducer.State.Selected.Count == 1);
    Assert(reducer.State.Selected[0].Value.EndsWith("選択.txt", StringComparison.Ordinal));
    Assert(reducer.State.Sequence == 2);
}

static void BridgeReducerInvalidatesSequenceGap()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    reducer.Apply(parser.Parse(StateFrame(instanceId, 3), instanceId));

    Assert(reducer.State is null);
    Assert(reducer.Availability == YaziBridgeAvailability.Unavailable);
}

static void BridgeReducerRequiresFreshSnapshot()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    reducer.MarkDisconnected();
    reducer.Apply(parser.Parse(StateFrame(instanceId, 2), instanceId));

    Assert(reducer.State is null);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 10), instanceId));
    Assert(reducer.State is not null);
    Assert(reducer.State!.Sequence == 10);
}

static void BridgePipeRoundTripsFrame()
{
    var instanceId = Guid.NewGuid();
    using var server = new YaziBridgePipeServer(instanceId);
    Assert(server.PipePath == $@"\\.\pipe\{server.PipeName}");
    var acceptTask = server.AcceptAsync();
    using var client = new NamedPipeClientStream(
        ".",
        server.PipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);
    client.Connect(5000);

    using var connection = acceptTask.GetAwaiter().GetResult();
    var frame = Frame(instanceId, 0, "hello", new { });
    var serverReadTask = connection.ReadFrameAsync();
    client.Write(frame, 0, frame.Length);
    client.WriteByte((byte)'\r');
    client.WriteByte((byte)'\n');
    client.Flush();

    var received = serverReadTask.GetAwaiter().GetResult();
    Assert(received is not null && received.SequenceEqual(frame));

    using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
    var clientReadTask = reader.ReadLineAsync();
    connection.WriteFrameAsync(frame).GetAwaiter().GetResult();
    Assert(clientReadTask.GetAwaiter().GetResult() == Encoding.UTF8.GetString(frame));
}

static void BridgeSessionReconnectsAfterDisconnect()
{
    var instanceId = Guid.NewGuid();
    using var server = new YaziBridgePipeServer(instanceId);
    var session = new YaziBridgeSession(instanceId, server);
    var states = new List<YaziBridgeState?>();
    var disconnectReasons = new List<string>();
    session.StateChanged += state =>
    {
        lock (states)
        {
            states.Add(state);
        }
    };
    session.Disconnected += reason =>
    {
        lock (disconnectReasons)
        {
            disconnectReasons.Add(reason);
        }
    };
    var runTask = session.RunAsync();

    using (var client = new NamedPipeClientStream(
        ".",
        server.PipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous))
    {
        client.Connect(5000);
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, SnapshotFrame(instanceId, 1));
    }

    WaitUntil(() =>
    {
        lock (states)
        {
            return states.Any(state => state is null);
        }
    });

    using (var client = new NamedPipeClientStream(
        ".",
        server.PipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous))
    {
        client.Connect(5000);
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, SnapshotFrame(instanceId, 1));
    }

    session.DisposeAsync().AsTask().GetAwaiter().GetResult();
    runTask.GetAwaiter().GetResult();

    YaziBridgeState?[] observedStates;
    lock (states)
    {
        observedStates = states.ToArray();
    }

    string[] observedReasons;
    lock (disconnectReasons)
    {
        observedReasons = disconnectReasons.ToArray();
    }

    Assert(observedStates.Count(state => state is not null) >= 2);
    Assert(observedStates[0] is not null && observedStates[0]!.Sequence == 1);
    Assert(observedStates[^1] is null);
    Assert(observedReasons.Contains("disconnect", StringComparer.Ordinal));
}

static void SendFrame(NamedPipeClientStream client, byte[] frame)
{
    client.Write(frame, 0, frame.Length);
    client.WriteByte((byte)'\n');
    client.Flush();
}

static void WaitUntil(Func<bool> condition)
{
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (!condition())
    {
        if (DateTime.UtcNow >= deadline)
        {
            throw new InvalidOperationException("Timed out waiting for the bridge state.");
        }

        Thread.Sleep(10);
    }
}

static void YaziCommandLineUsesBridgeIdentity()
{
    var commandLine = YaziProcessLaunchConfiguration.CreateCommandLine(@"C:\tools\yazi.exe");
    var clientId = commandLine.Split("--client-id ", StringSplitOptions.None).Last();
    Assert(long.TryParse(clientId, out var numericClientId) && numericClientId > 0);
}

static void BridgeEnvironmentScopeRestoresValues()
{
    var names = new[]
    {
        "YAZI_DESKTOP_HOST_PIPE",
        "YAZI_DESKTOP_HOST_INSTANCE_ID",
        "YAZI_DESKTOP_HOST_PROTOCOL",
    };
    var previousValues = names.ToDictionary(
        name => name,
        name => Environment.GetEnvironmentVariable(name),
        StringComparer.OrdinalIgnoreCase);

    try
    {
        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, $"previous-{name}");
        }

        var instanceId = Guid.NewGuid();
        using (YaziProcessLaunchConfiguration.EnterBridgeEnvironment(instanceId, @"\\.\pipe\yazi-test"))
        {
            Assert(Environment.GetEnvironmentVariable("YAZI_DESKTOP_HOST_PIPE") == @"\\.\pipe\yazi-test");
            Assert(Environment.GetEnvironmentVariable("YAZI_DESKTOP_HOST_INSTANCE_ID") == instanceId.ToString("D"));
            Assert(Environment.GetEnvironmentVariable("YAZI_DESKTOP_HOST_PROTOCOL") == YaziBridgeMessageParser.SupportedProtocol);
        }

        foreach (var name in names)
        {
            Assert(Environment.GetEnvironmentVariable(name) == $"previous-{name}");
        }
    }
    finally
    {
        foreach (var (name, value) in previousValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}

static void ShellTargetPrefersSelectedPaths()
{
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, @"C:\work\hovered.txt"),
        selected: [new(YaziBridgePathKind.Filesystem, @"C:\work\selected.txt")]);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual([@"C:\work\selected.txt"]));
}

static void ShellTargetFallsBackToHoveredPath()
{
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, @"C:\資料\日本語.txt"),
        selected: []);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual([@"C:\資料\日本語.txt"]));
}

static void ShellTargetNormalizesWindowsPathSeparators()
{
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, "C:/work/hovered.txt"),
        selected: []);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual([@"C:\work\hovered.txt"]));
}

static void ShellTargetNormalizesFileUri()
{
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, "file:///C:/work/hovered.txt"),
        selected: []);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual([@"C:\work\hovered.txt"]));
}

static void ShellTargetPreservesMultipleSelection()
{
    var selected = new YaziBridgePath[]
    {
        new(YaziBridgePathKind.Filesystem, @"C:\work\first.txt"),
        new(YaziBridgePathKind.Filesystem, @"C:\work\second.txt"),
    };
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, @"C:\work\hovered.txt"),
        selected);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual(selected.Select(path => path.Value)));
}

static void ShellTargetResolvesCurrentDirectory()
{
    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Filesystem, @"C:\work\hovered.txt"),
        selected: [new(YaziBridgePathKind.Filesystem, @"C:\work\selected.txt")]);

    var result = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.CurrentDirectory);

    Assert(result.Status == YaziShellTargetStatus.Available);
    Assert(result.Target!.Paths.SequenceEqual([@"C:\work"]));
}

static void ShellTargetRejectsUnavailableUrlsAndEmptyState()
{
    var unavailable = YaziShellTargetResolver.Resolve(null, YaziShellInvocation.CurrentDirectory);
    Assert(unavailable.Status == YaziShellTargetStatus.Unavailable);

    var state = AvailableState(
        hovered: new YaziBridgePath(YaziBridgePathKind.Url, "archive://remote/item"),
        selected: []);
    var unsupported = YaziShellTargetResolver.Resolve(state, YaziShellInvocation.SelectedOrHovered);
    Assert(unsupported.Status == YaziShellTargetStatus.Unsupported);

    var empty = YaziShellTargetResolver.Resolve(
        AvailableState(hovered: null, selected: []),
        YaziShellInvocation.SelectedOrHovered);
    Assert(empty.Status == YaziShellTargetStatus.Empty);
}

static void ShellContextComInterfacesPreserveNativeVtableOrder()
{
    var serviceType = typeof(WindowsShellContextMenuService);
    AssertDeclaredMethods(
        serviceType,
        "IContextMenu2",
        "QueryContextMenu",
        "InvokeCommand",
        "GetCommandString",
        "HandleMenuMsg");
    AssertDeclaredMethods(
        serviceType,
        "IContextMenu3",
        "QueryContextMenu",
        "InvokeCommand",
        "GetCommandString",
        "HandleMenuMsg",
        "HandleMenuMsg2");
}

static void ThemePalettesKeepDistinctModes()
{
    var dark = ThemePalette.For(AppThemeMode.Dark);
    var light = ThemePalette.For(AppThemeMode.Light);

    Assert(dark.TerminalBackground == new RgbColor(0, 0, 0));
    Assert(dark.TerminalForeground == new RgbColor(255, 255, 255));
    Assert(light.TerminalBackground == new RgbColor(251, 251, 251));
    Assert(light.TerminalForeground == new RgbColor(31, 31, 31));
    Assert(dark.HostBackground != light.HostBackground);
    Assert(dark.TerminalSelectionBackground != light.TerminalSelectionBackground);
    Assert(dark.TerminalColorTable.Count == 16);
    Assert(light.TerminalColorTable.Count == 16);
    Assert(light.TerminalColorTable[15] == new RgbColor(64, 64, 64));
}

static void AssertDeclaredMethods(Type declaringType, string nestedTypeName, params string[] expectedNames)
{
    var nestedType = declaringType.GetNestedType(nestedTypeName, BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing nested type: {nestedTypeName}");
    var actualNames = nestedType
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .OrderBy(method => method.MetadataToken)
        .Select(method => method.Name)
        .ToArray();
    Assert(actualNames.SequenceEqual(expectedNames));
}

static YaziBridgeState AvailableState(YaziBridgePath? hovered, IReadOnlyList<YaziBridgePath> selected) =>
    new(
        Guid.NewGuid(),
        5,
        0,
        new YaziBridgePath(YaziBridgePathKind.Filesystem, @"C:\work"),
        hovered,
        selected,
        YaziBridgeAvailability.Available);

static byte[] SnapshotFrame(Guid instanceId, ulong sequence) => Frame(
    instanceId,
    sequence,
    "snapshot",
    new
    {
        tab = 0,
        cwd = new { kind = "filesystem", value = @"C:\work" },
        hovered = (object?)null,
        selected = Array.Empty<object>(),
    });

static byte[] StateFrame(Guid instanceId, ulong sequence) => Frame(
    instanceId,
    sequence,
    "state",
    new { present = new[] { "tab" }, tab = 0 });

static byte[] HelloFrame(Guid instanceId) => Frame(instanceId, 0, "hello", new { });

static byte[] Frame(Guid instanceId, ulong sequence, string kind, object payload) =>
    System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
    {
        protocol = YaziBridgeMessageParser.SupportedProtocol,
        instanceId,
        sequence,
        kind,
        payload,
    }));

static void Expect<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Assertion failed.");
    }
}

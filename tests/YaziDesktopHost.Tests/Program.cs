using System.IO.Pipes;
using System.Reflection;
using System.Text;

using YaziDesktopHost;

var tests = new (string Name, Action Test)[]
{
    ("explicit path takes precedence", ExplicitPathTakesPrecedence),
    ("PATH lookup finds yazi.exe", PathLookupFindsExecutable),
    ("paired ya lookup prefers the yazi directory", PairedYaLookupPrefersYaziDirectory),
    ("missing executable is classified", MissingExecutableIsClassified),
    ("command line options preserve new-window default", CommandLineOptionsPreserveNewWindowDefault),
    ("command line options select last instance", CommandLineOptionsSelectLastInstance),
    ("last-instance protocol validates control frames", LastInstanceProtocolValidatesControlFrames),
    ("last-instance frame reader rejects oversized frames", LastInstanceFrameReaderRejectsOversizedFrames),
    ("last-instance registry publishes and removes current endpoint", LastInstanceRegistryPublishesAndRemovesCurrentEndpoint),
    ("last-instance registry handles missing and malformed metadata", LastInstanceRegistryHandlesMissingAndMalformedMetadata),
    ("last-instance control server disables after publication failure", LastInstanceControlServerDisablesAfterPublicationFailure),
    ("last-instance client falls back for an unreachable endpoint", LastInstanceClientFallsBackForUnreachableEndpoint),
    ("last-instance client rejects invalid endpoint names", LastInstanceClientRejectsInvalidEndpointNames),
    ("last-instance client times out while waiting for ACK", LastInstanceClientTimesOutWhileWaitingForAcknowledgement),
    ("last-instance client rejects an invalid ACK frame", LastInstanceClientRejectsInvalidAcknowledgementFrame),
    ("last-instance control pipe accepts a directory request", LastInstanceControlPipeAcceptsDirectoryRequest),
    ("last-instance control pipe returns a negative ACK", LastInstanceControlPipeReturnsNegativeAcknowledgement),
    ("bridge parser accepts a CJK snapshot", BridgeParserAcceptsCjkSnapshot),
    ("bridge parser rejects a wrong instance", BridgeParserRejectsWrongInstance),
    ("bridge reducer applies an ordered update", BridgeReducerAppliesOrderedUpdate),
    ("bridge reducer invalidates a sequence gap", BridgeReducerInvalidatesSequenceGap),
    ("bridge reducer requires a fresh snapshot after disconnect", BridgeReducerRequiresFreshSnapshot),
    ("bridge pipe round-trips a framed message", BridgePipeRoundTripsFrame),
    ("bridge session reconnects after disconnect", BridgeSessionReconnectsAfterDisconnect),
    ("Yazi command line uses bridge identity", YaziCommandLineUsesBridgeIdentity),
    ("Yazi directory command preserves argument boundaries", YaziDirectoryCommandPreservesArgumentBoundaries),
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

static void PairedYaLookupPrefersYaziDirectory()
{
    var checkedPaths = new List<string>();
    var result = YaziExecutableResolver.ResolvePairedYa(
        @"C:\tools\yazi.exe",
        @"C:\other",
        path =>
        {
            checkedPaths.Add(path);
            return path.Equals(@"C:\tools\ya.exe", StringComparison.OrdinalIgnoreCase)
                || path.Equals(@"C:\other\ya.exe", StringComparison.OrdinalIgnoreCase);
        });

    Assert(result.Equals(@"C:\tools\ya.exe", StringComparison.OrdinalIgnoreCase));
    Assert(checkedPaths.Count == 1);
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

static void CommandLineOptionsPreserveNewWindowDefault()
{
    var options = CommandLineOptions.Parse([], @"C:\work");

    Assert(!options.UseLastInstance);
    Assert(options.InitialDirectory.Equals(@"C:\work", StringComparison.OrdinalIgnoreCase));

    var positional = CommandLineOptions.Parse([@"C:\project"], @"C:\work");
    Assert(!positional.UseLastInstance);
    Assert(positional.InitialDirectory.Equals(@"C:\project", StringComparison.OrdinalIgnoreCase));
}

static void CommandLineOptionsSelectLastInstance()
{
    var options = CommandLineOptions.Parse(
        [CommandLineOptions.LastInstanceOption, @"C:\日本語\project"],
        @"C:\work");

    Assert(options.UseLastInstance);
    Assert(options.InitialDirectory.Equals(@"C:\日本語\project", StringComparison.OrdinalIgnoreCase));

    var currentDirectory = CommandLineOptions.Parse(
        [CommandLineOptions.LastInstanceOption],
        @"C:\work");
    Assert(currentDirectory.UseLastInstance);
    Assert(currentDirectory.InitialDirectory.Equals(@"C:\work", StringComparison.OrdinalIgnoreCase));
}

static void LastInstanceProtocolValidatesControlFrames()
{
    var request = new LastInstanceControlRequest(@"C:\work\日本語 folder");
    var frame = LastInstanceControlProtocol.Serialize(request);

    Assert(LastInstanceControlProtocol.TryParse(frame, out var parsed));
    Assert(parsed!.Path == request.Path);
    Assert(!LastInstanceControlProtocol.TryParse(
        frame.Replace(LastInstanceControlProtocol.SupportedProtocol, "wrong", StringComparison.Ordinal),
        out _));
    Assert(!LastInstanceControlProtocol.TryParse(
        "{\"protocol\":\"" + LastInstanceControlProtocol.SupportedProtocol + "\",\"command\":\"shell\",\"path\":\"C:\\\\work\"}",
        out _));
    Assert(!LastInstanceControlProtocol.TryParse(
        LastInstanceControlProtocol.Serialize(new LastInstanceControlRequest("relative")),
        out _));
    Assert(LastInstanceControlProtocol.IsAcceptedAcknowledgement(
        LastInstanceControlProtocol.SerializeAcknowledgement(true)));
    Assert(!LastInstanceControlProtocol.IsAcceptedAcknowledgement(
        LastInstanceControlProtocol.SerializeAcknowledgement(false)));
}

static void LastInstanceFrameReaderRejectsOversizedFrames()
{
    var oversized = Encoding.UTF8.GetBytes(
        new string('x', LastInstanceControlProtocol.MaxFrameBytes + 1) + "\n");

    Expect<InvalidDataException>(() => LastInstanceFrame.ReadAsync(
        new MemoryStream(oversized),
        LastInstanceControlProtocol.MaxFrameBytes,
        CancellationToken.None).GetAwaiter().GetResult());
}

static void LastInstanceRegistryPublishesAndRemovesCurrentEndpoint()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-test-");
    try
    {
        var metadataPath = Path.Combine(directory.FullName, "last.json");
        var registry = new LastInstanceRegistry(metadataPath, $@"Local\yazi-test-{Guid.NewGuid():N}");
        var currentPipe = $"yazi-terminal-control-{Guid.NewGuid():N}";
        var newPipe = $"yazi-terminal-control-{Guid.NewGuid():N}";
        var olderPipe = $"yazi-terminal-control-{Guid.NewGuid():N}";
        registry.Publish(currentPipe);

        Assert(registry.TryRead(out var endpoint));
        Assert(endpoint!.PipeName == currentPipe);

        Assert(registry.Publish(newPipe));
        Assert(registry.TryRead(out endpoint));
        Assert(endpoint!.PipeName == newPipe);

        registry.RemoveIfCurrent(olderPipe);
        Assert(registry.TryRead(out endpoint));
        Assert(endpoint!.PipeName == newPipe);

        registry.RemoveIfCurrent(currentPipe);
        Assert(registry.TryRead(out endpoint));
        Assert(endpoint!.PipeName == newPipe);

        registry.RemoveIfCurrent(newPipe);
        Assert(!registry.TryRead(out _));
    }
    finally
    {
        directory.Delete(recursive: true);
    }
}

static void LastInstanceRegistryHandlesMissingAndMalformedMetadata()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-metadata-");
    try
    {
        var metadataPath = Path.Combine(directory.FullName, "last.json");
        var registry = new LastInstanceRegistry(metadataPath, $@"Local\yazi-test-{Guid.NewGuid():N}");
        Assert(!registry.TryRead(out _));

        File.WriteAllText(metadataPath, "not-json");
        Assert(!registry.TryRead(out _));

        File.WriteAllText(metadataPath, "{\"Protocol\":\"yazi-desktop-host/last-instance/1\",\"PipeName\":\"bad\"}");
        Assert(!registry.TryRead(out _));

        File.WriteAllText(metadataPath, new string('x', 4097));
        Assert(!registry.TryRead(out _));
    }
    finally
    {
        directory.Delete(recursive: true);
    }
}

static void LastInstanceControlServerDisablesAfterPublicationFailure()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-publish-");
    var mutexName = $@"Local\yazi-test-{Guid.NewGuid():N}";
    using var holderReady = new ManualResetEventSlim();
    using var holderRelease = new ManualResetEventSlim();
    var holder = Task.Run(() =>
    {
        using var heldMutex = new Mutex(false, mutexName);
        if (!heldMutex.WaitOne(TimeSpan.FromSeconds(2)))
        {
            return;
        }

        holderReady.Set();
        holderRelease.Wait();
        heldMutex.ReleaseMutex();
    });
    try
    {
        Assert(holderReady.Wait(TimeSpan.FromSeconds(2)));
        var registry = new LastInstanceRegistry(Path.Combine(directory.FullName, "last.json"), mutexName);
        Assert(!registry.Publish("yazi-terminal-control-publish-failure"));

        using var server = new LastInstanceControlServer(registry);
        Assert(!server.Start());
        Assert(!File.Exists(Path.Combine(directory.FullName, "last.json")));
    }
    finally
    {
        holderRelease.Set();
        holder.GetAwaiter().GetResult();
        directory.Delete(recursive: true);
    }
}

static void LastInstanceClientFallsBackForUnreachableEndpoint()
{
    var endpoint = new LastInstanceEndpoint($"yazi-terminal-control-{Guid.NewGuid():N}");

    Assert(!LastInstanceClient.TrySend(endpoint, @"C:\work", TimeSpan.FromMilliseconds(100)));
}

static void LastInstanceClientRejectsInvalidEndpointNames()
{
    var invalidNames = new string?[]
    {
        null,
        string.Empty,
        "yazi-terminal-control-",
        "yazi-terminal-control-not-a-guid",
        "yazi-terminal-control-gggggggggggggggggggggggggggggggg",
        "other-control-" + Guid.NewGuid().ToString("N"),
        "yazi-terminal-control-" + Guid.NewGuid().ToString("N") + "-extra",
    };

    foreach (var name in invalidNames)
    {
        Assert(!LastInstanceClient.TrySendAsync(
            new LastInstanceEndpoint(name!),
            @"C:\work",
            TimeSpan.FromSeconds(1)).GetAwaiter().GetResult());
    }
}

static void LastInstanceClientTimesOutWhileWaitingForAcknowledgement()
{
    var pipeName = $"yazi-terminal-control-{Guid.NewGuid():N}";
    using var server = new NamedPipeServerStream(
        pipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    var acceptTask = server.WaitForConnectionAsync();
    var resultTask = Task.Run(() => LastInstanceClient.TrySend(
        new LastInstanceEndpoint(pipeName),
        @"C:\work",
        TimeSpan.FromMilliseconds(200)));

    Assert(acceptTask.Wait(TimeSpan.FromSeconds(2)));
    Assert(!resultTask.GetAwaiter().GetResult());
}

static void LastInstanceClientRejectsInvalidAcknowledgementFrame()
{
    var acknowledgements = new[]
    {
        Encoding.UTF8.GetBytes("{malformed}\n"),
        Enumerable.Repeat((byte)'x', LastInstanceControlProtocol.MaxFrameBytes + 1)
            .Append((byte)'\n')
            .ToArray(),
        new byte[] { 0xFF, (byte)'\n' },
    };

    foreach (var acknowledgement in acknowledgements)
    {
        var pipeName = $"yazi-terminal-control-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var acceptTask = server.WaitForConnectionAsync();
        var resultTask = Task.Run(() => LastInstanceClient.TrySend(
            new LastInstanceEndpoint(pipeName),
            @"C:\work",
            TimeSpan.FromSeconds(2)));

        Assert(acceptTask.Wait(TimeSpan.FromSeconds(2)));
        try
        {
            server.Write(acknowledgement, 0, acknowledgement.Length);
            server.Flush();
        }
        catch (IOException)
        {
            // The client may close after rejecting the frame.
        }

        Assert(!resultTask.GetAwaiter().GetResult());
    }
}

static void LastInstanceControlPipeAcceptsDirectoryRequest()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-pipe-");
    try
    {
        var registry = new LastInstanceRegistry(
            Path.Combine(directory.FullName, "last.json"),
            $@"Local\yazi-test-{Guid.NewGuid():N}");
        using var server = new LastInstanceControlServer(registry);
        var received = new ManualResetEventSlim();
        string? receivedPath = null;
        server.DirectoryRequested += (path, _) =>
        {
            receivedPath = path;
            received.Set();
            return Task.FromResult(true);
        };
        server.Start();

        Assert(registry.TryRead(out var endpoint));
        Assert(LastInstanceClient.TrySend(
            endpoint!,
            @"C:\work\space\日本語",
            TimeSpan.FromSeconds(2)));
        Assert(received.Wait(TimeSpan.FromSeconds(2)));
        Assert(receivedPath == @"C:\work\space\日本語");
    }
    finally
    {
        directory.Delete(recursive: true);
    }
}

static void LastInstanceControlPipeReturnsNegativeAcknowledgement()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-negative-");
    try
    {
        var registry = new LastInstanceRegistry(
            Path.Combine(directory.FullName, "last.json"),
            $@"Local\yazi-test-{Guid.NewGuid():N}");
        using var server = new LastInstanceControlServer(registry);
        server.DirectoryRequested += (_, _) => Task.FromResult(false);
        server.Start();

        Assert(registry.TryRead(out var endpoint));
        Assert(!LastInstanceClient.TrySend(endpoint!, @"C:\work", TimeSpan.FromSeconds(2)));
    }
    finally
    {
        directory.Delete(recursive: true);
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

static void YaziDirectoryCommandPreservesArgumentBoundaries()
{
    var startInfo = YaziDirectoryController.CreateStartInfo(
        @"C:\tools\ya.exe",
        "12345",
        @"C:\資料\space folder");

    Assert(!startInfo.UseShellExecute);
    Assert(startInfo.ArgumentList.SequenceEqual([
        "emit-to",
        "12345",
        "cd",
        @"C:\資料\space folder"]));
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

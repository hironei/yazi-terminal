using System.IO.Pipes;
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
    ("bridge session publishes state and disconnect", BridgeSessionPublishesStateAndDisconnect),
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

static void BridgeSessionPublishesStateAndDisconnect()
{
    var instanceId = Guid.NewGuid();
    using var server = new YaziBridgePipeServer(instanceId);
    var session = new YaziBridgeSession(instanceId, server);
    var states = new List<YaziBridgeState?>();
    var disconnectReason = string.Empty;
    session.StateChanged += states.Add;
    session.Disconnected += reason => disconnectReason = reason;
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

    runTask.GetAwaiter().GetResult();
    session.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Assert(states.Count >= 2);
    Assert(states[0] is not null && states[0]!.Sequence == 1);
    Assert(states[^1] is null);
    Assert(disconnectReason == "disconnect");
}

static void SendFrame(NamedPipeClientStream client, byte[] frame)
{
    client.Write(frame, 0, frame.Length);
    client.WriteByte((byte)'\n');
    client.Flush();
}

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

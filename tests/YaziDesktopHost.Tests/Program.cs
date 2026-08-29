using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;

using YaziDesktopHost;

var tests = new (string Name, Action Test)[]
{
    ("explicit path takes precedence", ExplicitPathTakesPrecedence),
    ("PATH lookup finds yazi.exe", PathLookupFindsExecutable),
    ("paired ya lookup prefers the yazi directory", PairedYaLookupPrefersYaziDirectory),
    ("missing executable is classified", MissingExecutableIsClassified),
    ("command line options preserve new-window default", CommandLineOptionsPreserveNewWindowDefault),
    ("command line options route an existing file through its parent directory", CommandLineOptionsRouteExistingFile),
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
    ("last-instance control pipe accepts a file request", LastInstanceControlPipeAcceptsFileRequest),
    ("last-instance control pipe returns a negative ACK", LastInstanceControlPipeReturnsNegativeAcknowledgement),
    ("path requests serialize startup file open and last-instance ACK", PathRequestsSerializeStartupFileOpenAndLastInstanceAcknowledgement),
    ("bridge parser accepts a CJK snapshot", BridgeParserAcceptsCjkSnapshot),
    ("bridge parser accepts a command catalog", BridgeParserAcceptsCommandCatalog),
    ("bridge parser preserves command run sequence", BridgeParserPreservesCommandRunSequence),
    ("bridge parser rejects an invalid command catalog", BridgeParserRejectsInvalidCommandCatalog),
    ("bridge parser rejects a wrong instance", BridgeParserRejectsWrongInstance),
    ("bridge reducer applies an ordered update", BridgeReducerAppliesOrderedUpdate),
    ("bridge reducer invalidates a sequence gap", BridgeReducerInvalidatesSequenceGap),
    ("bridge reducer rejects duplicate snapshots", BridgeReducerRejectsDuplicateSnapshots),
    ("bridge reducer rejects a decreasing snapshot", BridgeReducerRejectsDecreasingSnapshot),
    ("bridge reducer rejects a snapshot before hello", BridgeReducerRejectsSnapshotBeforeHello),
    ("bridge reducer requires a fresh snapshot after disconnect", BridgeReducerRequiresFreshSnapshot),
    ("bridge pipe round-trips a framed message", BridgePipeRoundTripsFrame),
    ("bridge session reconnects after disconnect", BridgeSessionReconnectsAfterDisconnect),
    ("bridge session publishes command catalog", BridgeSessionPublishesCommandCatalog),
    ("Phase 2 AC 138 parser accepts valid UTF-8 snapshot", Phase2Ac138ParserAcceptsValidUtf8Snapshot),
    ("Phase 2 AC 139 parser and frame reader reject invalid frames", Phase2Ac139ParserAndFrameReaderRejectInvalidFrames),
    ("Phase 2 AC 140 reducer rejects invalid path kinds and required fields", Phase2Ac140ReducerRejectsInvalidPathKindsAndRequiredFields),
    ("Phase 2 AC 141 reducer accepts first snapshot and ordered empty selection", Phase2Ac141ReducerAcceptsFirstSnapshotAndOrderedEmptySelection),
    ("Phase 2 AC 142 reducer rejects duplicate out-of-order and gap updates", Phase2Ac142ReducerRejectsDuplicateOutOfOrderAndGapUpdates),
    ("Phase 2 AC 143 session rejects goodbye and error then requires a snapshot", Phase2Ac143SessionRejectsGoodbyeAndErrorThenRequiresSnapshot),
    ("Phase 2 AC 144 fixtures preserve CJK surrogate long and root paths", Phase2Ac144FixturesPreserveCjkSurrogateLongAndRootPaths),
    ("Phase 2 AC 145 fixtures preserve URLs without terminal output", Phase2Ac145FixturesPreserveUrlsWithoutTerminalOutput),
    ("Yazi command line uses bridge identity", YaziCommandLineUsesBridgeIdentity),
    ("Yazi directory command preserves argument boundaries", YaziDirectoryCommandPreservesArgumentBoundaries),
    ("Yazi action command preserves argument boundaries", YaziActionCommandPreservesArgumentBoundaries),
    ("Yazi action sequence preserves binding order", YaziActionSequencePreservesBindingOrder),
    ("Yazi settings reveal command preserves the Windows path", YaziSettingsRevealCommandPreservesWindowsPath),
    ("Yazi file opener preserves a space-containing path as one argument", YaziFileOpenerPreservesSpacePathAsSingleArgument),
    ("Yazi action tokenizer handles quoted arguments", YaziActionTokenizerHandlesQuotedArguments),
    ("Yazi action tokenizer rejects unterminated quotes", YaziActionTokenizerRejectsUnterminatedQuotes),
    ("bridge environment scope restores values", BridgeEnvironmentScopeRestoresValues),
    ("host settings accept custom font families and sizes", HostSettingsAcceptCustomFontFamiliesAndSizes),
    ("host settings round trip and reject blank or invalid values", HostSettingsRoundTripAndRejectsBlankOrInvalidValues),
    ("window placement settings round trip and ignore malformed placement", WindowPlacementSettingsRoundTripAndIgnoreMalformedPlacement),
    ("window placement catalog keeps per-monitor placements", WindowPlacementCatalogKeepsPerMonitorPlacements),
    ("window placement catalog selects connected fallback and clamps bounds", WindowPlacementCatalogSelectsConnectedFallbackAndClampsBounds),
    ("Yazi exit policy distinguishes known normal and abnormal exits", YaziExitPolicyDistinguishesKnownNormalAndAbnormalExits),
    ("Yazi exit policy treats completed process monitors as normal", YaziExitPolicyTreatsCompletedProcessMonitorAsNormal),
    ("Yazi exit policy preserves unknown process-monitor exits", YaziExitPolicyPreservesUnknownProcessMonitorExit),
    ("Yazi exit policy preserves unknown terminal-marker exits", YaziExitPolicyPreservesUnknownTerminalMarkerExit),
    ("Yazi exit code reader waits for the OS process", YaziExitCodeReaderWaitsForTheOsProcess),
    ("shell target prefers selected paths", ShellTargetPrefersSelectedPaths),
    ("shell target preserves multiple selection", ShellTargetPreservesMultipleSelection),
    ("shell target falls back to hovered path", ShellTargetFallsBackToHoveredPath),
    ("shell target normalizes Windows path separators", ShellTargetNormalizesWindowsPathSeparators),
    ("shell target normalizes file URI", ShellTargetNormalizesFileUri),
    ("shell target resolves current directory", ShellTargetResolvesCurrentDirectory),
    ("shell target rejects unavailable, URLs, and empty state", ShellTargetRejectsUnavailableUrlsAndEmptyState),
    ("shell context COM interfaces preserve native vtable order", ShellContextComInterfacesPreserveNativeVtableOrder),
    ("shell context IContextMenu3 forwards LRESULT", ShellContextMenu3ForwardsLresult),
    ("shell context IContextMenu3 failure remains unhandled", ShellContextMenu3FailureRemainsUnhandled),
    ("shell context menu does not enumerate menu text for logging", ShellContextMenuDoesNotEnumerateMenuTextForLogging),
    ("command palette filters theme commands", CommandPaletteFiltersThemeCommands),
    ("command palette includes Yazi commands", CommandPaletteIncludesYaziCommands),
    ("palette navigation handles empty lists and selection boundaries", PaletteNavigationHandlesEmptyListsAndSelectionBoundaries),
    ("palette navigation wraps at first and last rows", PaletteNavigationWrapsAtFirstAndLastRows),
    ("palette navigation uses j and k only for empty queries", PaletteNavigationUsesJAndKOnlyForEmptyQueries),
    ("palette navigation leaves filter text input unhandled", PaletteNavigationLeavesFilterTextInputUnhandled),
    ("Yazi theme loader reads the selected flavor", YaziThemeLoaderReadsSelectedFlavor),
    ("theme palettes keep dark defaults and distinct light colors", ThemePalettesKeepDistinctModes),
    ("theme palette settings override Yazi colors", ThemePaletteSettingsOverrideYaziColors),
    ("terminal paste recognizes Ctrl+Shift+V", TerminalPasteRecognizesControlShiftV),
    ("terminal paste recognizes Shift+Insert", TerminalPasteRecognizesShiftInsert),
    ("terminal paste rejects other gestures and repeats", TerminalPasteRejectsOtherGesturesAndRepeats),
    ("terminal paste ignores negative mouse wheel messages", TerminalPasteIgnoresNegativeMouseWheelMessages),
    ("terminal paste ignores empty text", TerminalPasteIgnoresEmptyText),
    ("terminal paste frames Unicode and multiline text", TerminalPasteFramesUnicodeAndMultilineText),
    ("Kitty protocol filter drops a flags push", KittyProtocolFilterDropsFlagsPush),
    ("Kitty protocol filter leaves queries and pops untouched", KittyProtocolFilterLeavesQueriesAndPopsUntouched),
    ("Kitty protocol filter leaves unrelated escape sequences untouched", KittyProtocolFilterLeavesUnrelatedEscapeSequencesUntouched),
    ("Kitty protocol filter drops a push split across chunks", KittyProtocolFilterDropsPushSplitAcrossChunks),
    ("Kitty protocol filter drops a push split at every offset", KittyProtocolFilterDropsPushSplitAtEveryOffset),
    ("Kitty protocol filter drops a zero-flag push", KittyProtocolFilterDropsZeroFlagPush),
    ("Kitty protocol filter passes through plain text unchanged", KittyProtocolFilterPassesThroughPlainText),
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

static void CommandLineOptionsRouteExistingFile()
{
    var directory = Directory.CreateTempSubdirectory("yazi-command-line-file-");
    var filePath = Path.Combine(directory.FullName, "notes 日本語.txt");
    File.WriteAllText(filePath, "content");
    try
    {
        var options = CommandLineOptions.Parse([filePath], Environment.CurrentDirectory);

        Assert(!options.UseLastInstance);
        Assert(options.InitialDirectory.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase));
        Assert(options.FilePath is not null
            && options.FilePath.Equals(Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        directory.Delete(recursive: true);
    }
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
    Assert(parsed.Command == LastInstanceControlCommand.ChangeDirectory);
    var fileRequest = new LastInstanceControlRequest(
        @"C:\work\日本語 folder\notes.txt",
        LastInstanceControlCommand.OpenFile);
    var fileFrame = LastInstanceControlProtocol.Serialize(fileRequest);
    Assert(LastInstanceControlProtocol.TryParse(fileFrame, out var parsedFile));
    Assert(parsedFile!.Command == LastInstanceControlCommand.OpenFile);
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
        server.RequestReceived += (request, _) =>
        {
            receivedPath = request.Path;
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

static void LastInstanceControlPipeAcceptsFileRequest()
{
    var directory = Directory.CreateTempSubdirectory("yazi-last-instance-file-pipe-");
    try
    {
        var registry = new LastInstanceRegistry(
            Path.Combine(directory.FullName, "last.json"),
            $@"Local\yazi-test-{Guid.NewGuid():N}");
        using var server = new LastInstanceControlServer(registry);
        LastInstanceControlRequest? received = null;
        var receivedEvent = new ManualResetEventSlim();
        server.RequestReceived += (request, _) =>
        {
            received = request;
            receivedEvent.Set();
            return Task.FromResult(true);
        };
        server.Start();

        Assert(registry.TryRead(out var endpoint));
        Assert(LastInstanceClient.TrySend(
            endpoint!,
            new LastInstanceControlRequest(
                @"C:\work\space\日本語\notes.txt",
                LastInstanceControlCommand.OpenFile),
            TimeSpan.FromSeconds(2)));
        Assert(receivedEvent.Wait(TimeSpan.FromSeconds(2)));
        Assert(received is not null
            && received.Command == LastInstanceControlCommand.OpenFile
            && received.Path == @"C:\work\space\日本語\notes.txt");
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
        server.RequestReceived += (_, _) => Task.FromResult(false);
        server.Start();

        Assert(registry.TryRead(out var endpoint));
        Assert(!LastInstanceClient.TrySend(endpoint!, @"C:\work", TimeSpan.FromSeconds(2)));
    }
    finally
    {
        directory.Delete(recursive: true);
    }
}

static void PathRequestsSerializeStartupFileOpenAndLastInstanceAcknowledgement()
{
    var directory = Directory.CreateTempSubdirectory("yazi-path-transaction-");
    try
    {
        var registry = new LastInstanceRegistry(
            Path.Combine(directory.FullName, "last.json"),
            $@"Local\yazi-test-{Guid.NewGuid():N}");
        using var server = new LastInstanceControlServer(registry);
        var controller = new DelayedPathTransactionController();
        var sequencer = new YaziPathRequestSequencer(controller);
        server.RequestReceived += (request, cancellationToken) => sequencer.ExecuteAsync(
            request.Command switch
            {
                LastInstanceControlCommand.ChangeDirectory =>
                    new YaziPathRequest(YaziPathRequestKind.ChangeDirectory, request.Path),
                LastInstanceControlCommand.OpenFile =>
                    new YaziPathRequest(YaziPathRequestKind.OpenFile, request.Path),
                _ => throw new ArgumentOutOfRangeException(nameof(request)),
            },
            cancellationToken);
        Assert(server.Start());
        Assert(registry.TryRead(out var endpoint));

        var startupOpen = sequencer.ExecuteAsync(new YaziPathRequest(
            YaziPathRequestKind.OpenFile,
            @"C:\work\A.txt"));
        Assert(controller.OpenRevealStarted.Wait(TimeSpan.FromSeconds(2)));

        var lastInstanceChange = LastInstanceClient.TrySendAsync(
            endpoint!,
            new LastInstanceControlRequest(
                @"C:\work\B",
                LastInstanceControlCommand.ChangeDirectory),
            TimeSpan.FromSeconds(2));
        Assert(!lastInstanceChange.IsCompleted);
        Assert(controller.Operations.SequenceEqual(["reveal A"]));

        controller.AllowOpen();
        Assert(startupOpen.GetAwaiter().GetResult());
        Assert(controller.ChangeDirectoryStarted.Wait(TimeSpan.FromSeconds(2)));
        Assert(!lastInstanceChange.IsCompleted);
        Assert(controller.Operations.SequenceEqual(["reveal A", "open A", "cd B"]));

        controller.AllowChangeDirectory();
        Assert(lastInstanceChange.GetAwaiter().GetResult());
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

static void BridgeParserAcceptsCommandCatalog()
{
    using var document = JsonDocument.Parse("""
        {
          "commands": [
            { "key": "g d", "run": "cd C:\\work", "description": "Go work" },
            { "key": "q", "run": "quit", "description": "Quit" }
          ]
        }
        """);

    var commands = new YaziBridgeCommandCatalogParser().Parse(document.RootElement);
    Assert(commands.Count == 2);
    Assert(commands[0] == new YaziBridgeCommand("g d", "cd C:\\work", "Go work"));
    Assert(commands[1].Run == "quit");
}

static void BridgeParserPreservesCommandRunSequence()
{
    using var document = JsonDocument.Parse("""
        {
          "commands": [
            {
              "key": "g d",
              "run": "cd C:\\work",
              "runs": ["cd C:\\work", "plugin refresh"],
              "description": "Go work and refresh"
            }
          ]
        }
        """);

    var command = new YaziBridgeCommandCatalogParser().Parse(document.RootElement).Single();

    Assert(command.ActionSequence.SequenceEqual(["cd C:\\work", "plugin refresh"]));
    Assert(command.DisplayRun == "cd C:\\work → plugin refresh");
}

static void BridgeParserRejectsInvalidCommandCatalog()
{
    using var document = JsonDocument.Parse("""
        { "commands": [{ "key": "q", "run": "   ", "description": "Quit" }] }
        """);

    Expect<YaziBridgeProtocolException>(() =>
        new YaziBridgeCommandCatalogParser().Parse(document.RootElement));
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
    Assert(reducer.UnavailableReason == "sequence-gap");
}

static void BridgeReducerRejectsDuplicateSnapshots()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 2), instanceId));

    Assert(reducer.State is null);
    Assert(reducer.Availability == YaziBridgeAvailability.Unavailable);
    Assert(reducer.UnavailableReason == "duplicate-snapshot");
}

static void BridgeReducerRejectsDecreasingSnapshot()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 0), instanceId));

    Assert(reducer.State is null);
    Assert(reducer.Availability == YaziBridgeAvailability.Unavailable);
    Assert(reducer.UnavailableReason == "sequence-gap");
}

static void BridgeReducerRejectsSnapshotBeforeHello()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 0), instanceId));

    Assert(reducer.State is null);
    Assert(reducer.Availability == YaziBridgeAvailability.Unavailable);
    Assert(reducer.UnavailableReason == "handshake-required");
}

static void BridgeReducerRequiresFreshSnapshot()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    reducer.MarkDisconnected();

    Assert(reducer.State is null);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
    Assert(reducer.State is not null);
    Assert(reducer.State!.Sequence == 1);
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

static void BridgeSessionPublishesCommandCatalog()
{
    var instanceId = Guid.NewGuid();
    using var server = new YaziBridgePipeServer(instanceId);
    var session = new YaziBridgeSession(instanceId, server);
    var catalogs = new List<IReadOnlyList<YaziBridgeCommand>>();
    session.CommandsChanged += commands =>
    {
        lock (catalogs)
        {
            catalogs.Add(commands);
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
        SendFrame(client, Frame(
            instanceId,
            0,
            "hello",
            new
            {
                commands = new[]
                {
                    new { key = "q", run = "quit", description = "Quit" },
                },
            }));
        WaitUntil(() =>
        {
            lock (catalogs)
            {
                return catalogs.Any(catalog => catalog.Count == 1);
            }
        });
    }

    session.DisposeAsync().AsTask().GetAwaiter().GetResult();
    runTask.GetAwaiter().GetResult();

    lock (catalogs)
    {
        Assert(catalogs.Any(catalog => catalog.Count == 1
            && catalog[0] == new YaziBridgeCommand("q", "quit", "Quit")));
        Assert(catalogs[^1].Count == 0);
    }
}

static void Phase2Ac138ParserAcceptsValidUtf8Snapshot()
{
    var instanceId = Guid.NewGuid();
    var frame = Frame(
        instanceId,
        1,
        "snapshot",
        new
        {
            tab = 0,
            cwd = new { kind = "filesystem", value = @"C:\資料 folder" },
            hovered = new { kind = "filesystem", value = @"C:\資料 folder\emoji-😀.txt" },
            selected = Array.Empty<object>(),
        });

    var message = new YaziBridgeMessageParser().Parse(frame, instanceId);
    Assert(message.Kind == YaziBridgeMessageKind.Snapshot);
    Assert(message.Sequence == 1);
}

static void Phase2Ac139ParserAndFrameReaderRejectInvalidFrames()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();

    Expect<YaziBridgeProtocolException>(() =>
        parser.Parse(Encoding.UTF8.GetBytes("{\"protocol\":"), instanceId));
    Expect<YaziBridgeProtocolException>(() =>
        parser.Parse(new byte[YaziBridgeMessageParser.MaxFrameBytes + 1], instanceId));
    Expect<YaziBridgeProtocolException>(() => parser.Parse(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            protocol = "unsupported/1",
            instanceId,
            sequence = 0,
            kind = "hello",
            payload = new { },
        })),
        instanceId));
    Expect<YaziBridgeProtocolException>(() => parser.Parse(
        Frame(Guid.NewGuid(), 0, "hello", new { }),
        instanceId));
    Expect<YaziBridgeProtocolException>(() => parser.Parse(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            protocol = YaziBridgeMessageParser.SupportedProtocol,
            instanceId,
            sequence = 0,
            kind = "hello",
        })),
        instanceId));

    var oversizedFrame = Enumerable.Repeat((byte)'x', YaziBridgeMessageParser.MaxFrameBytes + 1)
        .Append((byte)'\n')
        .ToArray();
    using var connection = new YaziBridgePipeConnection(new MemoryStream(oversizedFrame));
    Expect<YaziBridgeProtocolException>(() =>
        connection.ReadFrameAsync().GetAwaiter().GetResult());
}

static void Phase2Ac140ReducerRejectsInvalidPathKindsAndRequiredFields()
{
    var instanceId = Guid.NewGuid();
    var parser = new YaziBridgeMessageParser();
    var invalidKindReducer = new YaziBridgeStateReducer(instanceId);
    invalidKindReducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    Expect<YaziBridgeProtocolException>(() => invalidKindReducer.Apply(parser.Parse(
        Frame(
            instanceId,
            1,
            "snapshot",
            new
            {
                tab = 0,
                cwd = new { kind = "virtual", value = "archive://remote" },
                hovered = (object?)null,
                selected = Array.Empty<object>(),
            }),
        instanceId)));
    Assert(invalidKindReducer.Availability == YaziBridgeAvailability.Unavailable);
    Assert(invalidKindReducer.UnavailableReason == "invalid-snapshot");

    var missingFieldReducer = new YaziBridgeStateReducer(instanceId);
    missingFieldReducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    Expect<YaziBridgeProtocolException>(() => missingFieldReducer.Apply(parser.Parse(
        Frame(instanceId, 1, "snapshot", new { tab = 0, hovered = (object?)null, selected = Array.Empty<object>() }),
        instanceId)));
    Assert(missingFieldReducer.Availability == YaziBridgeAvailability.Unavailable);
    Assert(missingFieldReducer.UnavailableReason == "invalid-snapshot");
}

static void Phase2Ac141ReducerAcceptsFirstSnapshotAndOrderedEmptySelection()
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
            new { present = new[] { "hovered", "selected" }, hovered = (object?)null, selected = Array.Empty<object>() }),
        instanceId));

    Assert(reducer.Availability == YaziBridgeAvailability.Available);
    Assert(reducer.State is not null);
    Assert(reducer.State!.Sequence == 2);
    Assert(reducer.State.Hovered is null);
    Assert(reducer.State.Selected.Count == 0);
}

static void Phase2Ac142ReducerRejectsDuplicateOutOfOrderAndGapUpdates()
{
    foreach (var invalidSequence in new ulong[] { 1, 0, 3 })
    {
        var instanceId = Guid.NewGuid();
        var parser = new YaziBridgeMessageParser();
        var reducer = new YaziBridgeStateReducer(instanceId);
        reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
        reducer.Apply(parser.Parse(SnapshotFrame(instanceId, 1), instanceId));
        reducer.Apply(parser.Parse(StateFrame(instanceId, invalidSequence), instanceId));

        Assert(reducer.Availability == YaziBridgeAvailability.Unavailable);
        Assert(reducer.State is null);
        Assert(reducer.UnavailableReason == "sequence-gap");
    }
}

static void Phase2Ac143SessionRejectsGoodbyeAndErrorThenRequiresSnapshot()
{
    var instanceId = Guid.NewGuid();
    using var server = new YaziBridgePipeServer(instanceId);
    var session = new YaziBridgeSession(instanceId, server);
    var states = new List<YaziBridgeState?>();
    var reasons = new List<string>();
    session.StateChanged += state =>
    {
        lock (states)
        {
            states.Add(state);
        }
    };
    session.Disconnected += reason =>
    {
        lock (reasons)
        {
            reasons.Add(reason);
        }
    };
    var runTask = session.RunAsync();

    using (var client = ConnectBridgeClient(server.PipeName))
    {
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, SnapshotFrame(instanceId, 1));
        SendFrame(client, Frame(instanceId, 2, "goodbye", new { }));
    }
    WaitUntil(() => HasReason(reasons, "goodbye"));

    using (var client = ConnectBridgeClient(server.PipeName))
    {
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, SnapshotFrame(instanceId, 1));
        SendFrame(client, Frame(instanceId, 2, "error", new { }));
    }
    WaitUntil(() => HasReason(reasons, "protocol-error"));

    var nullStateCount = CountNullStates(states);
    using (var client = ConnectBridgeClient(server.PipeName))
    {
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, StateFrame(instanceId, 1));
        WaitUntil(() => CountNullStates(states) > nullStateCount);
    }

    WaitUntil(() => HasReason(reasons, "disconnect"));
    var availableStateCount = CountAvailableStates(states);
    using (var client = ConnectBridgeClient(server.PipeName))
    {
        SendFrame(client, HelloFrame(instanceId));
        SendFrame(client, SnapshotFrame(instanceId, 1));
        WaitUntil(() => CountAvailableStates(states) > availableStateCount);
    }

    session.DisposeAsync().AsTask().GetAwaiter().GetResult();
    runTask.GetAwaiter().GetResult();
    Assert(HasStateWithSequence(states, 1));
    Assert(HasNullState(states));
}

static void Phase2Ac144FixturesPreserveCjkSurrogateLongAndRootPaths()
{
    var instanceId = Guid.NewGuid();
    var longPath = @"C:\long\" + new string('x', 32_000) + ".txt";
    var rootPath = "C:\\";
    var surrogatePath = @"C:\資料 folder\emoji-😀.txt";
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(
        Frame(
            instanceId,
            1,
            "snapshot",
            new
            {
                tab = 0,
                cwd = new { kind = "filesystem", value = rootPath },
                hovered = new { kind = "filesystem", value = surrogatePath },
                selected = new[] { new { kind = "filesystem", value = longPath } },
            }),
        instanceId));

    Assert(reducer.State is not null);
    Assert(reducer.State!.Cwd.Value == rootPath);
    Assert(reducer.State.Hovered?.Value == surrogatePath);
    Assert(reducer.State.Selected.Single().Value == longPath);
}

static void Phase2Ac145FixturesPreserveUrlsWithoutTerminalOutput()
{
    var instanceId = Guid.NewGuid();
    var url = "archive://remote/資料 folder/emoji-😀.zip";
    var parser = new YaziBridgeMessageParser();
    var reducer = new YaziBridgeStateReducer(instanceId);
    reducer.Apply(parser.Parse(HelloFrame(instanceId), instanceId));
    reducer.Apply(parser.Parse(
        Frame(
            instanceId,
            1,
            "snapshot",
            new
            {
                tab = 0,
                cwd = new { kind = "filesystem", value = @"C:\work" },
                hovered = new { kind = "url", value = url },
                selected = new[] { new { kind = "url", value = url } },
            }),
        instanceId));

    Assert(reducer.State is not null);
    Assert(reducer.State!.Hovered == new YaziBridgePath(YaziBridgePathKind.Url, url));
    Assert(reducer.State.Selected.Single() == new YaziBridgePath(YaziBridgePathKind.Url, url));
}

static void SendFrame(NamedPipeClientStream client, byte[] frame)
{
    client.Write(frame, 0, frame.Length);
    client.WriteByte((byte)'\n');
    client.Flush();
}

static NamedPipeClientStream ConnectBridgeClient(string pipeName)
{
    var client = new NamedPipeClientStream(
        ".",
        pipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);
    client.Connect(5000);
    return client;
}

static bool HasReason(IReadOnlyList<string> reasons, string expected)
{
    lock (reasons)
    {
        return reasons.Contains(expected, StringComparer.Ordinal);
    }
}

static bool HasStateWithSequence(IReadOnlyList<YaziBridgeState?> states, ulong sequence)
{
    lock (states)
    {
        return states.Any(state => state?.Sequence == sequence);
    }
}

static bool HasNullState(IReadOnlyList<YaziBridgeState?> states)
{
    return CountNullStates(states) > 0;
}

static int CountNullStates(IReadOnlyList<YaziBridgeState?> states)
{
    lock (states)
    {
        return states.Count(state => state is null);
    }
}

static int CountAvailableStates(IReadOnlyList<YaziBridgeState?> states)
{
    lock (states)
    {
        return states.Count(state => state is not null);
    }
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

static void YaziActionCommandPreservesArgumentBoundaries()
{
    var startInfo = YaziCommandController.CreateStartInfo(
        @"C:\tools\ya.exe",
        "12345",
        "shell -- \"powershell.exe\" --block");

    Assert(startInfo.ArgumentList.SequenceEqual([
        "emit-to",
        "12345",
        "shell",
        "--",
        "powershell.exe",
        "--block"]));
}

static void YaziActionSequencePreservesBindingOrder()
{
    var startInfos = YaziCommandController.CreateStartInfos(
        @"C:\tools\ya.exe",
        "12345",
        ["cd \"C:\\work folder\"", "plugin refresh"]);

    Assert(startInfos.Count == 2);
    Assert(startInfos[0].ArgumentList.SequenceEqual(["emit-to", "12345", "cd", @"C:\work folder"]));
    Assert(startInfos[1].ArgumentList.SequenceEqual(["emit-to", "12345", "plugin", "refresh"]));
}

static void YaziSettingsRevealCommandPreservesWindowsPath()
{
    var path = @"C:\Users\hiron\AppData\Local\YaziTerminal\settings.json";
    var startInfo = YaziCommandController.CreateStartInfo(
        @"C:\tools\ya.exe",
        "12345",
        $"reveal \"{path}\"");

    Assert(startInfo.ArgumentList.SequenceEqual([
        "emit-to",
        "12345",
        "reveal",
        path]));
}

static void YaziFileOpenerPreservesSpacePathAsSingleArgument()
{
    var path = @"C:\Program Files\Yazi Terminal\notes.txt";
    var revealStartInfo = YaziCommandController.CreateStartInfo(
        @"C:\tools\ya.exe",
        "12345",
        "reveal",
        [path]);

    Assert(revealStartInfo.ArgumentList.SequenceEqual([
        "emit-to",
        "12345",
        "reveal",
        path]));

    var openStartInfo = YaziCommandController.CreateStartInfo(
        @"C:\tools\ya.exe",
        "12345",
        "open");
    Assert(openStartInfo.ArgumentList.SequenceEqual([
        "emit-to",
        "12345",
        "open"]));
}

static void YaziActionTokenizerHandlesQuotedArguments()
{
    Assert(YaziCommandController.TryTokenize(
        "plugin command-palette \"argument with spaces\" 'single quoted'",
        out var tokens));
    Assert(tokens.SequenceEqual([
        "plugin",
        "command-palette",
        "argument with spaces",
        "single quoted"]));
}

static void YaziActionTokenizerRejectsUnterminatedQuotes()
{
    Assert(!YaziCommandController.TryTokenize("cd \"C:\\work", out _));
}

static void BridgeEnvironmentScopeRestoresValues()
{
    var names = new[]
    {
        "YAZI_DESKTOP_HOST_PIPE",
        "YAZI_DESKTOP_HOST_INSTANCE_ID",
        "YAZI_DESKTOP_HOST_PROTOCOL",
        "YAZI_CONFIG_HOME",
        "COLORTERM",
        "TERM",
        "NO_COLOR",
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
            Assert(Environment.GetEnvironmentVariable("YAZI_CONFIG_HOME") == YaziThemeLoader.ResolveConfigHome());
            Assert(Environment.GetEnvironmentVariable("COLORTERM") == "truecolor");
            Assert(Environment.GetEnvironmentVariable("TERM") == "xterm-256color");
            Assert(Environment.GetEnvironmentVariable("NO_COLOR") is null);
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

static void YaziExitPolicyDistinguishesKnownNormalAndAbnormalExits()
{
    var normal = YaziProcessExitPolicy.FromProcessMonitor(0);
    var positiveFailure = YaziProcessExitPolicy.FromProcessMonitor(1);
    var negativeFailure = YaziProcessExitPolicy.FromProcessMonitor(-1);

    Assert(normal == new YaziProcessExit.Known(0));
    Assert(YaziProcessExitPolicy.Classify(normal) == YaziProcessExitClassification.Normal);
    Assert(YaziProcessExitPolicy.Classify(positiveFailure) == YaziProcessExitClassification.Abnormal);
    Assert(YaziProcessExitPolicy.Classify(negativeFailure) == YaziProcessExitClassification.Abnormal);
}

static void YaziExitPolicyPreservesUnknownProcessMonitorExit()
{
    var exit = YaziProcessExitPolicy.FromProcessMonitor(null);

    Assert(exit is YaziProcessExit.Unknown);
    Assert(YaziProcessExitPolicy.Classify(exit) == YaziProcessExitClassification.Unknown);
    Assert(!YaziProcessExitPolicy.IsNormalExit(exit));
}

static void YaziExitPolicyTreatsCompletedProcessMonitorAsNormal()
{
    var exit = YaziProcessExitPolicy.FromProcessMonitorCompleted();

    Assert(exit is YaziProcessExit.ProcessMonitorCompleted);
    Assert(YaziProcessExitPolicy.Classify(exit) == YaziProcessExitClassification.Normal);
    Assert(YaziProcessExitPolicy.IsNormalExit(exit));
}

static void YaziExitPolicyPreservesUnknownTerminalMarkerExit()
{
    var exit = YaziProcessExitPolicy.FromTerminalMarker(null);

    Assert(exit is YaziProcessExit.Unknown);
    Assert(YaziProcessExitPolicy.Classify(exit) == YaziProcessExitClassification.Unknown);
    Assert(!YaziProcessExitPolicy.IsNormalExit(exit));
}

static void YaziExitCodeReaderWaitsForTheOsProcess()
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "cmd.exe",
        UseShellExecute = false,
        CreateNoWindow = true,
        ArgumentList = { "/c", "ping 127.0.0.1 -n 2 >nul" },
    }) ?? throw new InvalidOperationException("Could not start the exit-code test process.");

    var exitCode = MainWindow.ReadExitCode(new object(), process);

    Assert(exitCode == 0);
    Assert(process.HasExited);
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

static void ShellContextMenu3ForwardsLresult()
{
    var expected = new IntPtr(0x1234);
    var handler = new FakeShellContextMenuMessageHandler
    {
        HandlesMenuMsg2 = true,
        MenuMsg2HResult = 0,
        MenuMsg2Result = expected,
    };
    var handled = false;

    var actual = ShellContextMenuMessageForwarder.Forward(
        handler,
        0x0120,
        new IntPtr(0x56),
        new IntPtr(0x78),
        ref handled);

    Assert(handled);
    Assert(actual == expected);
    Assert(handler.MenuMsg2CallCount == 1);
    Assert(handler.LastMessage == 0x0120);
    Assert(handler.LastWParam == new IntPtr(0x56));
    Assert(handler.LastLParam == new IntPtr(0x78));
    Assert(handler.MenuMsgCallCount == 0);
}

static void ShellContextMenu3FailureRemainsUnhandled()
{
    var handler = new FakeShellContextMenuMessageHandler
    {
        HandlesMenuMsg2 = true,
        MenuMsg2HResult = unchecked((int)0x80004005),
        MenuMsg2Result = new IntPtr(0x1234),
    };
    var handled = true;

    var actual = ShellContextMenuMessageForwarder.Forward(
        handler,
        0x002B,
        IntPtr.Zero,
        IntPtr.Zero,
        ref handled);

    Assert(!handled);
    Assert(actual == IntPtr.Zero);
    Assert(handler.MenuMsg2CallCount == 1);
    Assert(handler.MenuMsgCallCount == 0);
}

static void ShellContextMenuDoesNotEnumerateMenuTextForLogging()
{
    var serviceType = typeof(WindowsShellContextMenuService);
    var privateMethods = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
    var privateFields = serviceType.GetFields(BindingFlags.NonPublic | BindingFlags.Static);

    Assert(!privateMethods.Any(method => method.Name == "LogMenuItems"));
    Assert(!privateMethods.Any(method => method.Name == "GetMenuItemInfo"));
    Assert(!privateFields.Any(field => field.Name == "MiimString"));
}

static void ThemePalettesKeepDistinctModes()
{
    var dark = ThemePalette.For(AppThemeMode.Dark);
    var light = ThemePalette.For(AppThemeMode.Light);

    Assert(dark.TerminalBackground == new RgbColor(0, 0, 0));
    Assert(dark.TerminalForeground == new RgbColor(255, 255, 255));
    Assert(light.HostBackground == new RgbColor(238, 232, 213));
    Assert(light.TerminalBackground == new RgbColor(253, 246, 227));
    Assert(light.HostForeground == new RgbColor(7, 54, 66));
    Assert(light.PaletteForeground == new RgbColor(7, 54, 66));
    Assert(light.TerminalForeground == new RgbColor(7, 54, 66));
    Assert(dark.HostBackground != light.HostBackground);
    Assert(dark.TerminalSelectionBackground != light.TerminalSelectionBackground);
    Assert(light.PaletteBackground == new RgbColor(253, 246, 227));
    Assert(light.PaletteInputBackground == new RgbColor(238, 232, 213));
    Assert(light.PaletteSelectionBackground == new RgbColor(38, 139, 210));
    Assert(light.PaletteSelectionForeground == new RgbColor(253, 246, 227));
    Assert(dark.TerminalColorTable.Count == 16);
    Assert(light.TerminalColorTable.Count == 16);
    Assert(dark.TerminalColorTable.SequenceEqual(
    [
        new RgbColor(0, 0, 0),
        new RgbColor(128, 0, 0),
        new RgbColor(0, 128, 0),
        new RgbColor(128, 128, 0),
        new RgbColor(0, 0, 128),
        new RgbColor(128, 0, 128),
        new RgbColor(0, 128, 128),
        new RgbColor(192, 192, 192),
        new RgbColor(128, 128, 128),
        new RgbColor(255, 0, 0),
        new RgbColor(0, 255, 0),
        new RgbColor(255, 255, 0),
        new RgbColor(0, 0, 255),
        new RgbColor(255, 0, 255),
        new RgbColor(0, 255, 255),
        new RgbColor(255, 255, 255),
    ]));
    Assert(light.TerminalColorTable[0] == new RgbColor(7, 54, 66));
    Assert(light.TerminalColorTable[15] == new RgbColor(253, 246, 227));
}

static void ThemePaletteSettingsOverrideYaziColors()
{
    var table = Enumerable.Range(0, 16)
        .Select(index => new RgbColor((byte)(index + 10), (byte)(index + 20), (byte)(index + 30)))
        .ToArray();
    var overrides = new ThemeColorOverrides(
        new RgbColor(1, 2, 3),
        new RgbColor(4, 5, 6),
        new RgbColor(7, 8, 9),
        new RgbColor(10, 11, 12),
        new RgbColor(13, 14, 15),
        new RgbColor(16, 17, 18),
        new RgbColor(19, 20, 21),
        new RgbColor(22, 23, 24),
        new RgbColor(25, 26, 27),
        new RgbColor(28, 29, 30),
        new RgbColor(31, 32, 33),
        table);
    var yazi = new YaziThemeColors(
        new RgbColor(100, 101, 102),
        new RgbColor(103, 104, 105),
        new RgbColor(106, 107, 108),
        new RgbColor(109, 110, 111),
        "test-light",
        TerminalBackground: new RgbColor(112, 113, 114));

    var colors = ThemePalette.For(AppThemeMode.Light, yazi, overrides);

    Assert(colors.HostBackground == new RgbColor(1, 2, 3));
    Assert(colors.HostForeground == new RgbColor(4, 5, 6));
    Assert(colors.PaletteBackground == new RgbColor(7, 8, 9));
    Assert(colors.PaletteForeground == new RgbColor(10, 11, 12));
    Assert(colors.PaletteBorder == new RgbColor(13, 14, 15));
    Assert(colors.PaletteInputBackground == new RgbColor(16, 17, 18));
    Assert(colors.PaletteSelectionBackground == new RgbColor(19, 20, 21));
    Assert(colors.PaletteSelectionForeground == new RgbColor(22, 23, 24));
    Assert(colors.TerminalBackground == new RgbColor(25, 26, 27));
    Assert(colors.TerminalForeground == new RgbColor(28, 29, 30));
    Assert(colors.TerminalSelectionBackground == new RgbColor(31, 32, 33));
    Assert(colors.TerminalColorTable.SequenceEqual(table));
}

static void TerminalPasteRecognizesControlShiftV()
{
    Assert(TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x56, true, true, false));
    Assert(TerminalClipboardPaste.IsPasteShortcut(0x0104, 0x56, true, true, false));
}

static void TerminalPasteRecognizesShiftInsert()
{
    Assert(TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x2D, false, true, false));
    Assert(TerminalClipboardPaste.IsPasteShortcut(0x0104, 0x2D, false, true, false));
}

static void TerminalPasteRejectsOtherGesturesAndRepeats()
{
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x56, false, true, false));
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x56, true, false, false));
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x56, true, true, true));
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x2D, true, true, false));
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0100, 0x2D, false, false, false));
    Assert(!TerminalClipboardPaste.IsPasteShortcut(0x0006, 0x56, true, true, false));
}

static void TerminalPasteIgnoresNegativeMouseWheelMessages()
{
    const int wmMouseWheel = 0x020A;
    var negativeWheelWParam = new IntPtr(unchecked((long)0xFF880000));

    Assert(!TerminalClipboardPaste.IsPasteShortcut(
        wmMouseWheel,
        negativeWheelWParam,
        IntPtr.Zero,
        controlDown: true,
        shiftDown: true));
}

static void TerminalPasteFramesUnicodeAndMultilineText()
{
    const string text = "貼り付け\r\nsecond line";
    Assert(TerminalClipboardPaste.Frame(text)
        == "\u001b[200~貼り付け\r\nsecond line\u001b[201~");
}

static void KittyProtocolFilterDropsFlagsPush()
{
    var filter = new KittyKeyboardProtocolFilter();
    var data = "before[>29uafter".ToCharArray().AsSpan();
    filter.Process(ref data);
    Assert(new string(data) == "beforeafter");
}

static void KittyProtocolFilterLeavesQueriesAndPopsUntouched()
{
    var filter = new KittyKeyboardProtocolFilter();
    const string input = "before[?uquery[<upop after";
    var data = input.ToCharArray().AsSpan();
    filter.Process(ref data);
    Assert(new string(data) == input);
}

static void KittyProtocolFilterLeavesUnrelatedEscapeSequencesUntouched()
{
    var filter = new KittyKeyboardProtocolFilter();
    const string input = "before[38;5;196mred[0m after[>29umiddle[?utail";
    var data = input.ToCharArray().AsSpan();
    filter.Process(ref data);
    Assert(new string(data) == "before[38;5;196mred[0m aftermiddle[?utail");
}

static void KittyProtocolFilterDropsPushSplitAcrossChunks()
{
    var filter = new KittyKeyboardProtocolFilter();
    var chunks = new[] { "before", "[>", "2", "9", "u", "tail" };
    var output = new StringBuilder();
    foreach (var chunk in chunks)
    {
        var data = chunk.ToCharArray().AsSpan();
        filter.Process(ref data);
        output.Append(data);
    }

    Assert(output.ToString() == "beforetail");
}

static void KittyProtocolFilterDropsPushSplitAtEveryOffset()
{
    const string sequence = "[>29u";
    for (var splitAt = 1; splitAt < sequence.Length; splitAt++)
    {
        var filter = new KittyKeyboardProtocolFilter();
        var output = new StringBuilder();

        var firstChunk = ("before" + sequence[..splitAt]).ToCharArray().AsSpan();
        filter.Process(ref firstChunk);
        output.Append(firstChunk);

        var secondChunk = (sequence[splitAt..] + "after").ToCharArray().AsSpan();
        filter.Process(ref secondChunk);
        output.Append(secondChunk);

        Assert(output.ToString() == "beforeafter");
    }
}

static void KittyProtocolFilterDropsZeroFlagPush()
{
    var filter = new KittyKeyboardProtocolFilter();
    var data = "before[>uafter".ToCharArray().AsSpan();
    filter.Process(ref data);
    Assert(new string(data) == "beforeafter");
}

static void KittyProtocolFilterPassesThroughPlainText()
{
    var filter = new KittyKeyboardProtocolFilter();
    const string input = "yazi normal output with no escape sequences\r\n";
    var data = input.ToCharArray().AsSpan();
    filter.Process(ref data);
    Assert(new string(data) == input);
}

static void TerminalPasteIgnoresEmptyText()
{
    Assert(!TerminalClipboardPaste.HasText(null));
    Assert(!TerminalClipboardPaste.HasText(string.Empty));
    Assert(TerminalClipboardPaste.HasText("text"));
}

static void HostSettingsRoundTripAndRejectsBlankOrInvalidValues()
{
    var path = Path.Combine(Path.GetTempPath(), $"yazi-settings-test-{Guid.NewGuid():N}.json");
    try
    {
        var darkTable = Enumerable.Range(0, 16)
            .Select(index => new RgbColor((byte)index, (byte)(index + 1), (byte)(index + 2)))
            .ToArray();
        var expected = new HostSettings(
            AppThemeMode.Light,
            "HackGen Console",
            18,
            new ThemeColorOverrides(
                HostBackground: new RgbColor(1, 2, 3),
                TerminalColorTable: darkTable),
            new ThemeColorOverrides(
                PaletteSelectionBackground: new RgbColor(4, 5, 6)));
        HostSettingsStore.Save(expected, path);
        var actual = HostSettingsStore.Load(path);

        Assert(actual.ThemeMode == expected.ThemeMode);
        Assert(actual.FontFamily == expected.FontFamily);
        Assert(actual.FontSize == expected.FontSize);
        Assert(actual.DarkColors?.HostBackground == new RgbColor(1, 2, 3));
        Assert(actual.DarkColors?.TerminalColorTable?.SequenceEqual(darkTable) == true);
        Assert(actual.LightColors?.PaletteSelectionBackground == new RgbColor(4, 5, 6));

        File.WriteAllText(
            path,
            """
            {
              "Theme": "Light",
              "FontFamily": "   ",
              "FontSize": 0,
              "ThemeColors": {
                "Dark": {
                  "HostBackground": "#010203",
                  "HostForeground": 123,
                  "TerminalColorTable": ["#010203"]
                },
                "Light": {
                  "TerminalForeground": "#AABBCC"
                }
              }
            }
            """);
        var fallback = HostSettingsStore.Load(path);
        Assert(fallback.ThemeMode == AppThemeMode.Light);
        Assert(fallback.FontFamily == HostSettingsCatalog.DefaultFontFamily);
        Assert(fallback.FontSize == HostSettingsCatalog.DefaultFontSize);
        Assert(fallback.DarkColors?.HostBackground == new RgbColor(1, 2, 3));
        Assert(fallback.DarkColors?.HostForeground is null);
        Assert(fallback.DarkColors?.TerminalColorTable is null);
        Assert(fallback.LightColors?.TerminalForeground == new RgbColor(170, 187, 204));
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void HostSettingsAcceptCustomFontFamiliesAndSizes()
{
    Assert(HostSettingsCatalog.TryNormalizeFontFamily("HackGen Console", out var customFontFamily));
    Assert(customFontFamily == "HackGen Console");
    Assert(!HostSettingsCatalog.TryNormalizeFontFamily("   ", out _));
    Assert(HostSettingsCatalog.IsValidFontSize(13));
    Assert(HostSettingsCatalog.IsValidFontSize(short.MaxValue));
    Assert(!HostSettingsCatalog.IsValidFontSize(0));
    Assert(!HostSettingsCatalog.IsValidFontSize(short.MaxValue + 1));
    Assert(HostSettingsCatalog.DefaultFontFamily == "MS Gothic");
    Assert(HostSettingsCatalog.DefaultFontSize == 14);
}

static void WindowPlacementSettingsRoundTripAndIgnoreMalformedPlacement()
{
    var path = Path.Combine(Path.GetTempPath(), $"yazi-placement-test-{Guid.NewGuid():N}.json");
    try
    {
        var expectedPlacement = new WindowPlacementSettings(
            @"\\.\DISPLAY2",
            [
                new MonitorWindowPlacement(
                    @"\\.\DISPLAY1",
                    new WindowBounds(-120, 80, 1080, 880),
                    WindowPlacementShowState.Normal),
                new MonitorWindowPlacement(
                    @"\\.\DISPLAY2",
                    new WindowBounds(1920, 120, 3200, 920),
                    WindowPlacementShowState.Maximized),
            ]);
        HostSettingsStore.Save(
            new HostSettings(AppThemeMode.Light, "HackGen Console", 18, WindowPlacement: expectedPlacement),
            path);

        var actual = HostSettingsStore.Load(path);
        Assert(actual.ThemeMode == AppThemeMode.Light);
        Assert(actual.WindowPlacement?.LastMonitorId == expectedPlacement.LastMonitorId);
        Assert(actual.WindowPlacement?.Monitors.Count == 2);
        Assert(actual.WindowPlacement?.Monitors.Single(item => item.MonitorId.EndsWith("DISPLAY2"))
            .ShowState == WindowPlacementShowState.Maximized);

        File.WriteAllText(
            path,
            "{\"Theme\":\"Light\",\"FontFamily\":\"HackGen Console\",\"FontSize\":18,\"WindowPlacement\":\"invalid\"}");
        var malformed = HostSettingsStore.Load(path);
        Assert(malformed.ThemeMode == AppThemeMode.Light);
        Assert(malformed.FontFamily == "HackGen Console");
        Assert(malformed.FontSize == 18);
        Assert(malformed.WindowPlacement is null);
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void WindowPlacementCatalogKeepsPerMonitorPlacements()
{
    var first = new MonitorWindowPlacement(
        @"\\.\DISPLAY1",
        new WindowBounds(10, 20, 1010, 720),
        WindowPlacementShowState.Normal);
    var second = new MonitorWindowPlacement(
        @"\\.\DISPLAY2",
        new WindowBounds(1920, 40, 3120, 840),
        WindowPlacementShowState.Maximized);
    var updatedFirst = first with
    {
        NormalBounds = new WindowBounds(50, 60, 1250, 860),
    };

    var settings = WindowPlacementCatalog.Upsert(null, first);
    settings = WindowPlacementCatalog.Upsert(settings, second);
    settings = WindowPlacementCatalog.Upsert(settings, updatedFirst);

    Assert(settings.LastMonitorId == first.MonitorId);
    Assert(settings.Monitors.Count == 2);
    Assert(settings.Monitors.Single(item => item.MonitorId == first.MonitorId).NormalBounds == updatedFirst.NormalBounds);
    Assert(settings.Monitors.Single(item => item.MonitorId == second.MonitorId).ShowState == second.ShowState);
}

static void WindowPlacementCatalogSelectsConnectedFallbackAndClampsBounds()
{
    var settings = new WindowPlacementSettings(
        @"\\.\DISPLAY3",
        [
            new MonitorWindowPlacement(
                @"\\.\DISPLAY1",
                new WindowBounds(-500, -400, 1500, 1200),
                WindowPlacementShowState.Normal),
            new MonitorWindowPlacement(
                @"\\.\DISPLAY2",
                new WindowBounds(2000, 200, 2800, 800),
                WindowPlacementShowState.Maximized),
        ]);
    var connected = new[]
    {
        new ConnectedMonitor(@"\\.\DISPLAY1", new WindowBounds(0, 0, 1920, 1040)),
        new ConnectedMonitor(@"\\.\DISPLAY2", new WindowBounds(1920, 0, 3840, 2160)),
    };

    var selected = WindowPlacementCatalog.Select(settings, connected);
    Assert(selected?.MonitorId == @"\\.\DISPLAY1");
    var clamped = WindowPlacementCatalog.Clamp(
        selected!.NormalBounds,
        connected[0].WorkArea);
    Assert(clamped == new WindowBounds(0, 0, 1920, 1040));

    var lastMonitorAvailable = settings with { LastMonitorId = @"\\.\DISPLAY2" };
    Assert(WindowPlacementCatalog.Select(lastMonitorAvailable, connected)?.MonitorId == @"\\.\DISPLAY2");
}

static void YaziThemeLoaderReadsSelectedFlavor()
{
    var configHome = Path.Combine(Path.GetTempPath(), $"yazi-theme-test-{Guid.NewGuid():N}");
    var flavorDirectory = Path.Combine(configHome, "flavors", "test-light.yazi");
    Directory.CreateDirectory(flavorDirectory);

    try
    {
        File.WriteAllText(
            Path.Combine(configHome, "theme.toml"),
            """
            [flavor]
            dark = "test-dark"
            light = "test-light"
            """);
        File.WriteAllText(
            Path.Combine(flavorDirectory, "flavor.toml"),
            """
            [mgr]
            cwd = { fg = "#179299" }
            border_style = { fg = "#8c8fa1" }

            [app]
            overall = { bg = "#fdf6e3" }

            [tabs]
            active = { fg = "#eff1f5", bg = "#1e66f5", bold = true }

            [filetype]
            rules = [
                { url = "*", fg = "#4c4f69" },
                { url = "*/", fg = "#1e66f5" },
            ]
            """);

        var colors = YaziThemeLoader.Load(AppThemeMode.Light, configHome);
        Assert(colors is not null);
        Assert(colors!.Foreground == new RgbColor(76, 79, 105));
        Assert(colors.Border == new RgbColor(140, 143, 161));
        Assert(colors.SelectionBackground == new RgbColor(30, 102, 245));
        Assert(colors.SelectionForeground == new RgbColor(239, 241, 245));
        Assert(colors.FlavorName == "test-light");
        Assert(colors.FileForeground == new RgbColor(76, 79, 105));
        Assert(colors.DirectoryForeground == new RgbColor(30, 102, 245));
        Assert(colors.TerminalBackground == new RgbColor(253, 246, 227));

        var palette = ThemePalette.For(AppThemeMode.Light, colors);
        Assert(palette.HostBackground == new RgbColor(238, 232, 213));
        Assert(palette.TerminalBackground == new RgbColor(253, 246, 227));
        Assert(palette.HostForeground == new RgbColor(76, 79, 105));
        Assert(palette.PaletteBorder == new RgbColor(140, 143, 161));
    }
    finally
    {
        if (Directory.Exists(configHome))
        {
            Directory.Delete(configHome, recursive: true);
        }
    }
}

static void CommandPaletteFiltersThemeCommands()
{
    var commands = CommandPaletteCommands.All;

    Assert(commands.Count == 3);
    Assert(CommandPaletteCommands.Filter(commands, "light").Single().Id == PaletteCommandId.LightTheme);
    Assert(CommandPaletteCommands.Filter(commands, "dark host").Single().Id == PaletteCommandId.DarkTheme);
    Assert(CommandPaletteCommands.Filter(commands, "settings.json").Single().Id == PaletteCommandId.EditSettings);
    Assert(CommandPaletteCommands.Filter(commands, "missing").Count == 0);
    Assert(CommandPaletteCommands.Filter(commands, " ").SequenceEqual(commands));
}

static void CommandPaletteIncludesYaziCommands()
{
    var commands = CommandPaletteCommands.WithYaziCommands([
        new YaziBridgeCommand(
            "g d",
            "cd C:\\work",
            "Go work",
            ["cd C:\\work", "plugin refresh"]),
    ]);

    Assert(commands.Count == 4);
    Assert(commands[^1].Id == PaletteCommandId.YaziAction);
    Assert(commands[^1].Title == "Yazi: Go work");
    var yaziCommand = CommandPaletteCommands.Filter(commands, "plugin refresh").Single().YaziCommand;
    Assert(yaziCommand?.ActionSequence.SequenceEqual(["cd C:\\work", "plugin refresh"]) == true);
}

static void PaletteNavigationHandlesEmptyListsAndSelectionBoundaries()
{
    Assert(PaletteNavigation.NextIndex(0, -1, 1) == -1);
    Assert(PaletteNavigation.NextIndex(0, -1, -1) == -1);
    Assert(PaletteNavigation.NextIndex(3, -1, 1) == 0);
    Assert(PaletteNavigation.NextIndex(3, -1, -1) == 2);
    Assert(PaletteNavigation.NextIndex(3, -1, 0) == -1);
}

static void PaletteNavigationWrapsAtFirstAndLastRows()
{
    Assert(PaletteNavigation.NextIndex(3, 0, -1) == 2);
    Assert(PaletteNavigation.NextIndex(3, 2, 1) == 0);
    Assert(PaletteNavigation.NextIndex(3, 1, 1) == 2);
    Assert(PaletteNavigation.NextIndex(3, 1, -1) == 0);
}

static void PaletteNavigationUsesJAndKOnlyForEmptyQueries()
{
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.J, true, string.Empty) == 1);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.K, true, "   ") == -1);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.J, false, string.Empty) is null);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.K, true, "theme") is null);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.Down, false, "theme") == 1);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.Up, false, "theme") == -1);
}

static void PaletteNavigationLeavesFilterTextInputUnhandled()
{
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.Other, true, string.Empty) is null);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.J, true, "j") is null);
    Assert(PaletteNavigation.TryGetMoveOffset(PaletteNavigationKey.K, true, "dark") is null);
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

sealed class FakeShellContextMenuMessageHandler : IShellContextMenuMessageHandler
{
    public bool HandlesMenuMsg2 { get; init; }

    public int MenuMsg2HResult { get; init; }

    public IntPtr MenuMsg2Result { get; init; }

    public bool HandlesMenuMsg { get; init; }

    public int MenuMsgHResult { get; init; }

    public int MenuMsg2CallCount { get; private set; }

    public int MenuMsgCallCount { get; private set; }

    public uint LastMessage { get; private set; }

    public IntPtr LastWParam { get; private set; }

    public IntPtr LastLParam { get; private set; }

    public bool TryHandleMenuMsg2(
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        out int hResult,
        out IntPtr result)
    {
        MenuMsg2CallCount++;
        LastMessage = message;
        LastWParam = wParam;
        LastLParam = lParam;
        hResult = MenuMsg2HResult;
        result = MenuMsg2Result;
        return HandlesMenuMsg2;
    }

    public bool TryHandleMenuMsg(
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        out int hResult)
    {
        MenuMsgCallCount++;
        LastMessage = message;
        LastWParam = wParam;
        LastLParam = lParam;
        hResult = MenuMsgHResult;
        return HandlesMenuMsg;
    }
}

sealed class DelayedPathTransactionController : IYaziPathTransactionController
{
    private readonly TaskCompletionSource _openRelease = new();
    private readonly TaskCompletionSource _changeDirectoryRelease = new();
    private readonly List<string> _operations = [];

    public ManualResetEventSlim OpenRevealStarted { get; } = new();

    public ManualResetEventSlim ChangeDirectoryStarted { get; } = new();

    public IReadOnlyList<string> Operations
    {
        get
        {
            lock (_operations)
            {
                return _operations.ToArray();
            }
        }
    }

    public async Task<bool> ChangeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        AddOperation("cd B");
        ChangeDirectoryStarted.Set();
        await _changeDirectoryRelease.Task.WaitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> OpenFileAsync(string filePath, CancellationToken cancellationToken)
    {
        AddOperation("reveal A");
        OpenRevealStarted.Set();
        await _openRelease.Task.WaitAsync(cancellationToken);
        AddOperation("open A");
        return true;
    }

    public void AllowOpen() => _openRelease.SetResult();

    public void AllowChangeDirectory() => _changeDirectoryRelease.SetResult();

    private void AddOperation(string operation)
    {
        lock (_operations)
        {
            _operations.Add(operation);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace OneDriveShellPoc;

internal static class Program
{
    private const uint CommandFirst = 1;
    private const uint CommandLast = 0x7FFF;
    private const uint CmfExplore = 0x00000004;
    private const uint GcsVerbA = 0x00000000;
    private const uint GcsVerbW = 0x00000004;
    private const uint MiimFtype = 0x00000001;
    private const uint MiimId = 0x00000002;
    private const uint MiimSubmenu = 0x00000004;
    private const uint MfSeparator = 0x00000800;
    private const uint CmicMaskUnicode = 0x00004000;
    private const int SwShownormal = 1;
    private const int MaxMenuDepth = 32;
    private const int MaxTextLength = 1024;
    private const int DefaultVerbProbeTimeoutMilliseconds = 3000;
    private const int MaxVerbProbeTimeoutMilliseconds = 60000;

    private static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IidContextMenu = new("000214E4-0000-0000-C000-000000000046");

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 6 && string.Equals(args[0], "--probe-verb", StringComparison.Ordinal))
        {
            return RunVerbProbe(args[1], args[2], args[3], args[4], args[5]);
        }

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (!TryParseArguments(args, out var options, out var error))
        {
            Console.Error.WriteLine($"error: {error}");
            PrintUsage();
            return 1;
        }

        try
        {
            using var context = ShellContext.Open(options.Path);
            var entries = context.Enumerate(
                includeCanonicalVerbs: options.Invocation?.CommandId is null,
                options.VerbProbeTimeoutMilliseconds);
            if (options.Invocation is not null)
            {
                var invocation = ResolveInvocation(entries, options.Invocation);
                if (invocation.Error is not null)
                {
                    Console.Error.WriteLine($"error: {invocation.Error}");
                    return 3;
                }

                context.Invoke(invocation.Entry!, options.Invocation);
                WriteInvocationResult(options, invocation.Entry!);
                return 0;
            }

            WriteResults(options, entries);
            return 0;
        }
        catch (ShellPocException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
        catch (COMException exception)
        {
            Console.Error.WriteLine($"error: COM operation failed ({FormatHResult(exception.HResult)})");
            return 2;
        }
        catch (Win32Exception exception)
        {
            Console.Error.WriteLine($"error: Win32 operation failed ({exception.NativeErrorCode})");
            return 2;
        }
        catch (InvalidCastException)
        {
            Console.Error.WriteLine("error: Shell returned an incompatible COM interface");
            return 2;
        }
    }

    private static int RunVerbProbe(
        string path,
        string offsetText,
        string positionsText,
        string expectedPathText,
        string commandIdText)
    {
        var positions = positionsText.Split(',');
        if (!uint.TryParse(offsetText, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
            || !uint.TryParse(commandIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var commandId)
            || commandId is < CommandFirst or > CommandLast
            || positions.Length is 0 or > MaxMenuDepth + 1
            || positions.Any(position => !uint.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return 1;
        }

        string[]? expectedPath;
        try
        {
            expectedPath = JsonSerializer.Deserialize<string[]>(expectedPathText);
        }
        catch (JsonException)
        {
            return 1;
        }

        if (expectedPath is null || expectedPath.Length != positions.Length)
        {
            return 1;
        }

        try
        {
            using var context = ShellContext.Open(path);
            var result = context.ReadCanonicalVerbDirect(
                offset,
                commandId,
                positions.Select(position => uint.Parse(position, CultureInfo.InvariantCulture)).ToArray(),
                expectedPath);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = result.Status,
                value = result.Value,
            }));
            return 0;
        }
        catch (COMException exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = $"failed:{FormatHResult(exception.HResult)}",
                value = (string?)null,
            }));
            return 0;
        }
        catch (Win32Exception exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = $"failed:win32-{exception.NativeErrorCode}",
                value = (string?)null,
            }));
            return 0;
        }
        catch (ShellPocException)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "failed:shell-error",
                value = (string?)null,
            }));
            return 0;
        }
        catch (InvalidCastException)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "failed:invalid-com-interface",
                value = (string?)null,
            }));
            return 0;
        }
    }

    private static bool TryParseArguments(
        string[] args,
        out Options options,
        out string error)
    {
        options = default;
        error = string.Empty;
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith('-'))
        {
            error = "exactly one filesystem path is required";
            return false;
        }

        var json = false;
        var verbProbeTimeoutMilliseconds = DefaultVerbProbeTimeoutMilliseconds;
        var verbProbeTimeoutSpecified = false;
        Invocation? invocation = null;
        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--json":
                    if (json)
                    {
                        error = "--json was specified more than once";
                        return false;
                    }

                    json = true;
                    break;
                case "--verb-probe-timeout-ms":
                    if (verbProbeTimeoutSpecified)
                    {
                        error = "--verb-probe-timeout-ms was specified more than once";
                        return false;
                    }

                    if (!TryReadValue(args, ref index, out var timeoutText)
                        || !int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out verbProbeTimeoutMilliseconds)
                        || verbProbeTimeoutMilliseconds is < 100 or > MaxVerbProbeTimeoutMilliseconds)
                    {
                        error = $"--verb-probe-timeout-ms must be between 100 and {MaxVerbProbeTimeoutMilliseconds}";
                        return false;
                    }

                    verbProbeTimeoutSpecified = true;
                    break;
                case "--invoke-id":
                    if (invocation is not null || !TryReadValue(args, ref index, out var idText))
                    {
                        error = "one invocation option and its value are required";
                        return false;
                    }

                    if (!uint.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var commandId)
                        || commandId < CommandFirst
                        || commandId > CommandLast)
                    {
                        error = "--invoke-id must be a positive unsigned command ID";
                        return false;
                    }

                    invocation = Invocation.ById(commandId);
                    break;
                case "--invoke-verb":
                    if (invocation is not null || !TryReadValue(args, ref index, out var verb))
                    {
                        error = "one invocation option and its value are required";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(verb) || verb.Any(char.IsWhiteSpace))
                    {
                        error = "--invoke-verb must be a non-empty canonical verb";
                        return false;
                    }

                    invocation = Invocation.ByVerb(verb);
                    break;
                default:
                    error = $"unknown option '{args[index]}'";
                    return false;
            }
        }

        if (verbProbeTimeoutSpecified && invocation?.CommandId is not null)
        {
            error = "--verb-probe-timeout-ms cannot be used with --invoke-id";
            return false;
        }

        options = new Options(args[0], json, invocation, verbProbeTimeoutMilliseconds);
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Length)
        {
            return false;
        }

        value = args[++index];
        return !value.StartsWith('-');
    }

    private static InvocationResolution ResolveInvocation(
        IReadOnlyList<MenuEntry> entries,
        Invocation invocation)
    {
        if (invocation.CommandId is uint commandId)
        {
            var entry = entries.FirstOrDefault(candidate =>
                candidate.CommandId == commandId && !candidate.HasSubmenu);
            return entry is null
                ? new(null, $"command ID {commandId} was not returned as a leaf menu command")
                : new(entry, null);
        }

        var matches = entries
            .Where(candidate => candidate is { HasSubmenu: false }
                && candidate.CanonicalVerbStatus.StartsWith("ok-", StringComparison.Ordinal)
                && string.Equals(candidate.CanonicalVerb, invocation.Verb, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            1 => new(matches[0], null),
            0 => new(null, "canonical verb did not identify a returned Shell menu command"),
            _ => new(null, "canonical verb matched multiple Shell menu commands"),
        };
    }

    private static void WriteResults(Options options, IReadOnlyList<MenuEntry> entries)
    {
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(entries, JsonOptions));
            return;
        }

        Console.WriteLine($"commands={entries.Count}");
        foreach (var entry in entries)
        {
            var id = entry.CommandId?.ToString() ?? "-";
            var offset = entry.CommandOffset?.ToString() ?? "-";
            var verb = entry.CanonicalVerb ?? "-";
            Console.WriteLine(
                $"id={id} offset={offset} submenu={entry.HasSubmenu.ToString().ToLowerInvariant()} "
                + $"verbStatus={entry.CanonicalVerbStatus} verb={Quote(verb)} text={Quote(entry.MenuText)} "
                + $"menuPath={Quote(string.Join(" > ", entry.MenuPath))}");
        }
    }

    private static void WriteInvocationResult(Options options, MenuEntry entry)
    {
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new { invoked = true, command = entry },
                JsonOptions));
            return;
        }

        Console.WriteLine(
            $"invoked=true id={entry.CommandId} offset={entry.CommandOffset} "
            + $"verb={Quote(entry.CanonicalVerb ?? "-")} text={Quote(entry.MenuText)}");
    }

    private static string Quote(string value) =>
        JsonSerializer.Serialize(value);

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  OneDriveShellPoc.exe <path> [--verb-probe-timeout-ms <100..60000>] [--json]");
        Console.WriteLine("  OneDriveShellPoc.exe <path> --invoke-id <command-id> [--json]");
        Console.WriteLine("  OneDriveShellPoc.exe <path> --invoke-verb <canonical-verb> [--verb-probe-timeout-ms <100..60000>] [--json]");
        Console.WriteLine();
        Console.WriteLine("Enumeration is the default. Invocation runs exactly one validated Shell command.");
        Console.WriteLine("Canonical-verb probing starts a separate worker for each leaf command (default timeout: 3000 ms).");
    }

    private static string FormatHResult(int hResult) => $"0x{hResult:X8}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly record struct Options(
        string Path,
        bool Json,
        Invocation? Invocation,
        int VerbProbeTimeoutMilliseconds);

    private sealed record Invocation(uint? CommandId, string? Verb)
    {
        public static Invocation ById(uint commandId) => new(commandId, null);

        public static Invocation ByVerb(string verb) => new(null, verb);
    }

    private sealed record InvocationResolution(MenuEntry? Entry, string? Error);

    private sealed record MenuEntry(
        uint? CommandId,
        uint? CommandOffset,
        string MenuText,
        bool HasSubmenu,
        string CanonicalVerbStatus,
        string? CanonicalVerb,
        IReadOnlyList<string> MenuPath);

    private sealed class ShellPocException(string message) : Exception(message);

    private sealed class ShellContext : IDisposable
    {
        private readonly string _path;
        private readonly IntPtr _pidl;
        private readonly IShellFolder _parent;
        private readonly IContextMenu _contextMenu;
        private readonly IntPtr _menu;
        private bool _disposed;

        private ShellContext(
            string path,
            IntPtr pidl,
            IShellFolder parent,
            IContextMenu contextMenu,
            IntPtr menu)
        {
            _path = path;
            _pidl = pidl;
            _parent = parent;
            _contextMenu = contextMenu;
            _menu = menu;
        }

        public static ShellContext Open(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new ShellPocException("the target path does not exist");
            }

            var hr = SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
            ThrowIfFailed(hr, "SHParseDisplayName");

            IShellFolder? parent = null;
            IntPtr contextMenuPointer = IntPtr.Zero;
            try
            {
                var iidShellFolder = IidShellFolder;
                hr = SHBindToParent(
                    pidl,
                    ref iidShellFolder,
                    out parent,
                    out var childPidl);
                ThrowIfFailed(hr, "SHBindToParent");

                var childPidlArray = Marshal.AllocCoTaskMem(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(childPidlArray, childPidl);
                    var iidContextMenu = IidContextMenu;
                    hr = parent.GetUIObjectOf(
                        IntPtr.Zero,
                        1,
                        childPidlArray,
                        ref iidContextMenu,
                        IntPtr.Zero,
                        out contextMenuPointer);
                    ThrowIfFailed(hr, "IShellFolder.GetUIObjectOf");
                }
                finally
                {
                    Marshal.FreeCoTaskMem(childPidlArray);
                }

                var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPointer);
                Marshal.Release(contextMenuPointer);
                contextMenuPointer = IntPtr.Zero;

                var menu = CreatePopupMenu();
                if (menu == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    Marshal.ReleaseComObject(contextMenu);
                    throw new Win32Exception(error);
                }

                try
                {
                    hr = contextMenu.QueryContextMenu(
                        menu,
                        0,
                        CommandFirst,
                        CommandLast,
                        CmfExplore);
                    ThrowIfFailed(hr, "IContextMenu.QueryContextMenu");
                }
                catch
                {
                    DestroyMenu(menu);
                    Marshal.ReleaseComObject(contextMenu);
                    throw;
                }

                return new ShellContext(path, pidl, parent, contextMenu, menu);
            }
            catch
            {
                if (contextMenuPointer != IntPtr.Zero)
                {
                    Marshal.Release(contextMenuPointer);
                }

                if (parent is not null)
                {
                    Marshal.ReleaseComObject(parent);
                }

                CoTaskMemFree(pidl);
                throw;
            }
        }

        public IReadOnlyList<MenuEntry> Enumerate(bool includeCanonicalVerbs, int verbProbeTimeoutMilliseconds)
        {
            var entries = new List<MenuEntry>();
            EnumerateMenu(_menu, [], [], entries, 0, includeCanonicalVerbs, verbProbeTimeoutMilliseconds);
            return entries;
        }

        public void Invoke(MenuEntry entry, Invocation invocation)
        {
            var hr = invocation.CommandId is not null
                ? InvokeById(entry.CommandOffset
                    ?? throw new ShellPocException("the selected command offset is unavailable"))
                : InvokeByVerb(entry.CanonicalVerb
                    ?? throw new ShellPocException("the selected canonical verb is unavailable"));
            ThrowIfFailed(hr, "IContextMenu.InvokeCommand");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyMenu(_menu);
            Marshal.ReleaseComObject(_contextMenu);
            Marshal.ReleaseComObject(_parent);
            CoTaskMemFree(_pidl);
        }

        private void EnumerateMenu(
            IntPtr menu,
            IReadOnlyList<string> parentPath,
            IReadOnlyList<uint> parentPositions,
            ICollection<MenuEntry> entries,
            int depth,
            bool includeCanonicalVerbs,
            int verbProbeTimeoutMilliseconds)
        {
            if (depth > MaxMenuDepth)
            {
                throw new ShellPocException("Shell menu nesting exceeded the safety limit");
            }

            var itemCount = GetMenuItemCount(menu);
            if (itemCount < 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            for (uint position = 0; position < itemCount; position++)
            {
                var info = new MENUITEMINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                    fMask = MiimFtype | MiimId | MiimSubmenu,
                };
                if (!GetMenuItemInfo(menu, position, true, ref info))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if ((info.fType & MfSeparator) != 0)
                {
                    continue;
                }

                var text = ReadMenuText(menu, position);
                var currentPath = parentPath.Concat([text]).ToArray();
                var currentPositions = parentPositions.Concat([position]).ToArray();
                var hasSubmenu = info.hSubMenu != IntPtr.Zero;
                uint? commandId = hasSubmenu ? null : info.wID;
                var hasCommand = commandId is not null
                    && commandId.Value is >= CommandFirst and <= CommandLast;
                uint? commandOffset = hasCommand ? commandId!.Value - CommandFirst : null;
                var verb = hasCommand && includeCanonicalVerbs
                    ? ReadCanonicalVerb(
                        commandOffset!.Value,
                        commandId!.Value,
                        currentPositions,
                        currentPath,
                        verbProbeTimeoutMilliseconds)
                    : new VerbResult(hasCommand ? "not-requested" : "not-applicable", null);

                entries.Add(new MenuEntry(
                    hasCommand ? commandId : null,
                    commandOffset,
                    text,
                    hasSubmenu,
                    verb.Status,
                    verb.Value,
                    currentPath));

                if (hasSubmenu)
                {
                    EnumerateMenu(
                        info.hSubMenu,
                        currentPath,
                        currentPositions,
                        entries,
                        depth + 1,
                        includeCanonicalVerbs,
                        verbProbeTimeoutMilliseconds);
                }
            }
        }

        private VerbResult ReadCanonicalVerb(
            uint commandOffset,
            uint commandId,
            IReadOnlyList<uint> positions,
            IReadOnlyList<string> expectedPath,
            int timeoutMilliseconds)
        {
            var processPath = Environment.ProcessPath
                ?? throw new ShellPocException("the worker process path is unavailable");
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            if (string.Equals(
                    Path.GetFileNameWithoutExtension(processPath),
                    "dotnet",
                    StringComparison.OrdinalIgnoreCase))
            {
                var entryAssembly = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(entryAssembly))
                {
                    throw new ShellPocException("the entry assembly path is unavailable");
                }

                startInfo.ArgumentList.Add(entryAssembly);
            }

            startInfo.ArgumentList.Add("--probe-verb");
            startInfo.ArgumentList.Add(_path);
            startInfo.ArgumentList.Add(commandOffset.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(string.Join(",", positions));
            startInfo.ArgumentList.Add(JsonSerializer.Serialize(expectedPath));
            startInfo.ArgumentList.Add(commandId.ToString(CultureInfo.InvariantCulture));
            using var process = Process.Start(startInfo)
                ?? throw new ShellPocException("the canonical-verb worker could not start");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(milliseconds: timeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The worker may have exited between WaitForExit and Kill.
                }

                return new VerbResult("timeout", null);
            }

            var output = outputTask.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
            {
                return new VerbResult($"native-crash:{process.ExitCode}", null);
            }

            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;
                var status = root.GetProperty("status").GetString();
                var value = root.GetProperty("value").GetString();
                return new VerbResult(status ?? "worker-invalid-result", value);
            }
            catch (JsonException)
            {
                return new VerbResult("worker-invalid-result", null);
            }
        }

        public VerbResult ReadCanonicalVerbDirect(
            uint commandOffset,
            uint expectedCommandId,
            IReadOnlyList<uint> positions,
            IReadOnlyList<string> expectedPath)
        {
            var menu = _menu;
            for (var depth = 0; depth < positions.Count; depth++)
            {
                var position = positions[depth];
                var itemCount = GetMenuItemCount(menu);
                if (itemCount < 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (position >= itemCount)
                {
                    return new VerbResult("menu-mismatch", null);
                }

                var info = new MENUITEMINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                    fMask = MiimFtype | MiimId | MiimSubmenu,
                };
                if (!GetMenuItemInfo(menu, position, true, ref info))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (depth < positions.Count - 1)
                {
                    if ((info.fType & MfSeparator) != 0
                        || info.hSubMenu == IntPtr.Zero
                        || !string.Equals(ReadMenuText(menu, position), expectedPath[depth], StringComparison.Ordinal))
                    {
                        return new VerbResult("menu-mismatch", null);
                    }

                    menu = info.hSubMenu;
                    continue;
                }

                if ((info.fType & MfSeparator) != 0
                    || info.hSubMenu != IntPtr.Zero
                    || info.wID != expectedCommandId
                    || expectedCommandId - CommandFirst != commandOffset
                    || !string.Equals(ReadMenuText(menu, position), expectedPath[depth], StringComparison.Ordinal))
                {
                    return new VerbResult("menu-mismatch", null);
                }
            }

            var buffer = Marshal.AllocCoTaskMem(MaxTextLength * sizeof(char));
            try
            {
                Marshal.Copy(new byte[MaxTextLength * sizeof(char)], 0, buffer, MaxTextLength * sizeof(char));
                var unicodeHr = _contextMenu.GetCommandString(
                    (IntPtr)commandOffset,
                    GcsVerbW,
                    IntPtr.Zero,
                    buffer,
                    MaxTextLength);
                if (unicodeHr >= 0)
                {
                    var unicodeValue = Marshal.PtrToStringUni(buffer)?.TrimEnd('\0') ?? string.Empty;
                    if (!string.IsNullOrEmpty(unicodeValue))
                    {
                        return new VerbResult("ok-unicode", unicodeValue);
                    }
                }

                var fallbackReason = unicodeHr >= 0 ? "unicode-empty" : "unicode-failure";
                Marshal.Copy(new byte[MaxTextLength * sizeof(char)], 0, buffer, MaxTextLength * sizeof(char));
                var hr = _contextMenu.GetCommandString(
                    (IntPtr)commandOffset,
                    GcsVerbA,
                    IntPtr.Zero,
                    buffer,
                    MaxTextLength);
                if (hr < 0)
                {
                    var unicodeStatus = unicodeHr < 0
                        ? $"failed-unicode:{FormatHResult(unicodeHr)}"
                        : "empty-unicode";
                    return new VerbResult(
                        $"{unicodeStatus};failed-ansi:{FormatHResult(hr)}",
                        null);
                }

                var value = Marshal.PtrToStringAnsi(buffer)?.TrimEnd('\0') ?? string.Empty;
                return string.IsNullOrEmpty(value)
                    ? new VerbResult($"empty-ansi-after-{fallbackReason}", null)
                    : new VerbResult($"ok-ansi-after-{fallbackReason}", value);
            }
            finally
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }

        private int InvokeById(uint commandOffset)
        {
            var info = new CMINVOKECOMMANDINFO
            {
                cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                hwnd = IntPtr.Zero,
                lpVerb = (IntPtr)commandOffset,
                nShow = SwShownormal,
            };
            var pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<CMINVOKECOMMANDINFO>());
            try
            {
                Marshal.StructureToPtr(info, pointer, false);
                return _contextMenu.InvokeCommand(pointer);
            }
            finally
            {
                Marshal.DestroyStructure<CMINVOKECOMMANDINFO>(pointer);
                Marshal.FreeCoTaskMem(pointer);
            }
        }

        private int InvokeByVerb(string verb)
        {
            var ansiVerbPointer = IntPtr.Zero;
            var verbPointer = IntPtr.Zero;
            var pointer = IntPtr.Zero;
            try
            {
                ansiVerbPointer = Marshal.StringToCoTaskMemAnsi(verb);
                verbPointer = Marshal.StringToCoTaskMemUni(verb);
                var info = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    fMask = CmicMaskUnicode,
                    hwnd = IntPtr.Zero,
                    lpVerb = ansiVerbPointer,
                    lpVerbW = verbPointer,
                    nShow = SwShownormal,
                };
                pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<CMINVOKECOMMANDINFOEX>());
                Marshal.StructureToPtr(info, pointer, false);
                return _contextMenu.InvokeCommand(pointer);
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                {
                    Marshal.DestroyStructure<CMINVOKECOMMANDINFOEX>(pointer);
                    Marshal.FreeCoTaskMem(pointer);
                }

                if (verbPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(verbPointer);
                }

                if (ansiVerbPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ansiVerbPointer);
                }
            }
        }

        private static string ReadMenuText(IntPtr menu, uint position)
        {
            var buffer = new StringBuilder(MaxTextLength);
            var length = GetMenuString(menu, position, buffer, buffer.Capacity, 0x0400);
            return length == 0 ? string.Empty : buffer.ToString();
        }
    }

    private readonly record struct VerbResult(string Status, string? Value);

    private static void ThrowIfFailed(int hResult, string operation)
    {
        if (hResult < 0)
        {
            throw new COMException(operation, hResult);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string name,
        IntPtr bindingContext,
        out IntPtr pidl,
        uint attributes,
        out uint attributesOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellFolder parent,
        out IntPtr childPidl);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pointer);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetMenuString(
        IntPtr menu,
        uint item,
        StringBuilder stringBuffer,
        int maxCount,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMenuItemInfo(
        IntPtr menu,
        uint item,
        [MarshalAs(UnmanagedType.Bool)] bool byPosition,
        ref MENUITEMINFO menuItemInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMenuItemCount(IntPtr menu);

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string name, ref uint eaten, out IntPtr pidl, ref uint attributes);
        int EnumObjects(IntPtr hwnd, uint flags, out IntPtr enumIdList);
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int CompareIds(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr result);
        int GetAttributesOf(uint count, IntPtr[] pidls, ref uint attributes);
        int GetUIObjectOf(IntPtr hwndOwner, uint count, IntPtr pidls, ref Guid riid, IntPtr reserved, out IntPtr result);
        int GetDisplayNameOf(IntPtr pidl, uint flags, out IntPtr name);
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IntPtr newPidl);
    }

    [ComImport]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig]
        int InvokeCommand(IntPtr info);
        [PreserveSig]
        int GetCommandString(IntPtr command, uint flags, IntPtr reserved, IntPtr name, uint max);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MENUITEMINFO
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public IntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }
}

using YaziDesktopHost;

var tests = new (string Name, Action Test)[]
{
    ("explicit path takes precedence", ExplicitPathTakesPrecedence),
    ("PATH lookup finds yazi.exe", PathLookupFindsExecutable),
    ("missing executable is classified", MissingExecutableIsClassified),
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
        failures.Add($"FAIL {name}: {exception.Message}");
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

static void Assert(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Assertion failed.");
    }
}

namespace YaziDesktopHost;

// Workaround for Issue #59: Microsoft.Terminal.Control's Kitty keyboard
// protocol handler resolves alternate keys via a function-local static
// `LoadKeyboardLayoutW(L"00000409", 0)` (en-US) the first time it is asked to
// report alternate keys, which has the side effect of registering an
// unwanted en-US CTF/TSF input profile on Windows. That path is only ever
// reached once the terminal control's Kitty protocol state has been enabled
// via a client-issued flags push (`CSI > <flags> u`).
//
// An earlier attempt stripped only the REPORT_ALTERNATE_KEYS bit from that
// push, but Yazi turned out to rely on the alternate-key reporting this same
// bit gates (e.g. Shift+letter bindings such as zoxide's Shift+Z stopped
// working). Windows Terminal does not reproduce the original bug at all,
// and its `AllowKittyKeyboardMode` profile setting is not exposed by the
// `Microsoft.Terminal.Wpf` control we embed, so this filter instead drops
// the entire Kitty flags push before it reaches the terminal control. The
// terminal's Kitty state then stays at its inactive default, matching how
// Yazi already behaves correctly against any terminal that does not
// advertise Kitty support (including Windows Terminal's own effective
// default) via its own protocol fallback. Everything else in the stream,
// including Kitty flags queries (`CSI ? u`) and pops (`CSI < u`), passes
// through unchanged.
internal sealed class KittyKeyboardProtocolFilter
{
    private const char Escape = '\x1b';
    private const int MaxPendingLength = 32;

    private readonly List<char> _pending = [];

    public void Process(ref Span<char> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        if (_pending.Count == 0 && data.IndexOf(Escape) < 0)
        {
            return;
        }

        var combined = new char[_pending.Count + data.Length];
        _pending.CopyTo(combined);
        data.CopyTo(combined.AsSpan(_pending.Count));
        _pending.Clear();

        var (rewritten, pendingTail) = Rewrite(combined);
        if (pendingTail.Length > 0)
        {
            _pending.AddRange(pendingTail);
        }

        data = rewritten;
    }

    private static (char[] Rewritten, char[] PendingTail) Rewrite(ReadOnlySpan<char> input)
    {
        var result = new List<char>(input.Length);
        var index = 0;
        while (index < input.Length)
        {
            var relativeEscapeIndex = input[index..].IndexOf(Escape);
            if (relativeEscapeIndex < 0)
            {
                result.AddRange(input[index..]);
                break;
            }

            var escapeIndex = index + relativeEscapeIndex;
            result.AddRange(input[index..escapeIndex]);
            var remaining = input[escapeIndex..];

            if (TryMatchKittyPush(remaining, out var consumed))
            {
                // Drop the push entirely; do not forward it to the terminal.
                index = escapeIndex + consumed;
                continue;
            }

            if (remaining.Length <= MaxPendingLength && IsPossiblePartialKittyPush(remaining))
            {
                return (result.ToArray(), remaining.ToArray());
            }

            result.Add(Escape);
            index = escapeIndex + 1;
        }

        return (result.ToArray(), []);
    }

    private static bool TryMatchKittyPush(ReadOnlySpan<char> input, out int consumed)
    {
        consumed = 0;
        if (input.Length < 4 || input[1] != '[' || input[2] != '>')
        {
            return false;
        }

        var digitsStart = 3;
        var i = digitsStart;
        while (i < input.Length && char.IsAsciiDigit(input[i]))
        {
            i++;
        }

        if (i >= input.Length || input[i] != 'u')
        {
            return false;
        }

        consumed = i + 1;
        return true;
    }

    private static bool IsPossiblePartialKittyPush(ReadOnlySpan<char> input)
    {
        if (input.Length < 2)
        {
            return true;
        }

        if (input[1] != '[')
        {
            return false;
        }

        if (input.Length < 3)
        {
            return true;
        }

        if (input[2] != '>')
        {
            return false;
        }

        for (var i = 3; i < input.Length; i++)
        {
            if (!char.IsAsciiDigit(input[i]))
            {
                return false;
            }
        }

        return true;
    }
}

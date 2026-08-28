using System.Globalization;

namespace YaziDesktopHost;

// Workaround for Issue #59: Microsoft.Terminal.Control's Kitty keyboard
// protocol handler resolves alternate keys via a function-local static
// `LoadKeyboardLayoutW(L"00000409", 0)` (en-US) the first time it is asked to
// report alternate keys. That call has the side effect of registering an
// unwanted en-US CTF/TSF input profile on Windows. Yazi requests this
// capability by pushing Kitty flags 29 (1|4|8|16) via `CSI > 29 u`. This
// filter strips only the "report alternate keys" bit (4) from that push so
// the layout-loading code path is never reached, while leaving Yazi's other
// three Kitty capabilities untouched.
internal sealed class KittyAlternateKeysFilter
{
    private const char Escape = '\x1b';
    private const int ReportAlternateKeysFlag = 4;
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

            if (TryMatchKittyPush(remaining, out var consumed, out var flags))
            {
                if ((flags & ReportAlternateKeysFlag) != 0)
                {
                    AppendKittyPush(result, flags & ~ReportAlternateKeysFlag);
                }
                else
                {
                    result.AddRange(remaining[..consumed]);
                }

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

    private static bool TryMatchKittyPush(ReadOnlySpan<char> input, out int consumed, out int flags)
    {
        consumed = 0;
        flags = 0;
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

        var digits = input[digitsStart..i];
        flags = digits.IsEmpty ? 0 : int.Parse(digits, CultureInfo.InvariantCulture);
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

    private static void AppendKittyPush(List<char> result, int flags)
    {
        result.Add(Escape);
        result.Add('[');
        result.Add('>');
        foreach (var digit in flags.ToString(CultureInfo.InvariantCulture))
        {
            result.Add(digit);
        }

        result.Add('u');
    }
}

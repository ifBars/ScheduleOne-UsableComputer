using System;
using System.Text.RegularExpressions;

namespace UsableComputer.Apps.AppStudio;

internal static class StudioSource
{
    private static readonly Regex Location = new(@"(?:\((\d+),\d+(?:-\d+)?\)|:(\d+):)", RegexOptions.CultureInvariant);

    internal static int DiagnosticLine(string message)
    {
        Match match = Location.Match(message);
        string value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return int.TryParse(value, out int line) && line > 0 ? line : 0;
    }

    internal static string DescribeDiagnostic(string message)
    {
        Match match = Location.Match(message);
        int line = DiagnosticLine(message);
        return line == 0 ? message : $"Line {line}: {message.Substring(match.Index + match.Length).TrimStart(':', ' ')}";
    }

    internal static int LineStart(string source, int line)
    {
        if (line <= 1) return 0;
        int current = 1;
        for (int index = 0; index < source.Length; index++)
            if (source[index] == '\n' && ++current == line) return index + 1;
        return source.Length;
    }

    internal static int LineAt(string source, int offset)
    {
        int line = 1;
        for (int index = 0; index < Math.Min(offset, source.Length); index++)
            if (source[index] == '\n') line++;
        return line;
    }
}

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kemibrug.AI.Assistant
{
    /// <summary>
    /// Provides utility methods for sanitizing strings into safe C# identifiers.
    /// </summary>
    public static class CodeNamingSanitizer
    {
        public static string ToSafeClassName(string input)
        {
            var s = Regex.Replace(input ?? "Generated", @"[^\p{L}\p{Nd}]+", " ").Trim();
            s = string.Join("", s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Cap));
            if (!Regex.IsMatch(s, @"^\p{L}")) s = "C" + s;
            if (!s.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)) s += "Tests";
            return s;
        }

        public static string ToSafeMethodName(string input)
        {
            var s = Regex.Replace((input ?? "Scenario").Trim(), @"[^\p{L}\p{Nd}]+", " ").Trim();
            if (string.IsNullOrWhiteSpace(s)) return "Scenario_Default_BehavesAsExpected";
            var joined = string.Join("", s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Cap));
            if (!Regex.IsMatch(joined, @"^\p{L}")) joined = "Case" + joined;
            if (!Regex.IsMatch(joined, "(Should|Returns|Throws|Displays|Creates|Updates|Deletes|Succeeds|Fails)"))
                joined += "_BehavesAsExpected";
            return joined.Length > 128 ? joined[..128] : joined;
        }

        private static string Cap(string w) =>
            string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "");
    }
}
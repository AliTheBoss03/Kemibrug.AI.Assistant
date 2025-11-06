using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kemibrug.AI.Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    internal static class TddHelpers
    {
        public static (string endpoint, string key, string deployment) GetOpenAIConfig()
        {
            var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint") ?? "";
            var key = Environment.GetEnvironmentVariable("AzureOpenAIApiKey") ?? "";
            var deployment = Environment.GetEnvironmentVariable("AzureOpenAI_TDD_Model") ?? "gpt-4o";
            return (endpoint, key, deployment);
        }

        public static AnalyzedUserStory ParseAnalyzed(string json, ILogger log)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var className = root.TryGetProperty("className", out var cn) ? cn.GetString() ?? "" : "";
                var methods = new List<string>();
                if (root.TryGetProperty("testMethods", out var tm) && tm.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in tm.EnumerateArray())
                    {
                        var s = el.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(s)) methods.Add(ToSafeMethodName(s));
                    }
                }

                return new AnalyzedUserStory
                {
                    ClassName = ToSafeClassName(string.IsNullOrWhiteSpace(className) ? "GeneratedTests" : className),
                    TestMethods = methods.Distinct().ToList()
                };
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "ParseAnalyzed: invalid JSON.");
                return new AnalyzedUserStory { ClassName = "GeneratedTests", TestMethods = new List<string>() };
            }
        }

        public static (string fileName, string fileContent) ParseFileResult(string json, ILogger log)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var fn = root.TryGetProperty("fileName", out var f) ? (f.GetString() ?? "") : "";
                var fc = root.TryGetProperty("fileContent", out var c) ? (c.GetString() ?? "") : "";
                return (string.IsNullOrWhiteSpace(fn) ? "GeneratedTests.cs" : fn, fc);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "ParseFileResult: invalid JSON.");
                return ("GeneratedTests.cs", "");
            }
        }

        public static AnalyzedUserStory LocalHeuristicAnalyze(UserStoryInput us)
        {
            var baseName = string.IsNullOrWhiteSpace(us.Title) ? "Feature" : us.Title!;
            var className = ToSafeClassName(baseName + "Tests");

            var methods = (us.AcceptanceCriteria ?? new List<string> { "Happy path works", "Invalid input handled" })
                .Select(ToSafeMethodName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .DefaultIfEmpty("Scenario_Default_BehavesAsExpected")
                .ToList();

            return new AnalyzedUserStory { ClassName = className, TestMethods = methods };
        }

        public static string BuildCSharpTestSkeleton(string className, IEnumerable<string> methods)
        {
            var safeClass = ToSafeClassName(className);
            var ms = (methods ?? Array.Empty<string>()).Select(ToSafeMethodName).Distinct().ToList();
            if (ms.Count == 0) ms.Add("Scenario_Default_BehavesAsExpected");

            var sb = new StringBuilder();
            sb.AppendLine("using Xunit;");
            sb.AppendLine();
            sb.AppendLine("namespace Kemibrug.Tests");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {safeClass}");
            sb.AppendLine("    {");
            foreach (var m in ms)
            {
                sb.AppendLine("        [Fact]");
                sb.AppendLine($"        public void {m}()");
                sb.AppendLine("        {");
                sb.AppendLine("            // Arrange");
                sb.AppendLine();
                sb.AppendLine("            // Act");
                sb.AppendLine();
                sb.AppendLine("            // Assert");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

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

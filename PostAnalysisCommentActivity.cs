using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Kemibrug.AI.Assistant.Models.PR_Models;


namespace Kemibrug.AI.Assistant
{
    public static class PostAnalysisCommentActivity
    {
        private static readonly HttpClient _http = new HttpClient();

        [Function("PostAnalysisCommentActivity")]
        public static async Task<string> Run([ActivityTrigger] PostAnalysisCommentInput input, FunctionContext ctx)
        {
            var log = ctx.GetLogger("PostAnalysisCommentActivity");

            // Påkrævede settings
            var orgUrl = (Environment.GetEnvironmentVariable("AzureDevOpsOrgUrl") ?? "").TrimEnd('/');
            var project = Environment.GetEnvironmentVariable("AzureDevOpsProject");
            var repoId = Environment.GetEnvironmentVariable("AzureDevOpsRepositoryId");     // foretrukket
            var repoName = Environment.GetEnvironmentVariable("AzureDevOpsRepositoryName");   // fallback
            var pat = Environment.GetEnvironmentVariable("AzureDevOpsPAT");

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(pat))
            {
                log.LogError("Missing ADO config. Need AzureDevOpsOrgUrl, AzureDevOpsProject, AzureDevOpsPAT.");
                return "{\"error\":\"ADO configuration missing\"}";
            }

            try
            {
                // 1) Parse AI-resultat
                var result = SafeParse(input.AnalysisJson, log);

                // 2) Find repositoryId hvis kun navn er sat
                if (string.IsNullOrWhiteSpace(repoId))
                {
                    if (string.IsNullOrWhiteSpace(repoName))
                    {
                        log.LogError("Set either AzureDevOpsRepositoryId or AzureDevOpsRepositoryName.");
                        return "{\"error\":\"Repository not configured\"}";
                    }
                    repoId = await ResolveRepositoryIdAsync(orgUrl, project, repoName, pat, log);
                    if (string.IsNullOrWhiteSpace(repoId))
                        return "{\"error\":\"Repository not found\"}";
                }

                // 3) Build markdown
                var markdown = BuildMarkdown(result);

                // 4) Post kommentar
                var api = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{repoId}/pullRequests/{input.PullRequestId}/threads?api-version=7.1-preview.1";
                var payload = new
                {
                    comments = new[] { new { content = markdown, commentType = 1 } },
                    status = 1
                };
                var reqJson = JsonSerializer.Serialize(payload);

                SetAuthHeaders(pat);
                using var req = new HttpRequestMessage(HttpMethod.Post, api)
                { Content = new StringContent(reqJson, Encoding.UTF8, "application/json") };

                var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    log.LogError("ADO POST failed {Status}: {Body}", (int)resp.StatusCode, body);
                    return $"{{\"error\":\"ADO POST failed ({(int)resp.StatusCode})\"}}";
                }

                log.LogInformation("Comment posted to PR #{PR}.", input.PullRequestId);
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error posting PR comment");
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        private static void SetAuthHeaders(string pat)
        {
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")); // blank user
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static async Task<string?> ResolveRepositoryIdAsync(string orgUrl, string project, string repoName, string pat, ILogger log)
        {
            SetAuthHeaders(pat);
            var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version=7.1";
            var resp = await _http.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                log.LogError("Failed to list repos ({Status}): {Body}", (int)resp.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            foreach (var r in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var name = r.GetProperty("name").GetString();
                if (string.Equals(name, repoName, StringComparison.OrdinalIgnoreCase))
                    return r.GetProperty("id").GetString();
            }

            log.LogError("Repository '{RepoName}' not found in project '{Project}'.", repoName, project);
            return null;
        }

        private static AnalysisResult SafeParse(string json, ILogger log)
        {
            try
            {
                var r = JsonSerializer.Deserialize<AnalysisResult>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return r ?? new AnalysisResult { ViolationFound = null, Explanation = "Empty model response." };
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Invalid JSON from model.");
                return new AnalysisResult { ViolationFound = null, Explanation = "Model response was not valid JSON." };
            }
        }

        private static string BuildMarkdown(AnalysisResult r)
        {
            var status = r.ViolationFound == true ? "❌ **Arkitektur-brud fundet**:"
                       : r.ViolationFound == false ? "✅ **Ingen arkitektur-brud fundet**."
                       : "⚠️ **Analyse utilgængelig** (ugyldigt JSON-svar).";

            var sb = new StringBuilder();
            sb.AppendLine(status);

            if (!string.IsNullOrWhiteSpace(r.Explanation))
                sb.AppendLine($"\n_{Escape(r.Explanation)}_");

            if (r.Violations is { Count: > 0 })
            {
                sb.AppendLine("\n**Detaljer:**");
                int i = 0;
                foreach (var v in r.Violations.Take(10))
                {
                    i++;
                    var lines = (v.Lines != null && v.Lines.Count > 0) ? string.Join(", ", v.Lines) : "—";
                    var sev = string.IsNullOrWhiteSpace(v.Severity) ? "medium" : v.Severity!;
                    sb.AppendLine(
                        $"- **[{i}] {Escape(v.Rule ?? "Rule")}** · _{Escape(v.Principle ?? "Principle")}_ · **{sev.ToUpperInvariant()}** · linjer: {lines}\n" +
                        $"  - **Evidens:** {Escape(v.Evidence ?? "")}\n" +
                        $"  - **Forslag:** {Escape(v.Suggestion ?? "")}");
                }
                if (r.Violations.Count > 10)
                    sb.AppendLine($"\n… _{r.Violations.Count - 10} flere skjult._");
            }

            sb.AppendLine("\n— _Kemibrug AI-assistent_");
            return sb.ToString();
        }

        private static string Escape(string s) =>
            (s ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");
    }
}

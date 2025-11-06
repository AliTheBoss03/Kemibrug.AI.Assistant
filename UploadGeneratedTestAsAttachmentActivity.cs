using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kemibrug.AI.Assistant.Models;

namespace Kemibrug.AI.Assistant
{
    public class GeneratedTestUploadInput
    {
        public int UserStoryId { get; set; }
        public string FileName { get; set; } = "GeneratedTests.cs";
        public string FileContent { get; set; } = "";
        public string? Comment { get; set; } = "Auto-generated TDD test skeleton";
    }

    public static class UploadGeneratedTestAsAttachmentActivity
    {
        private static readonly HttpClient _http = new HttpClient();

        [Function("UploadGeneratedTestAsAttachmentActivity")]
        public static async Task<string> Run(
            [ActivityTrigger] GeneratedTestUploadInput input,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("UploadGeneratedTestAsAttachmentActivity");

            var pat = Environment.GetEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = Environment.GetEnvironmentVariable("AzureDevOpsOrgUrl");
            var projectName = "AI-assisted%20Quality%20Assurance%20and%20Test%20Automation";

            if (string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(orgUrl))
            {
                log.LogError("Azure DevOps configuration missing (PAT or OrgUrl).");
                return "{\"error\":\"Azure DevOps configuration missing.\"}";
            }

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var fileName = EnsureCsExtension(SanitizeFileName(input.FileName));
            var contentBytes = Encoding.UTF8.GetBytes(input.FileContent);

            try
            {
                var attachUrl =
                    $"{orgUrl}/{projectName}/_apis/wit/attachments?fileName={Uri.EscapeDataString(fileName)}&api-version=7.1-preview.3";

                using var raw = new ByteArrayContent(contentBytes);
                raw.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var uploadResp = await _http.PostAsync(attachUrl, raw);
                var uploadBody = await uploadResp.Content.ReadAsStringAsync();
                if (!uploadResp.IsSuccessStatusCode)
                {
                    log.LogError("Attachment upload failed ({Status}): {Body}", uploadResp.StatusCode, uploadBody);
                    return $"{{\"error\":\"Upload failed: {(int)uploadResp.StatusCode}\",\"body\":{JsonSerializer.Serialize(uploadBody)}}}";
                }

                var doc = JsonDocument.Parse(uploadBody);
                if (!doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    log.LogError("Attachment upload response missing 'url'. Body: {Body}", uploadBody);
                    return "{\"error\":\"Attachment upload missing url.\"}";
                }
                var attachmentUrl = urlProp.GetString()!;

                var wiPatchUrl =
                    $"{orgUrl}/{projectName}/_apis/wit/workitems/{input.UserStoryId}?api-version=7.1";

                var patchOps = new[]
                {
                    new
                    {
                        op = "add",
                        path = "/relations/-",
                        value = new
                        {
                            rel = "AttachedFile",
                            url = attachmentUrl,
                            attributes = new { comment = input.Comment ?? "Auto-generated TDD test skeleton" }
                        }
                    }
                };

                var patchJson = JsonSerializer.Serialize(patchOps);
                using var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), wiPatchUrl);
                patchReq.Content = new StringContent(patchJson, Encoding.UTF8, "application/json-patch+json");

                var patchResp = await _http.SendAsync(patchReq);
                var patchBody = await patchResp.Content.ReadAsStringAsync();
                if (!patchResp.IsSuccessStatusCode)
                {
                    log.LogError("Work item PATCH failed ({Status}): {Body}", patchResp.StatusCode, patchBody);
                    return $"{{\"error\":\"Work item PATCH failed: {(int)patchResp.StatusCode}\",\"body\":{JsonSerializer.Serialize(patchBody)}}}";
                }

                log.LogInformation("Attached {File} to Work Item #{Id}", fileName, input.UserStoryId);
                return $"{{\"ok\":true,\"file\":\"{fileName}\",\"workItemId\":{input.UserStoryId}}}";
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed uploading/attaching generated test.");
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "GeneratedTests.cs";
            var clean = Regex.Replace(name, @"[^\w\-. ]", "_");
            return clean.Trim();
        }

        private static string EnsureCsExtension(string name)
        {
            return name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs";
        }
    }
}

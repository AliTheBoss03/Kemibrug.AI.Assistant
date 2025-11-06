using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using Kemibrug.AI.Assistant.Models;
using Kemibrug.AI.Assistant.TDD_Kickstarter.TDD_Models;

namespace Kemibrug.AI.Assistant
{
    public class UploadGeneratedTestAsAttachmentActivity
    {
        private readonly ILogger<UploadGeneratedTestAsAttachmentActivity> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public UploadGeneratedTestAsAttachmentActivity(ILogger<UploadGeneratedTestAsAttachmentActivity> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [Function(nameof(UploadGeneratedTestAsAttachmentActivity))]
        public async Task<string> Run([ActivityTrigger] GeneratedTestUploadInput input)
        {
            var pat = GetRequiredEnvironmentVariable("AzureDevOpsPAT");
            var orgUrl = GetRequiredEnvironmentVariable("AzureDevOpsOrgUrl");
            var projectName = GetRequiredEnvironmentVariable("AzureDevOpsProjectName");

            var client = _httpClientFactory.CreateClient();
            var authorizationHeader = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var fileName = EnsureCsExtension(SanitizeFileName(input.FileName));

            try
            {
                _logger.LogInformation("Uploading file '{fileName}'...", fileName);
                var attachmentUrl = await UploadAttachment(client, orgUrl, projectName, fileName, input.FileContent, authorizationHeader);
                _logger.LogInformation("File uploaded successfully. URL: {attachmentUrl}", attachmentUrl);

                _logger.LogInformation("Attaching file to Work Item #{workItemId}...", input.UserStoryId);
                await LinkAttachmentToWorkItem(client, orgUrl, projectName, input.UserStoryId, attachmentUrl, input.Comment, authorizationHeader);

                var successMessage = $"Successfully attached {fileName} to Work Item #{input.UserStoryId}.";
                _logger.LogInformation(successMessage);
                return successMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload and attach file for Work Item #{workItemId}.", input.UserStoryId);
                throw;
            }
        }

        private async Task<string> UploadAttachment(HttpClient client, string orgUrl, string projectName, string fileName, string fileContent, AuthenticationHeaderValue authHeader)
        {
            var requestUrl = $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/wit/attachments?fileName={Uri.EscapeDataString(fileName)}&api-version=7.1-preview.3";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = authHeader;
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("url", out var urlProp) || urlProp.GetString() is null)
            {
                throw new InvalidOperationException("Could not find 'url' property in the attachment upload response.");
            }
            return urlProp.GetString()!;
        }

        private async Task LinkAttachmentToWorkItem(HttpClient client, string orgUrl, string projectName, int workItemId, string attachmentUrl, string comment, AuthenticationHeaderValue authHeader)
        {
            var requestUrl = $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/wit/workitems/{workItemId}?api-version=7.1";

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
                        attributes = new { comment = comment ?? "Auto-generated TDD test skeleton" }
                    }
                }
            };

            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUrl);
            request.Headers.Authorization = authHeader;
            request.Content = new StringContent(JsonSerializer.Serialize(patchOps), Encoding.UTF8, "application/json-patch+json");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private static string GetRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Configuration error: Environment variable '{name}' is not set.");
            }
            return value;
        }

        private static string SanitizeFileName(string name) => string.IsNullOrWhiteSpace(name) ? "GeneratedTests" : Regex.Replace(name, @"[^\w\-. ]", "_").Trim();
        private static string EnsureCsExtension(string name) => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs";
    }
}
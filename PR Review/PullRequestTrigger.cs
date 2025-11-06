using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
// The correct attribute is in this namespace
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.Models.AzureDevOps;
using Kemibrug.AI.Assistant.Models.PR_Review;

namespace Kemibrug.AI.Assistant
{
    public class PullRequestTrigger
    {
        private readonly ILogger<PullRequestTrigger> _logger;

        public PullRequestTrigger(ILogger<PullRequestTrigger> logger)
        {
            _logger = logger;
        }

        [Function("PullRequestTrigger")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("Pull Request trigger received a webhook.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            PullRequestWebhookPayload? webhookData;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                webhookData = JsonSerializer.Deserialize<PullRequestWebhookPayload>(requestBody, options);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize webhook payload. Body: {requestBody}", requestBody);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (webhookData?.Resource?.PullRequestId is null or 0 ||
                webhookData.Resource.Repository is null ||
                string.IsNullOrEmpty(webhookData.Resource.Repository.Id) ||
                webhookData.Resource.Repository.Project is null ||
                string.IsNullOrEmpty(webhookData.Resource.Repository.Project.Id))
            {
                _logger.LogError("Webhook payload was malformed or missing required data (PullRequestId, Repository, Project).");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var prId = webhookData.Resource.PullRequestId;
            _logger.LogInformation("Received valid webhook for Pull Request ID: {prId}", prId);

            var orchestrationInput = new PullRequestOrchestratorInput
            {
                PullRequestId = prId,
                RepositoryId = webhookData.Resource.Repository.Id,
                ProjectId = webhookData.Resource.Repository.Project.Id,
                SourceRefName = webhookData.Resource.SourceRefName
            };

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(PullRequestOrchestrator),
                orchestrationInput);

            _logger.LogInformation("Started orchestration for PR {prId} with instance ID = '{instanceId}'.", prId, instanceId);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
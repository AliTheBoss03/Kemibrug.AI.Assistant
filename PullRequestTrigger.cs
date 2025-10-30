using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Kemibrug.AI.Assistant.Models;

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
            _logger.LogInformation("C# HTTP trigger function received a new pull request webhook.");

            // Læs den indkommende JSON-data fra Azure DevOps
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var webhookData = JsonSerializer.Deserialize<PullRequestWebhookPayload>(
                requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Tjek om vi fik de nødvendige data
            if (webhookData?.Resource == null || webhookData.Resource.PullRequestId == 0)
            {
                _logger.LogError("Webhook payload was malformed or did not contain a pullRequestId.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            _logger.LogInformation("Received data for Pull Request ID: {prId}", webhookData.Resource.PullRequestId);

            // Start orkestreringen og send det fulde 'Resource'-objekt med
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "PullRequestOrchestrator",
                webhookData.Resource); // Vi sender nu et rigtigt objekt, ikke bare en tekststreng

            _logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
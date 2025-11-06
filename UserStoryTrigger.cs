using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kemibrug.AI.Assistant
{
    public class UserStoryTrigger
    {
        private sealed class IdOnly { public int Id { get; set; } }
        private sealed class ServiceHookPayload
        {
            public ResourceObj? Resource { get; set; }
            public sealed class ResourceObj
            {
                [JsonPropertyName("workItemId")] public int? WorkItemId { get; set; }
                [JsonPropertyName("id")] public int? Id { get; set; }
            }
        }

        [Function("UserStoryTrigger")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("UserStoryTrigger");
            var body = await new StreamReader(req.Body).ReadToEndAsync();

            int extractedId = 0;

            try
            {
                var asId = JsonSerializer.Deserialize<IdOnly>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (asId?.Id > 0) extractedId = asId.Id;
            }
            catch {  }

            if (extractedId == 0)
            {
                try
                {
                    var hook = JsonSerializer.Deserialize<ServiceHookPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    extractedId = hook?.Resource?.WorkItemId ?? hook?.Resource?.Id ?? 0;
                }
                catch { }
            }

            if (extractedId <= 0)
            {
                log.LogError("Could not extract User Story ID from request body.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("TddKickstarterOrchestrator", extractedId);
            log.LogInformation("Started TDD orchestration for User Story ID {id}. Instance = {instanceId}", extractedId, instanceId);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}

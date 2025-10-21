using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Kemibrug.AI.Assistant.Models;

namespace Kemibrug.AI.Assistant
{
    public class UserStoryTrigger
    {
        [Function("UserStoryTrigger")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("UserStoryTrigger");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonSerializer.Deserialize<IdInput>(requestBody);

            if (input?.Id == 0) return req.CreateResponse(HttpStatusCode.BadRequest);

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "TddKickstarterOrchestrator", input.Id);

            logger.LogInformation("Started TDD orchestration for User Story ID {id} with instance ID = '{instanceId}'.", input.Id, instanceId);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
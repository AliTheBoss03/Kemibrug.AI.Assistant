using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public static class PullRequestOrchestrator
    {
        [Function("PullRequestOrchestrator")]
        public static async Task<string> RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger("PullRequestOrchestrator");
            var pullRequest = context.GetInput<Resource>();
            logger.LogInformation("Orchestration started for PR ID: {prId}", pullRequest.PullRequestId);

            logger.LogInformation("Step 1: Fetching file changes from DevOps.");
            var apiResponse = await context.CallActivityAsync<string>(
                "GetPullRequestChangesActivity",
                pullRequest);

            logger.LogInformation("API Response from DevOps: {response}", apiResponse);


            logger.LogInformation("Step 2: Calling AnalyzeCodeActivity.");
            var analysisResultJson = await context.CallActivityAsync<string>(
                "AnalyzeCodeActivity",
                apiResponse);

            logger.LogInformation("Analysis result: {result}", analysisResultJson);


            logger.LogInformation("Orchestration for PR {prId} completed.", pullRequest.PullRequestId);
            return "Orchestration completed successfully!";
        }
    }
}
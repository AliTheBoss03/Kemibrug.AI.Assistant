using Kemibrug.AI.Assistant.Models;
using Kemibrug.AI.Assistant.Models.PR_Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public static class PullRequestOrchestrator
    {
        [Function("PullRequestOrchestrator")]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger("PullRequestOrchestrator");

            // Input fra triggeren (Resource)
            var pullRequest = context.GetInput<Resource>();
            if (pullRequest is null)
                throw new System.InvalidOperationException("Missing orchestrator input (Resource).");

            var prId = pullRequest.PullRequestId;
            logger.LogInformation("Orchestration started for PR ID: {prId}", prId);

            // 1) Hent ændringer/kode
            logger.LogInformation("Step 1: Fetching file changes from DevOps.");
            var apiResponse = await context.CallActivityAsync<string>(
                "GetPullRequestChangesActivity",
                pullRequest);

            logger.LogInformation("API Response from DevOps received (length: {len})", apiResponse?.Length ?? 0);

            // 2) Kør AI-analysen
            logger.LogInformation("Step 2: Calling AnalyzeCodeActivity.");
            var analysisResultJson = await context.CallActivityAsync<string>(
                "AnalyzeCodeActivity",
                apiResponse);

            logger.LogInformation("Analysis result received (length: {len})", analysisResultJson?.Length ?? 0);

            // 3) Post PR-kommentar
            var postIn = new PostAnalysisCommentInput
            {
                PullRequestId = prId,
                AnalysisJson = analysisResultJson
            };

            await context.CallActivityAsync<string>("PostAnalysisCommentActivity", postIn);

            logger.LogInformation("Orchestration for PR {prId} completed.", prId);
            return "Orchestration completed successfully!";
        }
    }
}

using Kemibrug.AI.Assistant.Models.PR_Review;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Kemibrug.AI.Assistant.PR_Review;

namespace Kemibrug.AI.Assistant
{
    public static class PullRequestOrchestrator
    {
        [Function(nameof(PullRequestOrchestrator))]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger(nameof(PullRequestOrchestrator));

            var input = context.GetInput<PullRequestOrchestratorInput>();
            if (input is null)
            {
                throw new InvalidOperationException("Orchestrator received null input.");
            }

            logger.LogInformation("Orchestration started for PR ID: {prId}", input.PullRequestId);

            try
            {
                var layer = await context.CallActivityAsync<string>(
                    nameof(DeterminePrLayerActivity),
                    input.SourceRefName);

                if (string.IsNullOrEmpty(layer))
                {
                    logger.LogWarning("Could not determine a target layer for PR {prId}. Ending orchestration.", input.PullRequestId);
                    return "Orchestration complete: No target layer found.";
                }

                logger.LogInformation("Step 1: Fetching file changes from DevOps for layer '{layer}'.", layer);
                var codeChanges = await context.CallActivityAsync<string>(
                    nameof(GetPullRequestChangesActivity),
                    input);

                if (string.IsNullOrWhiteSpace(codeChanges))
                {
                    logger.LogInformation("No relevant C# file changes found for PR {prId}. Orchestration ending.", input.PullRequestId);
                    return "Orchestration complete: No changes to analyze.";
                }

                logger.LogInformation("Step 2: Analyzing code changes.");
                var analysisResultJson = await context.CallActivityAsync<string>(
                    nameof(AnalyzeCodeActivity),
                    codeChanges);

                logger.LogInformation("Step 3: Posting analysis results to PR.");
                var postCommentInput = new PostAnalysisCommentInput
                {
                    PullRequestId = input.PullRequestId,
                    RepositoryId = input.RepositoryId,
                    ProjectId = input.ProjectId,
                    AnalysisJson = analysisResultJson
                };

                await context.CallActivityAsync(nameof(PostAnalysisCommentActivity), postCommentInput);

                logger.LogInformation("Orchestration for PR {prId} completed successfully.", input.PullRequestId);
                return "Orchestration completed successfully!";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Orchestration for PR {prId} failed. Error: {message}", input.PullRequestId, ex.Message);
                throw;
            }
        }
    }
}
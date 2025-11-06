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
                logger.LogInformation("Step 1: Fetching file changes from DevOps.");
                var codeChanges = await context.CallActivityAsync<string>(
                    nameof(GetPullRequestChangesActivity),
                    input);

                if (string.IsNullOrWhiteSpace(codeChanges))
                {
                    logger.LogInformation("No relevant C# file changes found. Orchestration ending.");
                    return "Orchestration complete: No changes to analyze.";
                }

                logger.LogInformation("Step 2: Inferring layer from file paths.");
                var inferredLayer = await context.CallActivityAsync<string>(
                    nameof(InferLayerFromChangesActivity),
                    codeChanges);

                logger.LogInformation("Step 3: Analyzing code changes for layer '{layer}'.", inferredLayer);
                var analysisInput = new AnalysisInput
                {
                    CodeToAnalyze = codeChanges,
                    InferredLayer = inferredLayer
                };
                var analysisResultJson = await context.CallActivityAsync<string>(
                    nameof(AnalyzeCodeActivity),
                    analysisInput);

                logger.LogInformation("Step 4: Posting analysis results to PR.");
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
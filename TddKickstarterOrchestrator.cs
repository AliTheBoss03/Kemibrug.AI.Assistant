using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Kemibrug.AI.Assistant.Models;

namespace Kemibrug.AI.Assistant
{
    public static class TddKickstarterOrchestrator
    {
        [Function("TddKickstarterOrchestrator")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger("TddKickstarterOrchestrator");
            var userStoryId = context.GetInput<int>();

            logger.LogInformation("Starting orchestration for User Story ID: {id}", userStoryId);

            var userStory = await context.CallActivityAsync<UserStoryInput>(
                "GetUserStoryActivity", userStoryId);

            logger.LogInformation("Successfully fetched User Story: {title}", userStory.Title);

            var analyzedStory = await context.CallActivityAsync<AnalyzedUserStory>(
                "AnalyzeUserStoryActivity", userStory);

            var generatedCode = await context.CallActivityAsync<string>(
                "GenerateTestSkeletonActivity", analyzedStory);

            logger.LogInformation("Generated Test Skeleton:\n{code}", generatedCode);

            return "TDD Kickstarter orchestration completed.";
        }
    }
}
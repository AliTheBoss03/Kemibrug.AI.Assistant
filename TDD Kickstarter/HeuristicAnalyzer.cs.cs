using Kemibrug.AI.Assistant.Models;
using System.Collections.Generic;
using System.Linq;

namespace Kemibrug.AI.Assistant
{
    public interface IHeuristicAnalyzer
    {
        AnalyzedUserStory Analyze(UserStoryInput userStory);
    }

    public class HeuristicAnalyzer : IHeuristicAnalyzer
    {
        public AnalyzedUserStory Analyze(UserStoryInput us)
        {
            var baseName = string.IsNullOrWhiteSpace(us.Title) ? "Feature" : us.Title!;
            var className = CodeNamingSanitizer.ToSafeClassName(baseName);

            var methods = (us.AcceptanceCriteria ?? new List<string> { "Happy path works", "Invalid input handled" })
                .Select(CodeNamingSanitizer.ToSafeMethodName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .DefaultIfEmpty("Scenario_Default_BehavesAsExpected")
                .ToList();

            return new AnalyzedUserStory { ClassName = className, TestMethods = methods };
        }
    }
}
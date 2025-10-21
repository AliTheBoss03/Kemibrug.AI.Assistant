using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Kemibrug.AI.Assistant.Models;
using System.Collections.Generic;
using System.Text;

namespace Kemibrug.AI.Assistant
{
    public static class TddActivities
    {
        [Function("AnalyzeUserStoryActivity")]
        public static AnalyzedUserStory AnalyzeUserStory([ActivityTrigger] UserStoryInput userStory, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("AnalyzeUserStoryActivity");
            logger.LogInformation("Simulating AI analysis for User Story: {title}", userStory.Title);

            return new AnalyzedUserStory
            {
                ClassName = "ProductServiceTests",
                TestMethods = new List<string> { "AddProduct_WithValidData_ReturnsSuccess", "GetProduct_WithInvalidId_ThrowsException" }
            };
        }
        [Function("GenerateTestSkeletonActivity")]
        public static string GenerateTestSkeleton([ActivityTrigger] AnalyzedUserStory analyzedStory, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GenerateTestSkeletonActivity");
            logger.LogInformation("Simulating C# code generation for class: {className}", analyzedStory.ClassName);

            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("using Xunit;");
            codeBuilder.AppendLine("");
            codeBuilder.AppendLine($"namespace Kemibrug.Tests");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    public class {analyzedStory.ClassName}");
            codeBuilder.AppendLine("    {");

            foreach (var methodName in analyzedStory.TestMethods)
            {
                codeBuilder.AppendLine("        [Fact]");
                codeBuilder.AppendLine($"        public void {methodName}()");
                codeBuilder.AppendLine("        {");
                codeBuilder.AppendLine("            // Arrange");
                codeBuilder.AppendLine("");
                codeBuilder.AppendLine("            // Act");
                codeBuilder.AppendLine("");
                codeBuilder.AppendLine("            // Assert");
                codeBuilder.AppendLine("        }");
                codeBuilder.AppendLine("");
            }

            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("}");

            return codeBuilder.ToString();
        }
    }
}
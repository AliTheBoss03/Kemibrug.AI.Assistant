using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kemibrug.AI.Assistant
{
    public interface ITestSkeletonBuilder
    {
        string Build(string className, IEnumerable<string> methods);
    }

    public class CSharpTestSkeletonBuilder : ITestSkeletonBuilder
    {
        public string Build(string className, IEnumerable<string> methods)
        {
            var safeClass = CodeNamingSanitizer.ToSafeClassName(className);
            var ms = (methods ?? Array.Empty<string>()).Select(CodeNamingSanitizer.ToSafeMethodName).Distinct().ToList();
            if (ms.Count == 0) ms.Add("Scenario_Default_BehavesAsExpected");

            var sb = new StringBuilder();
            sb.AppendLine("using Xunit;");
            sb.AppendLine();
            sb.AppendLine("namespace Kemibrug.Tests");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {safeClass}");
            sb.AppendLine("    {");
            foreach (var m in ms)
            {
                sb.AppendLine();
                sb.AppendLine("        [Fact]");
                sb.AppendLine($"        public void {m}()");
                sb.AppendLine("        {");
                sb.AppendLine("            // Arrange");
                sb.AppendLine();
                sb.AppendLine("            // Act");
                sb.AppendLine();
                sb.AppendLine("            // Assert");
                sb.AppendLine("            throw new NotImplementedException(\"Test not implemented.\");");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
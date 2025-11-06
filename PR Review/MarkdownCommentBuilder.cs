using Kemibrug.AI.Assistant.Models.PR_Models;
using Kemibrug.AI.Assistant.Models.PR_Review;
using System.Linq;
using System.Text;

namespace Kemibrug.AI.Assistant.PR_Review
{
    public interface IMarkdownCommentBuilder
    {
        string Build(AnalysisResult result);
    }

    public class MarkdownCommentBuilder : IMarkdownCommentBuilder
    {
        public string Build(AnalysisResult r)
        {
            var status = r.ViolationFound == true ? "❌ **Architecture Violations Found**:"
                       : r.ViolationFound == false ? "✅ **No Architecture Violations Found**."
                       : "⚠️ **Analysis Inconclusive** (Invalid response from AI model).";

            var sb = new StringBuilder();
            sb.AppendLine(status);

            if (!string.IsNullOrWhiteSpace(r.Explanation))
            {
                sb.AppendLine($"\n_{Escape(r.Explanation)}_");
            }

            if (r.Violations is { Count: > 0 })
            {
                sb.AppendLine("\n**Details:**");
                int i = 0;
                foreach (var v in r.Violations.Take(10))
                {
                    i++;
                    var lines = (v.Lines != null && v.Lines.Count > 0) ? string.Join(", ", v.Lines) : "N/A";
                    var sev = string.IsNullOrWhiteSpace(v.Severity) ? "medium" : v.Severity!;
                    sb.AppendLine(
                        $"- **[{i}] {Escape(v.Rule ?? "Rule")}** · _{Escape(v.Principle ?? "Principle")}_ · **{sev.ToUpperInvariant()}** · Lines: {lines}\n" +
                        $"  - **Evidence:** {Escape(v.Evidence ?? "")}\n" +
                        $"  - **Suggestion:** {Escape(v.Suggestion ?? "")}");
                }
                if (r.Violations.Count > 10)
                {
                    sb.AppendLine($"\n... _{r.Violations.Count - 10} more violations were found but are not shown._");
                }
            }

            sb.AppendLine("\n---");
            sb.AppendLine("_Reviewed by Kemibrug AI Assistant_");
            return sb.ToString();
        }

        private static string Escape(string s) => (s ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
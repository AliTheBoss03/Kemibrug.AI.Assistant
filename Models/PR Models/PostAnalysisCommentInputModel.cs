using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models.PR_Models
{
    public sealed class PostAnalysisCommentInput
    {
        public int PullRequestId { get; init; }
        public string AnalysisJson { get; init; } = "{}";
    }
}

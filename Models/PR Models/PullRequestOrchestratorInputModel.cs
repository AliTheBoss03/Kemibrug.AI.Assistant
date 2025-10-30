using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models.PR_Models
{
    public sealed class PullRequestOrchestratorInput
    {
        public int PullRequestId { get; init; }
        public string? RepositoryId { get; init; }
        public string? Layer { get; init; }
    }
}

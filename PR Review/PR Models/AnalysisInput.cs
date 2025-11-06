namespace Kemibrug.AI.Assistant.Models.PR_Review
{
    /// <summary>
    /// Input for the AnalyzeCodeActivity, containing both the code to analyze
    /// and the architectural layer it belongs to.
    /// </summary>
    public sealed class AnalysisInput
    {
        public string CodeToAnalyze { get; init; } = string.Empty;
        public string InferredLayer { get; init; } = string.Empty;
    }
}
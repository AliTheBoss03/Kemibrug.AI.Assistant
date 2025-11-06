using System;

namespace Kemibrug.AI.Assistant.PR_Review
{
    public interface ILayerInferenceService
    {
        string InferLayer(string combinedCode);
    }

    public class LayerInferenceService : ILayerInferenceService
    {
        public string InferLayer(string combinedCode)
        {
            if (string.IsNullOrWhiteSpace(combinedCode)) return "Application";
            if (combinedCode.Contains("/KemibrugV2.WebApiServer/", StringComparison.OrdinalIgnoreCase)) return "WebApiServer";
            if (combinedCode.Contains("/KemibrugV2.Application/", StringComparison.OrdinalIgnoreCase)) return "Application";
            if (combinedCode.Contains("/KemibrugV2.Infrastructure/", StringComparison.OrdinalIgnoreCase)) return "Infrastructure";
            if (combinedCode.Contains("/KemibrugV2.Core/", StringComparison.OrdinalIgnoreCase)) return "Core";
            return "Application";
        }
    }
}
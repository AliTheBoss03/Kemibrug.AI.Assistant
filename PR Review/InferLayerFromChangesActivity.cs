using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant.PR_Review
{
    /// <summary>
    /// Infers the architectural layer by searching for specific project paths
    /// within a combined string of code changes.
    /// </summary>
    public class InferLayerFromChangesActivity
    {
        private readonly ILayerInferenceService _layerInferenceService;
        private readonly ILogger<InferLayerFromChangesActivity> _logger;

        public InferLayerFromChangesActivity(ILayerInferenceService layerInferenceService, ILogger<InferLayerFromChangesActivity> logger)
        {
            _layerInferenceService = layerInferenceService;
            _logger = logger;
        }

        [Function(nameof(InferLayerFromChangesActivity))]
        public string Run([ActivityTrigger] string combinedCode)
        {
            _logger.LogInformation("Inferring layer from combined code changes.");
            var layer = _layerInferenceService.InferLayer(combinedCode);
            _logger.LogInformation("Inferred layer: '{layer}'", layer);
            return layer;
        }
    }
}
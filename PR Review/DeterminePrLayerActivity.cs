using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant.PR_Review
{

    public class DeterminePrLayerActivity
    {
        private readonly ILogger<DeterminePrLayerActivity> _logger;

        public DeterminePrLayerActivity(ILogger<DeterminePrLayerActivity> logger)
        {
            _logger = logger;
        }

        [Function(nameof(DeterminePrLayerActivity))]
        public string? Run([ActivityTrigger] string sourceRefName)
        {
            _logger.LogInformation("Determining layer from source branch: '{sourceRefName}'", sourceRefName);

            if (string.IsNullOrEmpty(sourceRefName))
            {
                _logger.LogWarning("Source branch name was null or empty. Cannot determine layer.");
                return null;
            }

            var parts = sourceRefName.Split('/');

            foreach (var part in parts)
            {
                switch (part.ToLowerInvariant())
                {
                    case "webapiserver":
                    case "application":
                    case "infrastructure":
                    case "core":
                        var layer = part;
                        _logger.LogInformation("Found matching layer: '{layer}'", layer);
                        return layer;
                }
            }

            _logger.LogWarning("No known architectural layer found in branch name '{sourceRefName}'.", sourceRefName);
            return null;
        }
    }
}
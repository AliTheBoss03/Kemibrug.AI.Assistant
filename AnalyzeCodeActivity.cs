using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kemibrug.AI.Assistant
{
    public static class AnalyzeCodeActivity
    {
        [Function("AnalyzeCodeActivity")]
        public static string Run([ActivityTrigger] string codeToAnalyze, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("AnalyzeCodeActivity");
            logger.LogInformation("Analyzing code: '{code}'", codeToAnalyze);

            // ### MOCK AI-SVAR ###
            // Senere vil denne funktion indeholde den rigtige logik til at bygge en prompt
            // og lave et HTTP-kald til Azure OpenAI. Indtil da returnerer vi et hardcoded svar.

            string fakeViolationResponse = @"
            {
              ""violationFound"": true,
              ""explanation"": ""Dette er en simuleret overtrædelse. Service-laget kalder direkte på et repository.""
            }";

            return fakeViolationResponse;
        }
    }
}
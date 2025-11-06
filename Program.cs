using Azure;
using Azure.Identity;
using Kemibrug.AI.Assistant;
using Kemibrug.AI.Assistant.PR_Review;
using Kemibrug.AI.Assistant.TDD_Kickstarter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // === SHARED SERVICES (Used by both flows) ===

        services.AddHttpClient();

        services.AddAzureClients(clientBuilder =>
        {
            var openAiEndpoint = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint");
            var openAiKey = Environment.GetEnvironmentVariable("AzureOpenAIKey");

            if (!string.IsNullOrEmpty(openAiEndpoint) && !string.IsNullOrEmpty(openAiKey))
            {
                clientBuilder.AddOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            }
        });


        // === TDD KICKSTARTER FLOW REGISTRATIONS ===

        services.AddSingleton<UserStoryTrigger>();
        services.AddSingleton<GetUserStoryActivity>();
        services.AddSingleton<AnalyzeUserStoryActivity>();
        services.AddSingleton<GenerateTestSkeletonActivity>();
        services.AddSingleton<UploadGeneratedTestAsAttachmentActivity>();

        services.AddSingleton<IHeuristicAnalyzer, HeuristicAnalyzer>();
        services.AddSingleton<ITestSkeletonBuilder, CSharpTestSkeletonBuilder>();


        // === PR REVIEW FLOW REGISTRATIONS ===

        services.AddSingleton<PullRequestTrigger>();
        services.AddSingleton<DeterminePrLayerActivity>();
        services.AddSingleton<GetPullRequestChangesActivity>();
        services.AddSingleton<AnalyzeCodeActivity>();
        services.AddSingleton<PostAnalysisCommentActivity>();

        services.AddSingleton<IContextRetrievalService, ContextRetrievalService>();
        services.AddSingleton<IAnalysisResultParser, AnalysisResultParser>();
        services.AddSingleton<IMarkdownCommentBuilder, MarkdownCommentBuilder>();

    })
    .Build();

host.Run();
// Time Zone Agent — A hosted agent that tells the current time in any timezone.
// Uses Microsoft Agent Framework with Foundry Hosting SDK.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-5.1";

static string GetCurrentDateTime(string ianaTimezone)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
    var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    return $"Current time in {ianaTimezone}: {now:dddd, MMMM dd, yyyy 'at' hh:mm tt zzz}";
}

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a helpful assistant that can tell the current date and time in any timezone.
            When asked about the time, use the GetCurrentDateTime tool with the appropriate IANA timezone identifier.
            Be concise and friendly in your responses.
            """,
        name: "time-zone-agent",
        description: "A timezone assistant with a local function tool",
        tools:
        [
            AIFunctionFactory.Create(GetCurrentDateTime, "GetCurrentDateTime",
                "Gets the current date and time for a given IANA timezone identifier (e.g. America/New_York, Asia/Tokyo, Europe/London).")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

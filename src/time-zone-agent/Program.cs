// Time Zone Agent — A hosted agent that tells the current time in any timezone.
// Demonstrates the core hosted agent pattern using Microsoft Agent Framework.

using System.ComponentModel;
using Azure.AI.AgentServer.AgentFramework.Extensions;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("TimeZoneAgent");

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

var endpoint = config["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set. Run setup.ps1 or: dotnet user-secrets set AZURE_OPENAI_ENDPOINT <your-endpoint>");
var deploymentName = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-5.1";

// AzureCliCredential for local dev, DefaultAzureCredential in containers.
TokenCredential credential;
try
{
    credential = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        ? new DefaultAzureCredential()
        : new AzureCliCredential();
}
catch (AuthenticationFailedException ex)
{
    logger.LogCritical(ex, "Authentication failed. Run: az login --tenant <your-tenant-id>");
    throw;
}

logger.LogInformation("Endpoint: {Endpoint}", endpoint);
logger.LogInformation("Model: {Model}", deploymentName);
logger.LogInformation("Auth: {CredentialType}", credential.GetType().Name);

// Function tool — real C# method the model can call server-side
[Description("Gets the current date and time for a given IANA timezone")]
static string GetCurrentDateTime(
    [Description("IANA timezone identifier (e.g. America/New_York, Asia/Tokyo, Europe/London)")] string ianaTimezone)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
    var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    return $"Current time in {ianaTimezone}: {now:dddd, MMMM dd, yyyy 'at' hh:mm tt zzz}";
}

var chatClient = new AzureOpenAIClient(new Uri(endpoint), credential)
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .Build();

var agent = new ChatClientAgent(chatClient,
    name: "TimeZoneAgent",
    instructions: """
        You are a helpful assistant that can tell the current date and time in any timezone.
        When asked about the time, use the GetCurrentDateTime tool with the appropriate IANA timezone identifier.
        Be concise and friendly in your responses.
        """,
    tools: [AIFunctionFactory.Create(GetCurrentDateTime)])
    .AsBuilder()
    .Build();

// Hosting adapter — starts HTTP server on port 8088 with Foundry Responses Protocol
logger.LogInformation("{AgentName} running on {Url}", "TimeZoneAgent", "http://localhost:8088");
await agent.RunAIAgentAsync(telemetrySourceName: "Agents");

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

var notesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "agent-data");
var notesFile = Path.Combine(notesDir, "notes.json");

static string GetCurrentDateTime(string ianaTimezone)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
    var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    return $"Current time in {ianaTimezone}: {now:dddd, MMMM dd, yyyy 'at' hh:mm tt zzz}";
}

string SaveNote(string key, string value)
{
    Directory.CreateDirectory(notesDir);
    var notes = new Dictionary<string, string>();
    if (File.Exists(notesFile))
        notes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(notesFile))
                ?? new Dictionary<string, string>();
    notes[key] = value;
    File.WriteAllText(notesFile, System.Text.Json.JsonSerializer.Serialize(notes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return $"Saved note: {key} = {value}";
}

string ReadNotes(string? key = null)
{
    if (!File.Exists(notesFile))
        return "No notes saved yet.";
    var notes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(notesFile))
                ?? new Dictionary<string, string>();
    if (key != null)
        return notes.TryGetValue(key, out var val) ? $"{key} = {val}" : $"No note found for key '{key}'.";
    if (notes.Count == 0)
        return "No notes saved yet.";
    return string.Join("\n", notes.Select(n => $"{n.Key} = {n.Value}"));
}

string DeleteNote(string key)
{
    if (!File.Exists(notesFile))
        return $"No notes saved yet.";
    var notes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(notesFile))
                ?? new Dictionary<string, string>();
    if (!notes.Remove(key))
        return $"No note found for key '{key}'.";
    File.WriteAllText(notesFile, System.Text.Json.JsonSerializer.Serialize(notes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return $"Deleted note: {key}";
}

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a helpful assistant that can tell the current date and time in any timezone.
            When asked about the time, use the GetCurrentDateTime tool with the appropriate IANA timezone identifier.

            You also have note-taking tools (SaveNote, ReadNotes, DeleteNote) for persisting information.
            Only use these tools when the user explicitly asks you to save, remember, read, list, or delete notes.
            Do NOT use them for regular conversation.

            Be concise and friendly in your responses.
            """,
        name: "time-zone-agent",
        description: "A timezone assistant with notes persistence",
        tools:
        [
            AIFunctionFactory.Create(GetCurrentDateTime, "GetCurrentDateTime",
                "Gets the current date and time for a given IANA timezone identifier (e.g. America/New_York, Asia/Tokyo, Europe/London)."),
            AIFunctionFactory.Create(SaveNote, "SaveNote",
                "Saves a note with a key and value. Only use when the user explicitly asks to save or remember something."),
            AIFunctionFactory.Create(ReadNotes, "ReadNotes",
                "Reads saved notes. Pass a key to read a specific note, or omit to list all notes. Only use when the user explicitly asks to read or list notes."),
            AIFunctionFactory.Create(DeleteNote, "DeleteNote",
                "Deletes a saved note by key. Only use when the user explicitly asks to delete a note.")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

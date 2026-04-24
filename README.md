# Foundry Hosted Agent — Setup Guide

A .NET 10 C# agent deployed to Microsoft Foundry using the Azure Developer CLI (`azd`).

## What This Agent Does

A **Time Zone Agent** — ask it the time in any timezone and it calls a real C# function (`GetCurrentDateTime`) server-side. No hallucinations, no guessing.

## Prerequisites

- .NET 10 SDK
- Azure Developer CLI (`azd`) v1.24.0+
- Azure CLI (`az`)
- Azure subscription with Contributor + Owner/User Access Administrator permissions

## One-Time Setup

### 1. Install the azd agent extension

```powershell
azd ext install azure.ai.agents   # needs v0.1.27-preview+
```

### 2. Authenticate

```powershell
az login --tenant <your-tenant-id>
azd auth login --tenant-id <your-tenant-id>
```

### 3. Initialize environment

```powershell
cd foundry-hosted-agent
azd env set AZURE_TENANT_ID "<your-tenant-id>"
azd env set AZURE_OPENAI_DEPLOYMENT_NAME "gpt-5.1"
azd env set AI_PROJECT_DEPLOYMENTS '[{"name":"gpt-5.1","model":{"name":"gpt-5.1","format":"OpenAI","version":"2025-11-13"},"sku":{"name":"Standard","capacity":10}}]'
azd env set ENABLE_HOSTED_AGENTS "true"
```

## Deploy (3 commands)

```powershell
azd provision    # Creates: Foundry project, ACR, App Insights, model deployment, managed identity + role assignments
azd ai agent run # Test locally — then invoke from another terminal:
                 #   Invoke-RestMethod -Uri http://localhost:8088/responses -Method Post -ContentType "application/json" -Body '{"input":"What time is it in Tokyo?","stream":false}'
azd deploy       # Builds container remotely, deploys to Foundry Agent Service
```

## Verify

```powershell
azd ai agent show --output table
azd ai agent invoke "What time is it in London?"
```

Or use the **Foundry Playground** link from the `azd deploy` output.

## Clean Up

```powershell
azd down         # Deletes ALL resources — stops charges
```

## Project Structure

```
foundry-hosted-agent/
├── azure.yaml                      # azd service definition (host: azure.ai.agent)
├── infra/                          # Bicep IaC (auto-creates ACR, model, roles)
│   ├── main.bicep
│   ├── main.parameters.json
│   └── core/
└── src/time-zone-agent/
    ├── Program.cs                  # Agent logic + GetCurrentDateTime tool
    ├── HostedAgent.csproj          # .NET 10, NuGet dependencies
    ├── agent.yaml                  # Foundry agent manifest (protocols, env vars)
    ├── Dockerfile                  # Multi-stage container build
    └── .dockerignore
```

## Key Configuration Files

| File | Purpose |
|---|---|
| `azure.yaml` | Tells `azd` this is a hosted agent (`host: azure.ai.agent`, `language: docker`, `remoteBuild: true`) |
| `agent.yaml` | Foundry manifest — agent name, protocols (responses), environment variables |
| `main.parameters.json` | Bicep params — `enableHostedAgents=true` triggers ACR + AcrPull role creation |

## Gotchas We Hit

| Issue | Fix |
|---|---|
| `azd ai agent init` needs interactive mode | Scaffold files manually or use a template repo |
| `DeploymentNotFound` | Model wasn't deployed — add it via `AI_PROJECT_DEPLOYMENTS` env var or deploy manually |
| `no azure.ai.agent service found` | `azure.yaml` must use `host: azure.ai.agent` and `language: docker`, not `host: containerapp` |
| `Failed to pull container image` | ACR wasn't created or project identity missing AcrPull — set `enableHostedAgents=true` before provisioning |
| Repeated auth failures | Run `azd auth logout` then `azd auth login --tenant-id <id>` to clear stale tokens |
| `AZURE_TENANT_ID is not set` | Set via `azd env set AZURE_TENANT_ID "<id>"` |
| `PermissionDenied` on chat/completions | Agent identity needs **Azure AI User** role on the project — `azd deploy` assigns this automatically via the postdeploy hook (requires `AZURE_TENANT_ID` to be set) |

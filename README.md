# Foundry Hosted Agent — Setup Guide

A .NET 10 C# agent deployed to Microsoft Foundry using the Azure Developer CLI (`azd`).
Based on the official [foundry-samples](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework).

## What This Agent Does

A **Time Zone Agent** — ask it the time in any timezone and it calls a real C# function (`GetCurrentDateTime`) server-side.

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
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "gpt-5.1"
azd env set AI_PROJECT_DEPLOYMENTS '[{"name":"gpt-5.1","model":{"name":"gpt-5.1","format":"OpenAI","version":"2025-11-13"},"sku":{"name":"Standard","capacity":10}}]'
azd env set ENABLE_HOSTED_AGENTS "true"
```

## Deploy

```powershell
azd provision    # Creates: Foundry project, ACR, App Insights, model deployment, managed identity + role assignments
azd deploy       # Builds container remotely, deploys to Foundry Agent Service
```

## Verify

```powershell
azd ai agent show --output table
azd ai agent invoke "What time is it in London?"
```

Or use the **Foundry Playground** in the Azure AI Foundry portal.

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
    ├── HostedAgent.csproj          # .NET 10 (Microsoft.Agents.AI.Foundry.Hosting)
    ├── agent.yaml                  # Foundry container agent manifest
    ├── agent.manifest.yaml         # Foundry agent manifest (metadata, resources)
    ├── Dockerfile                  # Multi-stage container build
    └── .dockerignore
```

## Key Packages

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI.Foundry.Hosting` | Agent Framework hosting — `AgentHost`, `AddFoundryResponses`, `MapFoundryResponses` |
| `Azure.AI.Projects` | Foundry SDK — `AIProjectClient` and `AsAIAgent` extension |
| `DotNetEnv` | Load `.env` files for local development |

## Key Configuration

| File | Purpose |
|---|---|
| `azure.yaml` | Tells `azd` this is a hosted agent (`host: azure.ai.agent`, `language: docker`, `remoteBuild: true`) |
| `agent.yaml` | Container agent manifest — name, protocols, resources |
| `agent.manifest.yaml` | Rich agent metadata — display name, description, model resource binding |
| `main.parameters.json` | Bicep params — `enableHostedAgents=true` triggers ACR + AcrPull role creation |

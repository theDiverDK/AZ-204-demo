# AZ-204 ConferenceHub Student Guide

This repository is organized as progressive learning paths. You run each path from its branch in order.

## Prerequisites
- Git
- .NET SDK 9
- Azure CLI (`az`) logged in
- Bash (macOS/Linux)

## How This Repo Works
- `ConferenceHub/` is the shared app code.
- Each learning path branch (`lp/*`) adds the next topic.
- Each path has its own deployment script: `LearningPath/<NN-Name>/create.sh`.

## Student Workflow (Recommended)
Clone once, then switch branches as you progress.

```bash
git clone https://github.com/theDiverDK/AZ-204-Demo.git
cd AZ-204-Demo
```

Run learning paths in this order:

1. `lp/01-init`
```bash
git checkout lp/01-init
cd LearningPath/01-Init && ./create.sh
cd ../..
```

2. `lp/02-functions`
```bash
git checkout lp/02-functions
cd LearningPath/02-Functions && ./create.sh
cd ../..
```

3. `lp/03-storage`
```bash
git checkout lp/03-storage
cd LearningPath/03-Storage && ./create.sh
cd ../..
```

4. `lp/04-cosmos`
```bash
git checkout lp/04-cosmos
cd LearningPath/04-Cosmos && ./create.sh && ./migrate.sh
cd ../..
```

5. `lp/05-container`
```bash
git checkout lp/05-container
cd LearningPath/05-Container && ./create.sh
cd ../..
```

6. `lp/06-auth`
```bash
git checkout lp/06-auth
cd LearningPath/06-Auth && ./create.sh
cd ../..
```

7. `lp/07-keyvault`
```bash
git checkout lp/07-keyvault
cd LearningPath/07-KeyVault && ./create.sh
cd ../..
```

8. `lp/09-events`
```bash
git checkout lp/09-events
cd LearningPath/09-Events && ./create.sh
cd ../..
```

9. `lp/10-messages`
```bash
git checkout lp/10-messages
cd LearningPath/10-Messages && ./create.sh
cd ../..
```

10. `lp/11-appinsight`
```bash
git checkout lp/11-appinsight
cd LearningPath/11-AppInsight && ./create.sh
cd ../..
```

## What Each Learning Path Adds

### LP1 (`lp/01-init`)
- Demonstrates: baseline App Service deployment of ConferenceHub.
- `create.sh` adds/updates: resource group, app service plan, web app, app build/publish/deploy, then opens the site.

### LP2 (`lp/02-functions`)
- Demonstrates: Azure Functions integration for confirmation flow.
- `create.sh` adds/updates: function app resources, function deployment, web app settings for function endpoint/key, and redeploys the web app with function calling enabled.

### LP3 (`lp/03-storage`)
- Demonstrates: Azure Blob Storage for session slide uploads.
- `create.sh` adds/updates: storage account + containers for slides, web app settings for storage usage, and redeploys web app with upload/view support.

### LP4 (`lp/04-cosmos`)
- Demonstrates: Cosmos DB as app data store.
- `create.sh` adds/updates: Cosmos account, database, sessions/registrations containers, web app Cosmos settings.
- `migrate.sh` adds/updates: imports seed session data into Cosmos.

### LP5 (`lp/05-container`)
- Demonstrates: containerized deployment path.
- `create.sh` adds/updates: container-related hosting resources and deploys ConferenceHub container image to Azure hosting.

### LP6 (`lp/06-auth`)
- Demonstrates: Entra ID authentication/authorization.
- `create.sh` adds/updates: auth configuration for web app, app settings for tenant/client configuration, and deploys role-aware app behavior.

### LP7 (`lp/07-keyvault`)
- Demonstrates: secret/key management via Key Vault with managed identity and RBAC.
- `create.sh` adds/updates: key vault, role assignments, secret references, and replaces direct sensitive app settings with Key Vault-backed values.

### LP9 (`lp/09-events`)
- Demonstrates: event-driven integration using Event Grid/Event Hub.
- `create.sh` adds/updates: event infrastructure subscriptions/endpoints and app/function settings for event publishing/handling.

### LP10 (`lp/10-messages`)
- Demonstrates: asynchronous messaging with Service Bus and Queue Storage.
- `create.sh` adds/updates: messaging resources, function triggers/bindings deployment, and app settings so registration/thumbnail jobs flow through queues/topics.

### LP11 (`lp/11-appinsight`)
- Demonstrates: full observability with Application Insights.
- `create.sh` adds/updates: shared Application Insights connection across apps/functions, sampling disabled, telemetry settings, and redeploys for dependency/custom-event visibility.

## Verify Current State
- Current branch: `git branch --show-current`
- Local branches: `git branch --list`

## Cleanup Azure Resources
From repo root:

```bash
./cleanup.sh
```

This deletes the resource group and purges soft-deleted resources (including Key Vault).

## Optional: Worktrees (Advanced)
If you want all learning paths checked out at once, use:

```bash
./tools/worktrees.sh
```

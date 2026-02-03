# Variables (Bash)


# from 01-Init.md
# Usage: Azure region for resources (must be swedencentral).
location="swedencentral"


# Usage: Random suffix for globally-unique resource names.
random=49152 #"$RANDOM"

# Usage: Resource group that holds all resources.
resourceGroupName="rg-conferencehub-$random"


# Usage: App Service plan name for the web app.
appServicePlanName="plan-conferencehub-$random"

# Usage: Azure Web App name for ConferenceHub.
webAppName="app-conferencehub-$random"  # must be globally unique

# Usage: App Service runtime stack/version string.
appRuntime="DOTNETCORE|9.0"

# Usage: Local publish output folder for the web app.
publishDir="./publish"

# Usage: Path to the deployment zip for the web app.
zipPath="./ConferenceHub.zip"

# from 02-Functions.md
# Usage: Function App plan name (Elastic Premium).
functionPlanName="plan-conferencehub-functions-$random"

# Usage: Storage account for Function App runtime.
functionStorageAccountName="stconfhubfunc$random"

# Usage: Azure Function App name.
functionAppName="func-conferencehub-$random"

# from 03-Storage.md
# Usage: Storage account for blobs/tables.
storageAccountName="stconferencehub$random"

# from 04-CosmosDB.md
# Usage: Cosmos DB account name.
cosmosAccountName="cosmos-conferencehub-$random"

# Usage: Cosmos DB database name.
cosmosDatabaseName="ConferenceHubDB"

# from 05-Docker.md
# Usage: Azure Container Registry name.
acrName="acrconferencehub$random"

# Usage: Container Apps environment name.
containerAppsEnvName="env-conferencehub-$random"

# Usage: Container App name.
containerAppName="app-conferencehub-$random"

# Usage: Container image reference used for deployment.
containerImage="$acrName.azurecr.io/conferencehub:latest"

# Usage: App Service plan name for container alternative.
containerAppPlanName="plan-conferencehub-container-$random"

# from 06-Authentication.md
# Usage: Entra ID tenant ID.
azureAdTenantId="<your-tenant-id>"

# Usage: Entra ID app client ID.
azureAdClientId="<your-client-id>"

# Usage: Entra ID app client secret.
azureAdClientSecret="<your-client-secret>"

# from 07-KeyVault.md
# Usage: Key Vault name.
keyVaultName="kv-conferencehub-$random"

# Usage: App Configuration name.
appConfigName="appconfig-conferencehub-$random"

# from 08-APIM.md
# Usage: API Management instance name.
apiManagementName="apim-conferencehub-$random"

# Usage: APIM publisher email.
apiManagementPublisherEmail="instructor@example.com"

# Usage: APIM publisher name.
apiManagementPublisherName="ConferenceHub"

# Usage: APIM subscription key for calling APIs.
apiManagementSubscriptionKey="<your-subscription-key>"

# from 09-EventGrid.md
# Usage: Event Hubs namespace name.
eventHubNamespaceName="evhns-conferencehub-$random"

# Usage: Event Hub name.
eventHubName="session-feedback"

# Usage: Event Grid topic name.
eventGridTopicName="evgt-conferencehub-$random"

# from 10-ServiceBus.md
# Usage: Service Bus namespace name.
serviceBusNamespaceName="sb-conferencehub-$random"

# Usage: Service Bus queue name.
serviceBusQueueName="registration-queue"

# Usage: Service Bus topic name.
serviceBusTopicName="notification-topic"

# from 11-AppInsights.md
# Usage: Log Analytics workspace name.
logAnalyticsWorkspaceName="law-conferencehub-$random"

# Usage: Application Insights resource name.
appInsightsName="appinsights-conferencehub-$random"


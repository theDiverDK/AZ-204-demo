# Variables (Bash)

All variables are listed in the order they are first introduced in the learning path Bash blocks.
Values are copied from the learning path files.

```bash
# from 01-Init.md
# Usage: Azure region for resources (must be swedencentral).
location="swedencentral"

# Usage: Resource group that holds all resources.
resourceGroupName="$resourceGroupName"

# Usage: Random suffix for globally-unique resource names.
random=14537 #"$RANDOM"

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

# Usage: Function key used by the web app to call Functions.
functionKey=$(az functionapp function keys list \
  --name "$functionAppName" \
  --resource-group "$resourceGroupName" \
  --function-name SendConfirmation \
  --query "default" -o tsv)

# from 03-Storage.md
# Usage: Storage account for blobs/tables.
storageAccountName="stconferencehub$random"

# Usage: Storage account access key (from Azure CLI).
storageKey=$(az storage account keys list \
  --account-name "$storageAccountName" \
  --resource-group "$resourceGroupName" \
  --query "[0].value" \
  --output tsv)

# Usage: Storage connection string (from Azure CLI).
connectionString=$(az storage account show-connection-string \
  --name "$storageAccountName" \
  --resource-group "$resourceGroupName" \
  --output tsv)

# from 04-CosmosDB.md
# Usage: Cosmos DB account name.
cosmosAccountName="cosmos-conferencehub-$random"

# Usage: Cosmos DB database name.
cosmosDatabaseName="ConferenceHubDB"

# Usage: Cosmos DB connection string (from Azure CLI).
cosmosConnectionString=$(az cosmosdb keys list \
  --name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv)

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

# Usage: Storage connection string (from Azure CLI).
storageConnectionString=$(az storage account show-connection-string \
  --name "$storageAccountName" \
  --resource-group "$resourceGroupName" \
  --output tsv)

# Usage: ACR username (from Azure CLI).
acrUsername=$(az acr credential show \
  --name "$acrName" \
  --resource-group $resourceGroupName \
  --query "username" \
  --output tsv)

# Usage: ACR password (from Azure CLI).
acrPassword=$(az acr credential show \
  --name "$acrName" \
  --resource-group $resourceGroupName \
  --query "passwords[0].value" \
  --output tsv)

# from 06-Authentication.md
# Usage: Entra ID tenant ID.
azureAdTenantId="<your-tenant-id>"

# Usage: Entra ID app client ID.
azureAdClientId="<your-client-id>"

# Usage: Entra ID app client secret.
azureAdClientSecret="<your-client-secret>"

# Usage: App registration client ID (from Azure CLI).
appId=$(az ad app list --display-name "ConferenceHub-WebApp" --query "[0].appId" -o tsv)

# Usage: Tenant ID (from Azure CLI).
tenantId=$(az account show --query tenantId -o tsv)

# Usage: Raw secret creation response (from Azure CLI).
secretResult=$(az ad app credential reset \
  --id "$appId" \
  --append \
  --years 1)

# Usage: Client secret value extracted from secretResult.
clientSecret=$(echo "$secretResult" | jq -r ".password")

# Usage: Service principal object ID.
spObjectId=$(az ad sp list --display-name "ConferenceHub-WebApp" --query "[0].id" -o tsv)

# Usage: Signed-in user object ID.
userObjectId=$(az ad signed-in-user show --query id -o tsv)

# Usage: App role ID for Organizer role.
organizerRoleId=$(az ad app show --id "$appId" --query "appRoles[?value=='Organizer'].id" -o tsv)

# from 07-KeyVault.md
# Usage: Key Vault name.
keyVaultName="kv-conferencehub-$random"

# Usage: App Configuration name.
appConfigName="appconfig-conferencehub-$random"

# Usage: Key Vault URI (from Azure CLI).
keyVaultUri=$(az keyvault show \
  --name "$keyVaultName" \
  --resource-group "$resourceGroupNameName" \
  --query properties.vaultUri \
  --output tsv)

# Usage: Web App managed identity principal ID.
webAppPrincipalId=$(az webapp identity show \
  --name "conferencehub-demo-az204reinke" \
  --resource-group "$resourceGroupNameName" \
  --query principalId \
  --output tsv)

# Usage: Function App managed identity principal ID.
funcPrincipalId=$(az functionapp identity show \
  --name "$functionAppName" \
  --resource-group "$resourceGroupNameName" \
  --query principalId \
  --output tsv)

# Usage: Key Vault resource ID.
keyVaultId=$(az keyvault show \
  --name "$keyVaultName" \
  --resource-group "$resourceGroupNameName" \
  --query id \
  --output tsv)

# Usage: Current user object ID.
currentUserId=$(az ad signed-in-user show --query id -o tsv)

# Usage: App Configuration endpoint URL.
appConfigEndpoint=$(az appconfig show \
  --name $appConfigName \
  --resource-group $resourceGroupNameName \
  --query endpoint \
  --output tsv)

# Usage: App Configuration resource ID.
appConfigId=$(az appconfig show \
  --name $appConfigName \
  --resource-group $resourceGroupNameName \
  --query id \
  --output tsv)

# from 08-APIM.md
# Usage: API Management instance name.
apiManagementName="apim-conferencehub-$random"

# Usage: APIM publisher email.
apiManagementPublisherEmail="instructor@example.com"

# Usage: APIM publisher name.
apiManagementPublisherName="ConferenceHub"

# Usage: APIM subscription key for calling APIs.
apiManagementSubscriptionKey="<your-subscription-key>"

# Usage: APIM gateway URL (from Azure CLI).
apimGatewayUrl=$(az apim show \
  --name $apiManagementName \
  --resource-group $resourceGroupNameName \
  --query "gatewayUrl" \
  --output tsv)

# Usage: Function App key used by APIM backend.
functionAppKey=$(az functionapp keys list \
  --name $functionAppName \
  --resource-group $resourceGroupNameName \
  --query "functionKeys.default" \
  --output tsv)

# Usage: APIM subscription key returned by API.
subscriptionKey=$(az apim subscription show \
  --resource-group $resourceGroupNameName \
  --service-name $apiManagementName \
  --subscription-id "test-free-sub" \
  --query "primaryKey" \
  --output tsv)

# Usage: APIM base URL for calling APIs.
apimUrl=$("https://$apiManagementName.azure-api.net")

# Usage: Temp variable for CLI JSON output.
response=$(Invoke-RestMethod \
-Uri "$apimUrl/conferencehub/sessions" \
-Method Get \
-Headers @{)

# Usage: Temp variable for CLI JSON output.
response1=$(Invoke-RestMethod \
-Uri "$apimUrl/conferencehub/sessions/1" \
-Method Get \
-Headers @{)

# Usage: Temp variable for CLI JSON output.
response2=$(Invoke-RestMethod \
-Uri "$apimUrl/conferencehub/sessions/1" \
-Method Get \
-Headers @{)

# from 09-EventGrid.md
# Usage: Event Hubs namespace name.
eventHubNamespaceName="evhns-conferencehub-$random"

# Usage: Event Hub name.
eventHubName="session-feedback"

# Usage: Event Grid topic name.
eventGridTopicName="evgt-conferencehub-$random"

# Usage: Storage account resource ID.
storageAccountId=$(az storage account show \
  --name $storageAccountName \
  --resource-group $resourceGroupNameName \
  --query id \
  --output tsv)

# Usage: Function trigger URL (from Azure CLI).
functionUrl=$(az functionapp function show \
  --name $functionAppName \
  --resource-group $resourceGroupNameName \
  --function-name ProcessSlideUpload \
  --query invokeUrlTemplate \
  --output tsv)

# Usage: Event Hub connection string.
eventHubConnectionString=$(az eventhubs namespace authorization-rule keys list \
  --namespace-name $eventHubNamespaceName \
  --resource-group $resourceGroupNameName \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv)

# Usage: Local test file name/path.
testFile=$("test-slide.pdf")

# from 10-ServiceBus.md
# Usage: Service Bus namespace name.
serviceBusNamespaceName="sb-conferencehub-$random"

# Usage: Service Bus queue name.
serviceBusQueueName="registration-queue"

# Usage: Service Bus topic name.
serviceBusTopicName="notification-topic"

# Usage: Service Bus connection string.
serviceBusConnectionString=$(az servicebus namespace authorization-rule keys list \
  --namespace-name $serviceBusNamespaceName \
  --resource-group $resourceGroupNameName \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv)

# from 11-AppInsights.md
# Usage: Log Analytics workspace name.
logAnalyticsWorkspaceName="law-conferencehub-$random"

# Usage: Application Insights resource name.
appInsightsName="appinsights-conferencehub-$random"

# Usage: Log Analytics workspace ID.
workspaceId=$(az monitor log-analytics workspace show \
  --resource-group $resourceGroupNameName \
  --workspace-name $logAnalyticsWorkspaceName \
  --query id \
  --output tsv)

# Usage: App Insights instrumentation key.
instrumentationKey=$(az monitor app-insights component show \
  --app $appInsightsName \
  --resource-group $resourceGroupNameName \
  --query instrumentationKey \
  --output tsv)

```

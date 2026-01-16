targetScope = 'resourceGroup'

param location string = resourceGroup().location
param mainWebAppName string
param functionAppName string
param storageAccountName string
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param keyVaultUri string
param appConfigEndpoint string
param azureAdInstance string = 'https://login.microsoftonline.com/'
param azureAdTenantId string
param azureAdClientId string
@secure()
param azureAdClientSecret string
param apiManagementGatewayUrl string
@secure()
param apiManagementSubscriptionKey string
param eventHubNamespaceName string
param eventHubName string = 'session-feedback'
param serviceBusNamespaceName string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-conferencehub'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-conferencehub'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' existing = {
  name: eventHubNamespaceName
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var cosmosConnectionString = listConnectionStrings(cosmosAccount.id, cosmosAccount.apiVersion).connectionStrings[0].connectionString
var eventHubKeys = listKeys(resourceId('Microsoft.EventHub/namespaces/authorizationRules', eventHubNamespace.name, 'RootManageSharedAccessKey'), '2017-04-01')
var eventHubConnectionString = eventHubKeys.primaryConnectionString
var serviceBusKeys = listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2017-04-01')
var serviceBusConnectionString = serviceBusKeys.primaryConnectionString
var functionHostKeys = listKeys(resourceId('Microsoft.Web/sites/host', functionAppName, 'default'), '2022-09-01')
var functionKey = functionHostKeys.functionKeys.default

resource mainWebApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: mainWebAppName
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: functionAppName
}

resource mainWebAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${mainWebApp.name}/appsettings'
  properties: union(defaultAppSettings, {
    KeyVault__VaultUri: keyVaultUri
    AppConfiguration__Endpoint: appConfigEndpoint
    AzureStorage__ConnectionString: storageConnectionString
    AzureFunctions__SendConfirmationUrl: 'https://${functionAppName}.azurewebsites.net/api/SendConfirmation'
    AzureFunctions__FunctionKey: functionKey
    CosmosDb__ConnectionString: cosmosConnectionString
    CosmosDb__DatabaseName: cosmosDatabaseName
    AzureAd__Instance: azureAdInstance
    AzureAd__TenantId: azureAdTenantId
    AzureAd__ClientId: azureAdClientId
    AzureAd__ClientSecret: azureAdClientSecret
    ApiManagement__GatewayUrl: apiManagementGatewayUrl
    ApiManagement__SubscriptionKey: apiManagementSubscriptionKey
    EventHub__ConnectionString: eventHubConnectionString
    EventHub__Name: eventHubName
    ServiceBus__ConnectionString: serviceBusConnectionString
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
  })
  dependsOn: [
    appInsights
  ]
}

resource functionAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${functionApp.name}/appsettings'
  properties: {
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
  }
  dependsOn: [
    appInsights
  ]
}

output appInsightsConnectionString string = appInsights.properties.ConnectionString

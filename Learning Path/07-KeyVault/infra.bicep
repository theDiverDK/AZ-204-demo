targetScope = 'resourceGroup'

param location string = resourceGroup().location
param mainWebAppName string
param functionAppName string
param storageAccountName string
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param keyVaultName string
param appConfigName string
param azureAdInstance string = 'https://login.microsoftonline.com/'
param azureAdTenantId string
param azureAdClientId string
@secure()
param azureAdClientSecret string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    sku: {
      name: 'standard'
      family: 'A'
    }
  }
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: appConfigName
  location: location
  sku: {
    name: 'Standard'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var cosmosConnectionString = listConnectionStrings(cosmosAccount.id, cosmosAccount.apiVersion).connectionStrings[0].connectionString
var functionHostKeys = listKeys(resourceId('Microsoft.Web/sites/host', functionAppName, 'default'), '2022-09-01')
var functionKey = functionHostKeys.functionKeys.default

resource mainWebApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: mainWebAppName
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: functionAppName
}

resource keyVaultAccessWebApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, mainWebApp.id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: mainWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultAccessFunctionApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appConfigAccessWebApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appConfig.id, mainWebApp.id, 'appconfig-reader')
  scope: appConfig
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
    principalId: mainWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appConfigAccessFunctionApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appConfig.id, functionApp.id, 'appconfig-reader')
  scope: appConfig
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource mainWebAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${mainWebApp.name}/appsettings'
  properties: union(defaultAppSettings, {
    KeyVault__VaultUri: keyVault.properties.vaultUri
    AppConfiguration__Endpoint: appConfig.properties.endpoint
    AzureStorage__ConnectionString: storageConnectionString
    AzureFunctions__SendConfirmationUrl: 'https://${functionAppName}.azurewebsites.net/api/SendConfirmation'
    AzureFunctions__FunctionKey: functionKey
    CosmosDb__ConnectionString: cosmosConnectionString
    CosmosDb__DatabaseName: cosmosDatabaseName
    AzureAd__Instance: azureAdInstance
    AzureAd__TenantId: azureAdTenantId
    AzureAd__ClientId: azureAdClientId
    AzureAd__ClientSecret: azureAdClientSecret
  })
  dependsOn: [
    keyVaultAccessWebApp
    appConfigAccessWebApp
  ]
}

output keyVaultUri string = keyVault.properties.vaultUri
output appConfigEndpoint string = appConfig.properties.endpoint

targetScope = 'resourceGroup'

param location string = resourceGroup().location
param mainWebAppName string
param storageAccountName string
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param functionAppName string
param azureAdInstance string = 'https://login.microsoftonline.com/'
param azureAdTenantId string
param azureAdClientId string
@secure()
param azureAdClientSecret string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
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

resource mainWebAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${mainWebApp.name}/appsettings'
  properties: union(defaultAppSettings, {
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
}

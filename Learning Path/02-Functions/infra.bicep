targetScope = 'resourceGroup'

param location string = resourceGroup().location
param functionStorageAccountName string
param functionAppName string
param functionPlanName string = 'plan-conferencehub-functions'
param functionRuntime string = 'DOTNET-ISOLATED|8.0'
param mainWebAppName string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource functionStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: functionStorageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
  }
}

var functionStorageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${functionStorage.name};AccountKey=${listKeys(functionStorage.id, functionStorage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource functionPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: functionPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: functionRuntime
      ftpsState: 'Disabled'
    }
  }
}

resource functionAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${functionApp.name}/appsettings'
  properties: {
    AzureWebJobsStorage: functionStorageConnectionString
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    FUNCTIONS_EXTENSION_VERSION: '~4'
    WEBSITE_RUN_FROM_PACKAGE: '1'
  }
}

resource mainWebApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: mainWebAppName
}

var functionHostKeys = listKeys(resourceId('Microsoft.Web/sites/host', functionAppName, 'default'), '2022-09-01')
var functionKey = functionHostKeys.functionKeys.default

resource mainWebAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${mainWebApp.name}/appsettings'
  properties: union(defaultAppSettings, {
    AzureFunctions__SendConfirmationUrl: 'https://${functionAppName}.azurewebsites.net/api/SendConfirmation'
    AzureFunctions__FunctionKey: functionKey
  })
  dependsOn: [
    functionApp
    functionAppSettings
  ]
}

output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output functionKey string = functionKey

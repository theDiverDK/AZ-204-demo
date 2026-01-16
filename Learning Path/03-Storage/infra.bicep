targetScope = 'resourceGroup'

param location string = resourceGroup().location
param storageAccountName string
param mainWebAppName string
param functionAppName string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
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

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  name: '${storage.name}/default'
}

resource slidesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storage.name}/default/speaker-slides'
  properties: {
    publicAccess: 'Blob'
  }
  dependsOn: [
    blobService
  ]
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  name: '${storage.name}/default'
}

resource auditLogsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  name: '${storage.name}/default/AuditLogs'
  dependsOn: [
    tableService
  ]
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource mainWebApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: mainWebAppName
}

var functionHostKeys = listKeys(resourceId('Microsoft.Web/sites/host', functionAppName, 'default'), '2022-09-01')
var functionKey = functionHostKeys.functionKeys.default

resource mainWebAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${mainWebApp.name}/appsettings'
  properties: union(defaultAppSettings, {
    AzureStorage__ConnectionString: storageConnectionString
    AzureFunctions__SendConfirmationUrl: 'https://${functionAppName}.azurewebsites.net/api/SendConfirmation'
    AzureFunctions__FunctionKey: functionKey
  })
  dependsOn: [
    slidesContainer
    auditLogsTable
  ]
}

output storageConnectionString string = storageConnectionString

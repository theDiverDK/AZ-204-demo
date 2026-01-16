targetScope = 'resourceGroup'

param location string = resourceGroup().location
param acrName string
param containerAppsEnvName string = 'env-conferencehub'
param containerAppName string = 'app-conferencehub'
param containerImage string
param storageAccountName string
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param functionAppName string

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${containerAppsEnvName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: listKeys(logAnalytics.id, logAnalytics.apiVersion).primarySharedKey
      }
    }
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

var acrCredentials = listCredentials(acr.id, acr.apiVersion)

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: '${acr.name}.azurecr.io'
          username: acrCredentials.username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acrCredentials.passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'conferencehub'
          image: containerImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'CosmosDb__ConnectionString'
              value: cosmosConnectionString
            }
            {
              name: 'CosmosDb__DatabaseName'
              value: cosmosDatabaseName
            }
            {
              name: 'AzureStorage__ConnectionString'
              value: storageConnectionString
            }
            {
              name: 'AzureFunctions__SendConfirmationUrl'
              value: 'https://${functionAppName}.azurewebsites.net/api/SendConfirmation'
            }
            {
              name: 'AzureFunctions__FunctionKey'
              value: functionKey
            }
          ]
        }
      ]
    }
  }
}

output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

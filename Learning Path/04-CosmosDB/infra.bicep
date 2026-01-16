targetScope = 'resourceGroup'

param location string = resourceGroup().location
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param mainWebAppName string
param storageAccountName string
param functionAppName string

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    enableAutomaticFailover: false
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  name: '${cosmosAccount.name}/${cosmosDatabaseName}'
  properties: {
    resource: {
      id: cosmosDatabaseName
    }
  }
}

resource sessionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  name: '${cosmosAccount.name}/${cosmosDatabaseName}/Sessions'
  properties: {
    resource: {
      id: 'Sessions'
      partitionKey: {
        paths: [
          '/conferenceId'
        ]
        kind: 'Hash'
      }
    }
    options: {
      throughput: 400
    }
  }
  dependsOn: [
    cosmosDatabase
  ]
}

resource registrationsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  name: '${cosmosAccount.name}/${cosmosDatabaseName}/Registrations'
  properties: {
    resource: {
      id: 'Registrations'
      partitionKey: {
        paths: [
          '/sessionId'
        ]
        kind: 'Hash'
      }
    }
    options: {
      throughput: 400
    }
  }
  dependsOn: [
    cosmosDatabase
  ]
}

var cosmosConnectionString = listConnectionStrings(cosmosAccount.id, cosmosAccount.apiVersion).connectionStrings[0].connectionString

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
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
    CosmosDb__ConnectionString: cosmosConnectionString
    CosmosDb__DatabaseName: cosmosDatabaseName
  })
  dependsOn: [
    sessionsContainer
    registrationsContainer
  ]
}

output cosmosConnectionString string = cosmosConnectionString

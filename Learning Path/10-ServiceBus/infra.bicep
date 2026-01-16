targetScope = 'resourceGroup'

param location string = resourceGroup().location
param mainWebAppName string
param storageAccountName string
param cosmosAccountName string
param cosmosDatabaseName string = 'ConferenceHubDB'
param functionAppName string
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
param serviceBusQueueName string = 'registration-queue'
param serviceBusTopicName string = 'notification-topic'

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
}

resource registrationQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: '${serviceBusNamespace.name}/${serviceBusQueueName}'
  properties: {
    enablePartitioning: true
    requiresDuplicateDetection: false
  }
}

resource notificationTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  name: '${serviceBusNamespace.name}/${serviceBusTopicName}'
  properties: {
    enablePartitioning: true
  }
}

resource emailSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  name: '${serviceBusNamespace.name}/${serviceBusTopicName}/email-subscription'
  properties: {
    maxDeliveryCount: 10
  }
}

resource smsSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  name: '${serviceBusNamespace.name}/${serviceBusTopicName}/sms-subscription'
  properties: {
    maxDeliveryCount: 10
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource storageQueueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  name: '${storage.name}/default'
}

resource backgroundTasksQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/background-tasks'
  dependsOn: [
    storageQueueService
  ]
}

resource slideProcessingQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/slide-processing'
  dependsOn: [
    storageQueueService
  ]
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' existing = {
  name: eventHubNamespaceName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var cosmosConnectionString = listConnectionStrings(cosmosAccount.id, cosmosAccount.apiVersion).connectionStrings[0].connectionString
var functionHostKeys = listKeys(resourceId('Microsoft.Web/sites/host', functionAppName, 'default'), '2022-09-01')
var functionKey = functionHostKeys.functionKeys.default
var eventHubKeys = listKeys(resourceId('Microsoft.EventHub/namespaces/authorizationRules', eventHubNamespace.name, 'RootManageSharedAccessKey'), '2017-04-01')
var eventHubConnectionString = eventHubKeys.primaryConnectionString
var serviceBusKeys = listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2017-04-01')
var serviceBusConnectionString = serviceBusKeys.primaryConnectionString

resource mainWebApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: mainWebAppName
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
  })
  dependsOn: [
    registrationQueue
    notificationTopic
    emailSubscription
    smsSubscription
    backgroundTasksQueue
    slideProcessingQueue
  ]
}

output serviceBusConnectionString string = serviceBusConnectionString

targetScope = 'resourceGroup'

param location string = resourceGroup().location
param appServicePlanName string
param webAppName string
param appServicePlanSku string = 'S1'
param appRuntime string = 'DOTNETCORE|10.0'
param appCommandLine string = './ConferenceHub'
param enableSystemIdentity bool = true
param appSettings object = {}

var defaultAppSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

var mergedAppSettings = union(defaultAppSettings, appSettings)

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
    tier: appServicePlanSku == 'F1' ? 'Free' : 'Standard'
    size: appServicePlanSku
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: enableSystemIdentity ? {
    type: 'SystemAssigned'
  } : null
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: appRuntime
      appCommandLine: appCommandLine
      ftpsState: 'Disabled'
    }
  }
}

resource webAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: '${webApp.name}/appsettings'
  properties: mergedAppSettings
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name

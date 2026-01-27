# Azure Deployment Guide - ConferenceHub

## Infrastructure Summary (Bicep)

The template in `ConferenceHub/devops/main.bicep` provisions:
- App Service plan (Linux, PremiumV3 by default)
- Linux Web App with HTTPS-only and FTPS disabled
- Optional system-assigned managed identity
- App settings (defaults merged with supplied `appSettings`)

Default app settings:
- `ASPNETCORE_ENVIRONMENT=Production`
- `WEBSITE_RUN_FROM_PACKAGE=1`

Required parameters:
- `appServicePlanName`
- `webAppName`

Optional parameters:
- `location` (defaults to resource group location)
- `appServicePlanSku` (default `P0v3`)
- `appRuntime` (default `DOTNETCORE|9.0`)
- `appCommandLine` (default `/home/site/wwwroot/ConferenceHub`)
- `enableSystemIdentity`
- `appSettings`

## Option 1: Deploy via Azure CLI (Bicep + App)

Set variables used in the commands (Azure Cloud Shell):
```command prompt
RG_NAME="rg-conferencehub"
LOCATION="swedencentral"
PLAN_NAME="plan-conferencehub"
RANDOM="$RANDOM"
APP_NAME="conferencehub-$RANDOM"
```

### Step 1: Login to Azure
```command prompt
az login
az account set --subscription "<subscription-id>"
```

### Step 2: Create a Resource Group
```command prompt
az group create --name $RG_NAME --location $LOCATION
```

### Step 3: Deploy the Infrastructure (Bicep)
```command prompt
az deployment group create \
  --resource-group $RG_NAME \
  --template-file ConferenceHub/devops/main.bicep \
  --parameters \
    appServicePlanName=$PLAN_NAME \
    webAppName=$APP_NAME
```

Optional: add custom settings or runtime overrides
```command prompt
az deployment group create \
  --resource-group $RG_NAME \
  --template-file ConferenceHub/devops/main.bicep \
  --parameters \
    appServicePlanName=$PLAN_NAME \
    webAppName=$APP_NAME \
    appCommandLine="dotnet /home/site/wwwroot/ConferenceHub.dll" \
    appSettings='{ "FeatureFlags__EnableDemo": "true" }'
```

### Step 3a: Deploy the Infrastructure via Azure CLI (no Bicep)
```command prompt
az appservice plan create \
  --name $PLAN_NAME \
  --resource-group $RG_NAME \
  --sku P0v3 \
  --is-linux

az webapp create \
  --name $APP_NAME \
  --resource-group $RG_NAME \
  --plan $PLAN_NAME \
  --runtime "DOTNETCORE:9.0"

az webapp config appsettings set \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    WEBSITE_RUN_FROM_PACKAGE=1
```

Optional: enable system-assigned identity and set the app command line
```command prompt
az webapp identity assign \
  --resource-group $RG_NAME \
  --name $APP_NAME

az webapp config set \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --generic-configurations '{ "appCommandLine": "dotnet /home/site/wwwroot/ConferenceHub.dll" }'
```

### Step 4: Deploy the Application

**Zip Deploy**
```command prompt
# Publish the app
cd ConferenceHub
dotnet publish -c Release -o ./publish

# If you keep the default appCommandLine (/home/site/wwwroot/ConferenceHub),
# publish self-contained for Linux:
# dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish

# Create a zip file
cd publish
zip -r ../app.zip .
cd ..

# Deploy to Azure
az webapp deploy \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --src-path ./app.zip \
  --type zip

```


### Step 5: Browse the App
```command prompt
az webapp browse \
  --name $APP_NAME \
  --resource-group $RG_NAME
```

## Verify Deployment

### Check App Service Status
```command prompt
az webapp show \
  --name $APP_NAME \
  --resource-group $RG_NAME \
  --query state
```


### Test the Application
1. Navigate to `https://$APP_NAME.azurewebsites.net`
2. Verify the home page loads
3. Test the Sessions page: `/sessions`
4. Test the Organizer dashboard: `/organizer`
5. Try registering for a session

## Option 2: Deploy via Azure DevOps Pipeline

### Step 1: Create Service Connection
- Create an Azure Resource Manager service connection in Azure DevOps.
- Grant it access to the subscription/resource group.

### Step 2: Configure Pipeline Variables
Update `ConferenceHub/azure-pipelines.yml` variables:
- `azureSubscription` (service connection name)
- `resourceGroupName`, `location`
- `appServicePlanName`, `webAppName`
- `appServicePlanSku`, `appRuntime`

### Step 3: Run Pipeline
- Create a pipeline from `ConferenceHub/azure-pipelines.yml`.
- Run the pipeline; it deploys infra using Bicep and then publishes the web app.

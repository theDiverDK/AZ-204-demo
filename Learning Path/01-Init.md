# Learning Path 1: Init (Infra + App Deployment)

This guide deploys the base ConferenceHub infrastructure and the web app using Azure CLI.

## Prerequisites
- Azure CLI installed and logged in
- .NET 10 SDK installed

## Variables
```bash
# Required
location="swedencentral"
resourceGroupName="$resourceGroupName"

# Random suffix for globally unique names
random="$RANDOM"

# App Service
appServicePlanName="plan-conferencehub-$random"
webAppName="app-conferencehub-$random"  # must be globally unique

# Runtime
appRuntime="DOTNETCORE|9.0"

# Build output
publishDir="./publish"
zipPath="./ConferenceHub.zip"
```

## Step 1: Create Resource Group
```bash
az group create --name "$resourceGroupNameName" --location "$location"
```

## Step 2: Create App Service Plan (Linux)
```bash
az appservice plan create \
  --name "$appServicePlanName" \
  --resource-group "$resourceGroupNameName" \
  --location "$location" \
  --sku P0v3 \
  --is-linux
```

## Step 3: Create Web App
```bash
az webapp create \
  --name "$webAppName" \
  --resource-group "$resourceGroupNameName" \
  --plan "$appServicePlanName" \
  --runtime "$appRuntime"
```

## Step 4: Build and Publish the App
```bash
# From repo root
cd ConferenceHub

# Publish self-contained build for Linux
rm -rf "$publishDir"
mkdir "$publishDir"

dotnet publish ConferenceHub.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$publishDir"

# Create ZIP
rm -f "$zipPath"
( cd "$publishDir" && zip -r "../$(basename "$zipPath")" . )
```

## Step 5: Deploy the App (ZIP Deploy)
```bash
az webapp deploy \
  --resource-group "$resourceGroupNameName" \
  --name "$webAppName" \
  --src-path "$zipPath"
```

## Step 6: Set Startup Command
```bash
az webapp config set \
  --resource-group "$resourceGroupNameName" \
  --name "$webAppName" \
  --startup-file "/home/site/wwwroot/ConferenceHub"
```

## Step 7: Browse the App
```bash
az webapp browse \
  --resource-group "$resourceGroupNameName" \
  --name "$webAppName"
```

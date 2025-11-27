# Azure Deployment Guide - ConferenceHub Part 1


## Option 1: Deploy via Azure CLI

### Step 1: Login to Azure
```command prompt
az login
```

### Step 2: Create a Resource Group
```command prompt
az group create --name rg-conferencehub --location swedencentral
```

### Step 3: Create an App Service Plan
```command prompt
az appservice plan create `
  --name plan-conferencehub `
  --resource-group rg-conferencehub `
  --sku B1 `
  --is-linux
```

### Step 4: Create a Web App
```command prompt
az webapp create `
  --name conferencehub-demo-az204reinke `
  --resource-group rg-conferencehub `
  --plan plan-conferencehub `
  --runtime "DOTNETCORE:8.0"
```
*Replace az204reinke with a unique identifier as the app name must be globally unique*

### Step 5: Deploy the Application

**Zip Deploy**
```command prompt
# Publish the app
cd ConferenceHub
dotnet publish -c Release -o ./publish

# Create a zip file
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force

# Deploy to Azure
az webapp deploy `
  --resource-group rg-conferencehub `
  --name conferencehub-demo-az204reinke `
  --src-path ./publish.zip

```


### Step 6: Browse the App
```command prompt
az webapp browse `
  --name conferencehub-demo-az204reinke `
  --resource-group rg-conferencehub
```

## Verify Deployment

### Check App Service Status
```command prompt
az webapp show `
  --name conferencehub-demo-az204reinke `
  --resource-group rg-conferencehub `
  --query state
```


### Test the Application
1. Navigate to `https://conferencehub-demo-az204reinke.azurewebsites.net`
2. Verify the home page loads
3. Test the Sessions page: `/sessions`
4. Test the Organizer dashboard: `/organizer`
5. Try registering for a session

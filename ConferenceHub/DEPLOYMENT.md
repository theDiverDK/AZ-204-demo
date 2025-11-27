# Azure Deployment Guide - ConferenceHub Part 1

## Deploying to Azure App Service

This guide covers deploying the ConferenceHub application to Azure App Service for the first learning path of AZ-204.

## Prerequisites
- Azure subscription
- Azure CLI installed or Azure Portal access
- .NET 8.0 SDK
- Git (optional, for deployment)

## Option 1: Deploy via Azure CLI

### Step 1: Login to Azure
```powershell
az login
```

### Step 2: Create a Resource Group
```powershell
az group create --name rg-conferencehub --location eastus
```

### Step 3: Create an App Service Plan
```powershell
az appservice plan create `
  --name plan-conferencehub `
  --resource-group rg-conferencehub `
  --sku B1 `
  --is-linux
```

### Step 4: Create a Web App
```powershell
az webapp create `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub `
  --plan plan-conferencehub `
  --runtime "DOTNET|8.0"
```
*Replace [yourname] with a unique identifier as the app name must be globally unique*

### Step 5: Deploy the Application

**Option A: Zip Deploy**
```powershell
# Publish the app
cd ConferenceHub
dotnet publish -c Release -o ./publish

# Create a zip file
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force

# Deploy to Azure
az webapp deployment source config-zip `
  --resource-group rg-conferencehub `
  --name conferencehub-demo-[yourname] `
  --src ./app.zip
```

**Option B: Local Git Deploy**
```powershell
# Get deployment credentials
az webapp deployment list-publishing-credentials `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub

# Set up local git deployment
az webapp deployment source config-local-git `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub

# Add Azure remote and push
git init
git add .
git commit -m "Initial commit"
git remote add azure <GIT_URL_FROM_ABOVE>
git push azure main
```

### Step 6: Browse the App
```powershell
az webapp browse `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub
```

## Option 2: Deploy via Visual Studio Code

### Step 1: Install Azure Extensions
- Install the "Azure App Service" extension in VS Code

### Step 2: Sign in to Azure
- Click the Azure icon in the Activity Bar
- Sign in to your Azure account

### Step 3: Deploy
1. Right-click on the `ConferenceHub` folder
2. Select "Deploy to Web App..."
3. Follow the prompts:
   - Select your subscription
   - Select "Create new Web App"
   - Enter a unique name
   - Select .NET 8 runtime
   - Choose a location

### Step 4: Configure and Browse
- Once deployed, VS Code will provide the URL
- Click to open in browser

## Option 3: Deploy via Azure Portal

### Step 1: Create App Service via Portal
1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource"
3. Search for "Web App"
4. Click "Create"
5. Fill in the details:
   - **Subscription**: Your subscription
   - **Resource Group**: Create new or use existing
   - **Name**: conferencehub-demo-[yourname]
   - **Publish**: Code
   - **Runtime stack**: .NET 8 (LTS)
   - **Operating System**: Linux or Windows
   - **Region**: Choose nearest region
   - **Pricing plan**: B1 (Basic)
6. Click "Review + Create" then "Create"

### Step 2: Deploy via FTP or Deployment Center
1. Navigate to your App Service
2. Go to "Deployment Center"
3. Choose deployment source:
   - **Local Git**: Follow git instructions
   - **GitHub**: Connect your repository
   - **FTP**: Use FTP credentials to upload published files

### Step 3: Manual Publish via ZIP
1. Publish the app locally:
   ```powershell
   dotnet publish -c Release -o ./publish
   ```
2. Zip the `publish` folder
3. In Azure Portal, go to App Service → Deployment Center
4. Use "FTPS credentials" or "ZIP Deploy" via Kudu (`https://<app-name>.scm.azurewebsites.net/ZipDeployUI`)

## Post-Deployment Configuration

### Configure Application Settings (Optional)
```powershell
az webapp config appsettings set `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub `
  --settings ASPNETCORE_ENVIRONMENT=Production
```

### Enable Application Insights (For Future Learning Paths)
```powershell
az monitor app-insights component create `
  --app conferencehub-insights `
  --location eastus `
  --resource-group rg-conferencehub `
  --application-type web

# Get instrumentation key
az monitor app-insights component show `
  --app conferencehub-insights `
  --resource-group rg-conferencehub `
  --query instrumentationKey
```

## Verify Deployment

### Check App Service Status
```powershell
az webapp show `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub `
  --query state
```

### View Logs
```powershell
az webapp log tail `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub
```

### Test the Application
1. Navigate to `https://conferencehub-demo-[yourname].azurewebsites.net`
2. Verify the home page loads
3. Test the Sessions page: `/sessions`
4. Test the Organizer dashboard: `/organizer`
5. Try registering for a session

## Troubleshooting

### App Doesn't Start
- Check logs: `az webapp log tail`
- Verify runtime stack matches .NET 8
- Check that `Data` folder exists and has read/write permissions

### 500 Error on Sessions Page
- Ensure `Data/sessions.json` was deployed
- Check file permissions in Kudu Console (`https://<app-name>.scm.azurewebsites.net`)

### Can't Save New Sessions
- App Service needs write permissions to `Data` folder
- Consider moving to Azure Storage or Database in future learning paths

## Cost Considerations

**Basic B1 Tier**: ~$13-15/month
- For demo purposes, you can use Free (F1) tier, but with limitations
- Stop the app when not in use to save costs
- Delete resources after demos

### Stop the App
```powershell
az webapp stop `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub
```

### Delete Resources
```powershell
az group delete --name rg-conferencehub --yes --no-wait
```

## Next Steps for Learning Path 2
In the next learning path, you'll enhance this app with:
- Azure Blob Storage for speaker slides
- Azure Table Storage for session metadata
- Azure Queue Storage for background processing

---
**Part of AZ-204: Developing Solutions for Microsoft Azure**

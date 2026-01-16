 # Learning Path 5: Docker & Azure Container Apps

## Overview
In this learning path, you'll containerize the ConferenceHub application using Docker and deploy it to Azure Container Apps, demonstrating modern cloud-native deployment patterns.

## What You'll Build
1. **Dockerfile**: Create a multi-stage Dockerfile for the web application
2. **Azure Container Registry**: Store container images in ACR
3. **Azure Container Apps**: Deploy the containerized application
4. **Environment Configuration**: Manage secrets and settings via environment variables

## Prerequisites
- Completed Learning Paths 1-4
- Docker Desktop installed
- Azure Container Registry or Docker Hub account
- Azure CLI with container extensions

---

## Part 1: Containerize the Web Application

### Step 1: Create Dockerfile

Create `ConferenceHub/Dockerfile`:
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ConferenceHub.csproj", "./"]
RUN dotnet restore "ConferenceHub.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "ConferenceHub.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConferenceHub.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ConferenceHub.dll"]
```

### Step 2: Create .dockerignore

Create `ConferenceHub/.dockerignore`:
```
**/.git
**/.gitignore
**/.vs
**/.vscode
**/bin
**/obj
**/*.user
**/node_modules
**/wwwroot/lib
*.md
.dockerignore
Dockerfile
docker-compose*.yml
```

### Step 3: Build Docker Image Locally

```powershell
cd ConferenceHub

# Build the image
docker build -t conferencehub:latest .

# Test the image locally
docker run -d -p 8080:8080 `
  -e CosmosDb__ConnectionString="YOUR_COSMOS_CONNECTION" `
  -e AzureStorage__ConnectionString="YOUR_STORAGE_CONNECTION" `
  -e AzureFunctions__SendConfirmationUrl="YOUR_FUNCTION_URL" `
  --name conferencehub-test `
  conferencehub:latest

# View logs
docker logs conferencehub-test

# Test the application
Start-Process "http://localhost:8080"

# Stop and remove container when done
docker stop conferencehub-test
docker rm conferencehub-test
```

---

## Part 2: Create Azure Container Registry

### Step 1: Create ACR

```powershell
# Create Azure Container Registry
az acr create `
  --name acrconferencehub `
  --resource-group rg-conferencehub `
  --sku Basic `
  --location eastus `
  --admin-enabled true

# Get ACR credentials
az acr credential show `
  --name acrconferencehub `
  --resource-group rg-conferencehub
```

### Step 2: Push Image to ACR

```powershell
# Login to ACR
az acr login --name acrconferencehub

# Tag the image
docker tag conferencehub:latest acrconferencehub.azurecr.io/conferencehub:v1
docker tag conferencehub:latest acrconferencehub.azurecr.io/conferencehub:latest

# Push to ACR
docker push acrconferencehub.azurecr.io/conferencehub:v1
docker push acrconferencehub.azurecr.io/conferencehub:latest

# Verify the image
az acr repository list --name acrconferencehub --output table
az acr repository show-tags --name acrconferencehub --repository conferencehub --output table
```

---

## Part 3: Deploy to Azure Container Apps

### Step 1: Create Container Apps Environment

```powershell
# Install/upgrade Container Apps extension
az extension add --name containerapp --upgrade

# Create Container Apps environment
az containerapp env create `
  --name env-conferencehub `
  --resource-group rg-conferencehub `
  --location eastus
```

### Step 2: Create Container App

```powershell
# Get connection strings for environment variables
$cosmosConnectionString = az cosmosdb keys list `
  --name cosmos-conferencehub `
  --resource-group rg-conferencehub `
  --type connection-strings `
  --query "connectionStrings[0].connectionString" `
  --output tsv

$storageConnectionString = az storage account show-connection-string `
  --name stconferencehub `
  --resource-group rg-conferencehub `
  --output tsv

# Get ACR credentials
$acrUsername = az acr credential show `
  --name acrconferencehub `
  --query "username" `
  --output tsv

$acrPassword = az acr credential show `
  --name acrconferencehub `
  --query "passwords[0].value" `
  --output tsv

# Create Container App
az containerapp create `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --environment env-conferencehub `
  --image acrconferencehub.azurecr.io/conferencehub:latest `
  --registry-server acrconferencehub.azurecr.io `
  --registry-username $acrUsername `
  --registry-password $acrPassword `
  --target-port 8080 `
  --ingress external `
  --min-replicas 1 `
  --max-replicas 3 `
  --cpu 0.5 `
  --memory 1.0Gi `
  --env-vars `
    "CosmosDb__ConnectionString=$cosmosConnectionString" `
    "CosmosDb__DatabaseName=ConferenceHubDB" `
    "AzureStorage__ConnectionString=$storageConnectionString" `
    "AzureFunctions__SendConfirmationUrl=https://func-conferencehub-az204reinke.azurewebsites.net/api/SendConfirmation" `
    "ASPNETCORE_ENVIRONMENT=Production"

# Get the application URL
az containerapp show `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --query "properties.configuration.ingress.fqdn" `
  --output tsv
```

---

## Part 4: Advanced Configuration

### Step 1: Add Secrets Management

```powershell
# Create secrets in Container App
az containerapp secret set `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --secrets `
    cosmosdb-connection="$cosmosConnectionString" `
    storage-connection="$storageConnectionString" `
    function-key="YOUR_FUNCTION_KEY"

# Update environment variables to use secrets
az containerapp update `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --set-env-vars `
    "CosmosDb__ConnectionString=secretref:cosmosdb-connection" `
    "AzureStorage__ConnectionString=secretref:storage-connection" `
    "AzureFunctions__FunctionKey=secretref:function-key"
```

### Step 2: Configure Scaling Rules

```powershell
# Add HTTP scaling rule
az containerapp update `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --min-replicas 1 `
  --max-replicas 5 `
  --scale-rule-name http-rule `
  --scale-rule-type http `
  --scale-rule-http-concurrency 50
```

### Step 3: Configure Health Probes

Update the Container App with health probes:
```powershell
az containerapp update `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --health-probe-type liveness `
  --health-probe-path "/health" `
  --health-probe-interval 30 `
  --health-probe-timeout 5
```

Add health endpoint to `Program.cs`:
```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
```

---

## Part 5: Alternative Deployment - App Service for Containers

### Option: Deploy to App Service for Containers

```powershell
# Create App Service Plan for Containers
az appservice plan create `
  --name plan-conferencehub-container `
  --resource-group rg-conferencehub `
  --is-linux `
  --sku B1

# Create Web App for Containers
az webapp create `
  --name conferencehub-container `
  --resource-group rg-conferencehub `
  --plan plan-conferencehub-container `
  --deployment-container-image-name acrconferencehub.azurecr.io/conferencehub:latest

# Configure ACR credentials
az webapp config container set `
  --name conferencehub-container `
  --resource-group rg-conferencehub `
  --docker-custom-image-name acrconferencehub.azurecr.io/conferencehub:latest `
  --docker-registry-server-url https://acrconferencehub.azurecr.io `
  --docker-registry-server-user $acrUsername `
  --docker-registry-server-password $acrPassword

# Configure app settings
az webapp config appsettings set `
  --name conferencehub-container `
  --resource-group rg-conferencehub `
  --settings `
    CosmosDb__ConnectionString="$cosmosConnectionString" `
    CosmosDb__DatabaseName="ConferenceHubDB" `
    AzureStorage__ConnectionString="$storageConnectionString" `
    WEBSITES_PORT=8080

# Enable continuous deployment
az webapp deployment container config `
  --name conferencehub-container `
  --resource-group rg-conferencehub `
  --enable-cd true
```

---

## Part 6: Docker Compose for Local Development

### Create docker-compose.yml

Create `docker-compose.yml` in the root directory:
```yaml
version: '3.8'

services:
  webapp:
    build:
      context: ./ConferenceHub
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - CosmosDb__ConnectionString=${COSMOS_CONNECTION_STRING}
      - CosmosDb__DatabaseName=ConferenceHubDB
      - AzureStorage__ConnectionString=${STORAGE_CONNECTION_STRING}
      - AzureFunctions__SendConfirmationUrl=http://functions:7071/api/SendConfirmation
    depends_on:
      - functions
    networks:
      - conferencehub-network

  functions:
    build:
      context: ./ConferenceHub.Functions
      dockerfile: Dockerfile
    ports:
      - "7071:7071"
    environment:
      - AzureWebJobsStorage=${STORAGE_CONNECTION_STRING}
      - CosmosDbConnectionString=${COSMOS_CONNECTION_STRING}
    networks:
      - conferencehub-network

networks:
  conferencehub-network:
    driver: bridge
```

### Create Dockerfile for Functions

Create `ConferenceHub.Functions/Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConferenceHub.Functions.csproj", "./"]
RUN dotnet restore "ConferenceHub.Functions.csproj"
COPY . .
RUN dotnet build "ConferenceHub.Functions.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConferenceHub.Functions.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
```

### Run with Docker Compose

```powershell
# Create .env file with connection strings
@"
COSMOS_CONNECTION_STRING=your-cosmos-connection-string
STORAGE_CONNECTION_STRING=your-storage-connection-string
"@ | Out-File -FilePath .env -Encoding utf8

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f webapp

# Stop all services
docker-compose down
```

---

## Part 7: CI/CD Pipeline with GitHub Actions

### Create GitHub Actions Workflow

Create `.github/workflows/deploy-container.yml`:
```yaml
name: Build and Deploy Container

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]
  workflow_dispatch:

env:
  ACR_NAME: acrconferencehub
  IMAGE_NAME: conferencehub
  CONTAINER_APP_NAME: app-conferencehub
  RESOURCE_GROUP: rg-conferencehub

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Log in to Azure Container Registry
      uses: azure/docker-login@v1
      with:
        login-server: ${{ env.ACR_NAME }}.azurecr.io
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}
    
    - name: Build and push Docker image
      working-directory: ./ConferenceHub
      run: |
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }} .
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest .
        docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}
        docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest
    
  deploy-to-container-apps:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    
    steps:
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Deploy to Container Apps
      uses: azure/CLI@v1
      with:
        inlineScript: |
          az containerapp update \
            --name ${{ env.CONTAINER_APP_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --image ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}
```

---

## Part 8: Monitoring and Logs

### View Container App Logs

```powershell
# Stream live logs
az containerapp logs show `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --follow

# View recent logs
az containerapp logs show `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --tail 100
```

### Enable Application Insights

```powershell
# Create Application Insights
az monitor app-insights component create `
  --app conferencehub-insights `
  --location eastus `
  --resource-group rg-conferencehub `
  --application-type web

# Get instrumentation key
$instrumentationKey = az monitor app-insights component show `
  --app conferencehub-insights `
  --resource-group rg-conferencehub `
  --query instrumentationKey `
  --output tsv

# Update Container App with App Insights
az containerapp update `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --set-env-vars "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$instrumentationKey"
```

---

## Part 9: Troubleshooting

### Debug Container Locally

```powershell
# Run container interactively
docker run -it --rm `
  -p 8080:8080 `
  --entrypoint /bin/bash `
  conferencehub:latest

# Inside container:
# dotnet ConferenceHub.dll
```

### Check Container App Status

```powershell
# Get replica status
az containerapp replica list `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --output table

# View revision history
az containerapp revision list `
  --name app-conferencehub `
  --resource-group rg-conferencehub `
  --output table

# Restart the app
az containerapp update `
  --name app-conferencehub `
  --resource-group rg-conferencehub
```

---

## Summary

You've successfully:
- ✅ Created a multi-stage Dockerfile for the application
- ✅ Built and tested container images locally
- ✅ Pushed images to Azure Container Registry
- ✅ Deployed to Azure Container Apps
- ✅ Configured environment variables and secrets
- ✅ Set up auto-scaling and health probes
- ✅ Created CI/CD pipeline with GitHub Actions

## Next Steps

In **Learning Path 6**, you'll:
- Implement **Microsoft Entra ID (Azure AD)** authentication
- Add role-based authorization (Organizer vs Attendee)
- Secure APIs with JWT tokens
- Protect Azure Functions with authentication

---

## Troubleshooting

### Image build fails
- Check Dockerfile syntax
- Verify base image compatibility
- Ensure all dependencies are restored

### Container won't start
- Check environment variables
- Review container logs
- Verify port configuration (8080)

### ACR authentication fails
- Verify ACR credentials
- Check if admin user is enabled
- Use managed identity for production

### Container App deployment fails
- Verify image exists in ACR
- Check resource quotas
- Review Container Apps environment status

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/05-Docker/azure-pipelines.yml`
- Bicep: `Learning Path/05-Docker/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `acrName`, `containerAppsEnvName`, `containerAppName`, `containerImage`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `functionAppName`
- Notes: The pipeline provisions ACR + Container Apps and uses `containerImage` (must already exist in ACR). It sets environment variables for Cosmos DB, Storage, and Functions.

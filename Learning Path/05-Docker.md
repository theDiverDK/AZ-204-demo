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

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
acrName="acrconferencehub$random"
containerAppsEnvName="env-conferencehub-$random"
containerAppName="app-conferencehub-$random"
containerImage="$acrName.azurecr.io/conferencehub:latest"
containerAppPlanName="plan-conferencehub-container-$random"
```

---

## Part 1: Containerize the Web Application

### Step 1: Create Dockerfile

Create `ConferenceHub/Dockerfile`:
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
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

```bash
cd ConferenceHub

# Build the image
docker build -t conferencehub:latest .

# Test the image locally
docker run -d -p 8080:8080 \
  -e CosmosDb__ConnectionString="YOUR_COSMOS_CONNECTION" \
  -e AzureStorage__ConnectionString="YOUR_STORAGE_CONNECTION" \
  -e AzureFunctions__SendConfirmationUrl="YOUR_FUNCTION_URL" \
  --name conferencehub-test \
  conferencehub:latest

# View logs
docker logs conferencehub-test

# Test the application
open "http://localhost:8080"

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
  --name $acrName `
  --resource-group $resourceGroupName `
  --sku Basic `
  --location $location `
  --admin-enabled true

# Get ACR credentials
az acr credential show `
  --name $acrName `
  --resource-group $resourceGroupName
```

```bash
# Create Azure Container Registry
az acr create \
  --name "$acrName" \
  --resource-group "$resourceGroupName" \
  --sku Basic \
  --location "$location" \
  --admin-enabled true

# Get ACR credentials
az acr credential show \
  --name "$acrName" \
  --resource-group "$resourceGroupName"
```

### Step 2: Push Image to ACR

```powershell
# Login to ACR
az acr login --name $acrName

# Tag the image
docker tag conferencehub:latest $acrName.azurecr.io/conferencehub:v1
docker tag conferencehub:latest $acrName.azurecr.io/conferencehub:latest

# Push to ACR
docker push $acrName.azurecr.io/conferencehub:v1
docker push $acrName.azurecr.io/conferencehub:latest

# Verify the image
az acr repository list --name $acrName --output table
az acr repository show-tags --name $acrName --repository conferencehub --output table
```

```bash
# Login to ACR
az acr login --name "$acrName" --resource-group $resourceGroupName

# Tag the image
docker tag conferencehub:latest "$acrName.azurecr.io/conferencehub:v1"
docker tag conferencehub:latest "$acrName.azurecr.io/conferencehub:latest"

# Push to ACR
docker push "$acrName.azurecr.io/conferencehub:v1"
docker push "$acrName.azurecr.io/conferencehub:latest"

# Verify the image
az acr repository list --name "$acrName" --output table
az acr repository show-tags --name "$acrName" --repository conferencehub --output table
```

---

## Part 3: Deploy to Azure Container Apps

### Step 1: Create Container Apps Environment

```powershell
# Install/upgrade Container Apps extension
az extension add --name containerapp --upgrade

# Create Container Apps environment
az containerapp env create `
  --name $containerAppsEnvName `
  --resource-group $resourceGroupName `
  --location $location
```

```bash
# Install/upgrade Container Apps extension
az extension add --name containerapp --upgrade

# Create Container Apps environment
az containerapp env create \
  --name "$containerAppsEnvName" \
  --resource-group "$resourceGroupName" \
  --location "$location"
```

### Step 2: Create Container App

```powershell
# Get connection strings for environment variables
$cosmosConnectionString = az cosmosdb keys list `
  --name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --type connection-strings `
  --query "connectionStrings[0].connectionString" `
  --output tsv

$storageConnectionString = az storage account show-connection-string `
  --name $storageAccountName `
  --resource-group $resourceGroupName `
  --output tsv

# Get ACR credentials
$acrUsername = az acr credential show `
  --name $acrName `
  --query "username" `
  --output tsv

$acrPassword = az acr credential show `
  --name $acrName `
  --query "passwords[0].value" `
  --output tsv

# Create Container App
az containerapp create `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --environment $containerAppsEnvName `
  --image $acrName.azurecr.io/conferencehub:latest `
  --registry-server $acrName.azurecr.io `
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
    "CosmosDb__DatabaseName=$cosmosDatabaseName" `
    "AzureStorage__ConnectionString=$storageConnectionString" `
    "AzureFunctions__SendConfirmationUrl=https://$functionAppName.azurewebsites.net/api/SendConfirmation" `
    "ASPNETCORE_ENVIRONMENT=Production"

# Get the application URL
az containerapp show `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --query "properties.configuration.ingress.fqdn" `
  --output tsv
```

```bash
# Get connection strings for environment variables
cosmosConnectionString=$(az cosmosdb keys list \
  --name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv)

storageConnectionString=$(az storage account show-connection-string \
  --name "$storageAccountName" \
  --resource-group "$resourceGroupName" \
  --output tsv)

# Get ACR credentials
acrUsername=$(az acr credential show \
  --name "$acrName" \
  --resource-group $resourceGroupName \
  --query "username" \
  --output tsv)

acrPassword=$(az acr credential show \
  --name "$acrName" \
  --resource-group $resourceGroupName \
  --query "passwords[0].value" \
  --output tsv)

Because this is on mac, build with:
If you’re already in ConferenceHub/:

  docker buildx build \
    --platform linux/amd64 \
    -t "$acrName.azurecr.io/conferencehub:latest" \
    --push \
    .

docker push "$acrName.azurecr.io/conferencehub:latest"

# Create Container App
az containerapp create \
  --name "$webAppName" \
  --resource-group "$resourceGroupName" \
  --environment "$containerAppsEnvName" \
  --image "$acrName.azurecr.io/conferencehub:latest" \
  --registry-server "$acrName.azurecr.io" \
  --registry-username "$acrUsername" \
  --registry-password "$acrPassword" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    "CosmosDb__ConnectionString=$cosmosConnectionString" \
    "CosmosDb__DatabaseName=$cosmosDatabaseName" \
    "AzureStorage__ConnectionString=$storageConnectionString" \
    "AzureFunctions__SendConfirmationUrl=https://$functionAppName.azurewebsites.net/api/SendConfirmation" \
    "ASPNETCORE_ENVIRONMENT=Production"

# Get the application URL
az containerapp show \
  --name "$webAppName" \
  --resource-group "$resourceGroupName" \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv
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

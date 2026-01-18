# Learning Path 8: Azure API Management

## Overview
In this learning path, you'll deploy Azure API Management (APIM) to expose and secure your ConferenceHub APIs with rate limiting, subscription keys, caching, and transformation policies.

## What You'll Build
1. **API Management Instance**: Central API gateway for all services
2. **API Products**: Different tiers (Free, Premium) with different quotas
3. **Rate Limiting & Throttling**: Protect backend services from overload
4. **Subscription Keys**: Secure API access
5. **Policies**: Request/response transformation and caching

## Prerequisites
- Completed Learning Path 1-7
- Azure API Management resource (can take 30-45 minutes to provision)
- Deployed Web App and Azure Functions

---

## Part 1: Create API Management Instance

Set variables used in the commands:
PowerShell:
```powershell
$RG_NAME="rg-conferencehub"
$LOCATION="swedencentral"
$APP_NAME="conferencehub-$RANDOM"
$FUNC_APP_NAME="func-conferencehub-$RANDOM"
$APIM_NAME="apim-conferencehub-$RANDOM"
$KEYVAULT_NAME="kv-conferencehub-$RANDOM"
$APPCONFIG_NAME="appconfig-conferencehub-$RANDOM"
```
Bash:
```bash
RG_NAME="rg-conferencehub"
LOCATION="swedencentral"
APP_NAME="conferencehub-$RANDOM"
FUNC_APP_NAME="func-conferencehub-$RANDOM"
APIM_NAME="apim-conferencehub-$RANDOM"
KEYVAULT_NAME="kv-conferencehub-$RANDOM"
APPCONFIG_NAME="appconfig-conferencehub-$RANDOM"
```

### Step 1: Provision APIM Instance

PowerShell:
```powershell
# Create API Management instance (this takes 30-45 minutes!)
az apim create `
  --name $APIM_NAME `
  --resource-group $RG_NAME `
  --location $LOCATION `
  --publisher-email soren@reinke.dk `
  --publisher-name "ConferenceHub" `
  --sku-name Developer `
  --no-wait

# Check provisioning status
az apim show `
  --name $APIM_NAME `
  --resource-group $RG_NAME `
  --query "provisioningState"

# Wait until status is "Succeeded" before continuing
# You can check status every few minutes
```
Bash:
```bash
# Create API Management instance (this takes 30-45 minutes!)
az apim create \
  --name $APIM_NAME \
  --resource-group $RG_NAME \
  --location $LOCATION \
  --publisher-email soren@reinke.dk \
  --publisher-name "ConferenceHub" \
  --sku-name Developer \
  --no-wait

# Check provisioning status
az apim show \
  --name $APIM_NAME \
  --resource-group $RG_NAME \
  --query "provisioningState"

# Wait until status is "Succeeded" before continuing
# You can check status every few minutes
```

Alternative: Use Azure Portal
1. Go to Azure Portal → Create a resource → API Management
2. Fill in: Name, Organization name, Administrator email
3. Select "Developer" tier (for learning)
4. Click "Review + create"

### Step 2: Get APIM Gateway URL

PowerShell:
```powershell
# Once provisioning is complete
$apimGatewayUrl = az apim show `
  --name $APIM_NAME `
  --resource-group $RG_NAME `
  --query "gatewayUrl" `
  --output tsv

Write-Host "APIM Gateway URL: $apimGatewayUrl"
```
Bash:
```bash
# Once provisioning is complete
apimGatewayUrl=$(az apim show \
  --name $APIM_NAME \
  --resource-group $RG_NAME \
  --query "gatewayUrl" \
  --output tsv)

echo "APIM Gateway URL: $apimGatewayUrl"
```

---

## Part 2: Import Web App API

### Step 1: Generate OpenAPI Specification

Install Swashbuckle for OpenAPI/Swagger support:
PowerShell:
```powershell
cd ConferenceHub
dotnet add package Swashbuckle.AspNetCore
```
Bash:
```bash
cd ConferenceHub
dotnet add package Swashbuckle.AspNetCore
```

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ... existing configuration ...

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConferenceHub API",
        Version = "v1",
        Description = "API for managing conference sessions and registrations",
        Contact = new OpenApiContact
        {
            Name = "ConferenceHub Support",
            Email = "support@conferencehub.com"
        }
    });

    // Add security definition for OAuth
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "Sign in" },
                    { "profile", "User profile" }
                }
            }
        }
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            new[] { "openid", "profile" }
        }
    });
});

// ... rest of builder configuration ...

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ConferenceHub API v1");
        options.RoutePrefix = "swagger";
    });
}

// ... rest of app configuration ...

app.Run();
```

Redeploy the application:
PowerShell:
```powershell
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deploy `
  --resource-group $RG_NAME `
  --name $APP_NAME `
  --src-path ./app.zip
  --type zip
```
Bash:
```bash
dotnet publish -c Release -o ./publish
cd publish
zip -r ../app.zip .
cd ..
az webapp deploy \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --src-path ./app.zip
  --type zip
```

### Step 2: Import API from OpenAPI Spec

PowerShell:
```powershell
# Import API from Swagger/OpenAPI endpoint
az apim api import `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --path "conferencehub" `
  --api-id "conferencehub-api" `
  --display-name "ConferenceHub API" `
  --specification-format OpenApi `
  --specification-url "https://$APP_NAME.azurewebsites.net/swagger/v1/swagger.json" `
  --protocols https

Write-Host "API imported successfully"
```
Bash:
```bash
# Import API from Swagger/OpenAPI endpoint
az apim api import \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --path "conferencehub" \
  --api-id "conferencehub-api" \
  --display-name "ConferenceHub API" \
  --specification-format OpenApi \
  --specification-url "https://$APP_NAME.azurewebsites.net/swagger/v1/swagger.json" \
  --protocols https

echo "API imported successfully"
```

### Step 3: Import Azure Functions API

PowerShell:
```powershell
# Get Function App host key
$functionAppKey = az functionapp keys list `
  --name $FUNC_APP_NAME `
  --resource-group $RG_NAME `
  --query "functionKeys.default" `
  --output tsv

# Import Function App
az apim api import `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --path "functions" `
  --api-id "functions-api" `
  --display-name "ConferenceHub Functions" `
  --service-url "https://$FUNC_APP_NAME.azurewebsites.net/api" `
  --specification-format OpenApiJson `
  --specification-path "functions-openapi.json"
```
Bash:
```bash
# Get Function App host key
functionAppKey=$(az functionapp keys list \
  --name $FUNC_APP_NAME \
  --resource-group $RG_NAME \
  --query "functionKeys.default" \
  --output tsv)

# Import Function App
az apim api import \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --path "functions" \
  --api-id "functions-api" \
  --display-name "ConferenceHub Functions" \
  --service-url "https://$FUNC_APP_NAME.azurewebsites.net/api" \
  --specification-format OpenApiJson \
  --specification-path "functions-openapi.json"
```

If you don't have OpenAPI spec for Functions, create manually:
PowerShell:
```powershell
# Create API manually
az apim api create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --api-id "functions-api" `
  --path "functions" `
  --display-name "ConferenceHub Functions" `
  --protocols https `
  --service-url "https://$FUNC_APP_NAME.azurewebsites.net/api"

# Add SendConfirmation operation
az apim api operation create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --api-id "functions-api" `
  --url-template "/SendConfirmation" `
  --method POST `
  --display-name "Send Confirmation Email"
```
Bash:
```bash
# Create API manually
az apim api create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --api-id "functions-api" \
  --path "functions" \
  --display-name "ConferenceHub Functions" \
  --protocols https \
  --service-url "https://$FUNC_APP_NAME.azurewebsites.net/api"

# Add SendConfirmation operation
az apim api operation create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --api-id "functions-api" \
  --url-template "/SendConfirmation" \
  --method POST \
  --display-name "Send Confirmation Email"
```

---

## Part 3: Create API Products and Subscriptions

### Step 1: Create Products

PowerShell:
```powershell
# Create Free tier product
az apim product create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "free-tier" `
  --product-name "Free Tier" `
  --description "Free tier with rate limits" `
  --subscription-required true `
  --approval-required false `
  --state published

# Create Premium tier product
az apim product create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "premium-tier" `
  --product-name "Premium Tier" `
  --description "Premium tier with higher limits" `
  --subscription-required true `
  --approval-required true `
  --state published

Write-Host "Products created"
```
Bash:
```bash
# Create Free tier product
az apim product create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "free-tier" \
  --product-name "Free Tier" \
  --description "Free tier with rate limits" \
  --subscription-required true \
  --approval-required false \
  --state published

# Create Premium tier product
az apim product create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "premium-tier" \
  --product-name "Premium Tier" \
  --description "Premium tier with higher limits" \
  --subscription-required true \
  --approval-required true \
  --state published

echo "Products created"
```

### Step 2: Associate APIs with Products

PowerShell:
```powershell
# Add ConferenceHub API to Free tier
az apim product api add `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "free-tier" `
  --api-id "conferencehub-api"

# Add Functions API to Free tier
az apim product api add `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "free-tier" `
  --api-id "functions-api"

# Add both APIs to Premium tier
az apim product api add `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "premium-tier" `
  --api-id "conferencehub-api"

az apim product api add `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "premium-tier" `
  --api-id "functions-api"
```
Bash:
```bash
# Add ConferenceHub API to Free tier
az apim product api add \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "free-tier" \
  --api-id "conferencehub-api"

# Add Functions API to Free tier
az apim product api add \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "free-tier" \
  --api-id "functions-api"

# Add both APIs to Premium tier
az apim product api add \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "premium-tier" \
  --api-id "conferencehub-api"

az apim product api add \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "premium-tier" \
  --api-id "functions-api"
```

### Step 3: Create Subscriptions

PowerShell:
```powershell
# Create subscription for testing
az apim subscription create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --subscription-id "test-free-sub" `
  --name "Test Free Subscription" `
  --scope "/products/free-tier" `
  --state active

# Get subscription keys
$subscriptionKey = az apim subscription show `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --subscription-id "test-free-sub" `
  --query "primaryKey" `
  --output tsv

Write-Host "Subscription Key: $subscriptionKey"
Write-Host "Save this key for testing!"
```
Bash:
```bash
# Create subscription for testing
az apim subscription create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --subscription-id "test-free-sub" \
  --name "Test Free Subscription" \
  --scope "/products/free-tier" \
  --state active

# Get subscription keys
subscriptionKey=$(az apim subscription show \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --subscription-id "test-free-sub" \
  --query "primaryKey" \
  --output tsv)

echo "Subscription Key: $subscriptionKey"
echo "Save this key for testing!"
```

---

## Part 4: Apply Rate Limiting and Throttling Policies

### Step 1: Add Rate Limit Policy to Free Tier

Create `free-tier-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
        <!-- Rate limit: 100 calls per hour -->
        <rate-limit calls="100" renewal-period="3600" />
        
        <!-- Quota: 1000 calls per day -->
        <quota calls="1000" renewal-period="86400" />
        
        <!-- Set backend URL -->
        <set-backend-service base-url="https://$APP_NAME.azurewebsites.net" />
        
        <!-- Add subscription key to header -->
        <set-header name="X-Subscription-Key" exists-action="override">
            <value>@(context.Subscription.Key)</value>
        </set-header>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
        <!-- Add CORS headers -->
        <cors>
            <allowed-origins>
                <origin>*</origin>
            </allowed-origins>
            <allowed-methods>
                <method>GET</method>
                <method>POST</method>
                <method>PUT</method>
                <method>DELETE</method>
            </allowed-methods>
            <allowed-headers>
                <header>*</header>
            </allowed-headers>
        </cors>
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

Apply policy:
PowerShell:
```powershell
az apim product policy create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "free-tier" `
  --policy-xml-path "free-tier-policy.xml"
```
Bash:
```bash
az apim product policy create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "free-tier" \
  --policy-xml-path "free-tier-policy.xml"
```

### Step 2: Add Premium Tier Policy

Create `premium-tier-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
        <!-- Rate limit: 500 calls per hour -->
        <rate-limit calls="500" renewal-period="3600" />
        
        <!-- Quota: 10000 calls per day -->
        <quota calls="10000" renewal-period="86400" />
        
        <set-backend-service base-url="https://$APP_NAME.azurewebsites.net" />
        
        <set-header name="X-Subscription-Key" exists-action="override">
            <value>@(context.Subscription.Key)</value>
        </set-header>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
        <cors>
            <allowed-origins>
                <origin>*</origin>
            </allowed-origins>
            <allowed-methods>
                <method>GET</method>
                <method>POST</method>
                <method>PUT</method>
                <method>DELETE</method>
            </allowed-methods>
            <allowed-headers>
                <header>*</header>
            </allowed-headers>
        </cors>
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

PowerShell:
```powershell
az apim product policy create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --product-id "premium-tier" `
  --policy-xml-path "premium-tier-policy.xml"
```
Bash:
```bash
az apim product policy create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --product-id "premium-tier" \
  --policy-xml-path "premium-tier-policy.xml"
```

---

## Part 5: Add Caching and Transformation Policies

### Step 1: Add Caching for GET Requests

Create `api-caching-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
        <!-- Check cache for GET requests -->
        <cache-lookup vary-by-developer="false" vary-by-developer-groups="false">
            <vary-by-header>Accept</vary-by-header>
            <vary-by-query-parameter>id</vary-by-query-parameter>
        </cache-lookup>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
        <!-- Cache responses for 5 minutes -->
        <cache-store duration="300" />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

Apply to specific operation:
PowerShell:
```powershell
az apim api operation policy create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --api-id "conferencehub-api" `
  --operation-id "Sessions_Index" `
  --policy-xml-path "api-caching-policy.xml"
```
Bash:
```bash
az apim api operation policy create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --api-id "conferencehub-api" \
  --operation-id "Sessions_Index" \
  --policy-xml-path "api-caching-policy.xml"
```

### Step 2: Add Response Transformation

Create `response-transformation-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
        <!-- Add custom headers -->
        <set-header name="X-Powered-By" exists-action="override">
            <value>ConferenceHub API Management</value>
        </set-header>
        <set-header name="X-Response-Time" exists-action="override">
            <value>@(context.Elapsed.TotalMilliseconds.ToString())</value>
        </set-header>
        
        <!-- Transform response body -->
        <set-body>@{
            var response = context.Response.Body.As<JObject>(preserveContent: true);
            response["apiVersion"] = "v1";
            response["timestamp"] = DateTime.UtcNow.ToString("o");
            return response.ToString();
        }</set-body>
    </outbound>
    <on-error>
        <base />
        <!-- Return custom error response -->
        <return-response>
            <set-status code="500" reason="Internal Server Error" />
            <set-header name="Content-Type" exists-action="override">
                <value>application/json</value>
            </set-header>
            <set-body>@{
                return new JObject(
                    new JProperty("error", new JObject(
                        new JProperty("code", "InternalError"),
                        new JProperty("message", context.LastError.Message),
                        new JProperty("timestamp", DateTime.UtcNow.ToString("o"))
                    ))
                ).ToString();
            }</set-body>
        </return-response>
    </on-error>
</policies>
```

---

## Part 6: Configure API Security

### Step 1: Validate JWT Tokens

Create `jwt-validation-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
        <!-- Validate JWT token -->
        <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Unauthorized">
            <openid-config url="https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0/.well-known/openid-configuration" />
            <audiences>
                <audience>YOUR_CLIENT_ID</audience>
            </audiences>
            <issuers>
                <issuer>https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0</issuer>
            </issuers>
            <required-claims>
                <claim name="roles" match="any">
                    <value>Organizer</value>
                    <value>Attendee</value>
                </claim>
            </required-claims>
        </validate-jwt>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

Apply to API:
PowerShell:
```powershell
az apim api policy create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --api-id "conferencehub-api" `
  --policy-xml-path "jwt-validation-policy.xml"
```
Bash:
```bash
az apim api policy create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --api-id "conferencehub-api" \
  --policy-xml-path "jwt-validation-policy.xml"
```

### Step 2: Add IP Filtering

Create `ip-filter-policy.xml`:
```xml
<policies>
    <inbound>
        <base />
        <!-- Allow only specific IP ranges -->
        <ip-filter action="allow">
            <address-range from="10.0.0.0" to="10.255.255.255" />
            <address-range from="172.16.0.0" to="172.31.255.255" />
            <address>203.0.113.5</address>
        </ip-filter>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

---

## Part 7: Update Web App to Use APIM

### Step 1: Update Configuration

Update `ConferenceHub/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "KeyVault": {
    "VaultUri": "https://$KEYVAULT_NAME.vault.azure.net/"
  },
  "AppConfiguration": {
    "Endpoint": "https://$APPCONFIG_NAME.azconfig.io"
  },
  "ApiManagement": {
    "GatewayUrl": "https://$APIM_NAME.azure-api.net",
    "SubscriptionKey": "YOUR_SUBSCRIPTION_KEY"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "https://$APIM_NAME.azure-api.net/functions/SendConfirmation",
    "FunctionKey": ""
  }
}
```

### Step 2: Create HTTP Client with Subscription Key

Create `Services/IApiManagementClient.cs`:
```csharp
namespace ConferenceHub.Services
{
    public interface IApiManagementClient
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
    }
}
```

Create `Services/ApiManagementClient.cs`:
```csharp
using Microsoft.Extensions.Configuration;

namespace ConferenceHub.Services
{
    public class ApiManagementClient : IApiManagementClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _subscriptionKey;

        public ApiManagementClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _subscriptionKey = configuration["ApiManagement:SubscriptionKey"]!;
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return await _httpClient.SendAsync(request);
        }
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddHttpClient<IApiManagementClient, ApiManagementClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiManagement:GatewayUrl"]!);
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", 
        builder.Configuration["ApiManagement:SubscriptionKey"]);
});
```

---

## Part 8: Test API Management

### Test 1: Call API with Subscription Key

PowerShell:
```powershell
# Set variables
$apimUrl = "https://$APIM_NAME.azure-api.net"
$subscriptionKey = "YOUR_SUBSCRIPTION_KEY"

# Test GET sessions
$response = Invoke-RestMethod `
  -Uri "$apimUrl/conferencehub/sessions" `
  -Method Get `
  -Headers @{
    "Ocp-Apim-Subscription-Key" = $subscriptionKey
  }

$response | ConvertTo-Json -Depth 10
```
Bash:
```bash
# Set variables
apimUrl="https://$APIM_NAME.azure-api.net"
subscriptionKey="YOUR_SUBSCRIPTION_KEY"

# Test GET sessions
curl -sS \
  -H "Ocp-Apim-Subscription-Key: $subscriptionKey" \
  "$apimUrl/conferencehub/sessions"
```

### Test 2: Test Rate Limiting

PowerShell:
```powershell
# Make 101 requests to trigger rate limit
for ($i = 1; $i -le 101; $i++) {
    try {
        $response = Invoke-RestMethod `
          -Uri "$apimUrl/conferencehub/sessions" `
          -Method Get `
          -Headers @{
            "Ocp-Apim-Subscription-Key" = $subscriptionKey
          }
        Write-Host "Request $i : Success"
    }
    catch {
        Write-Host "Request $i : Rate limit exceeded" -ForegroundColor Red
        Write-Host $_.Exception.Message
        break
    }
}
```
Bash:
```bash
# Make 101 requests to trigger rate limit
for i in $(seq 1 101); do
  status=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Ocp-Apim-Subscription-Key: $subscriptionKey" \
    "$apimUrl/conferencehub/sessions")
  if [ "$status" = "429" ]; then
    echo "Request $i : Rate limit exceeded ($status)"
    break
  fi
  echo "Request $i : Success ($status)"
done
```

### Test 3: Test Without Subscription Key

PowerShell:
```powershell
# This should fail with 401 Unauthorized
try {
    Invoke-RestMethod `
      -Uri "$apimUrl/conferencehub/sessions" `
      -Method Get
}
catch {
    Write-Host "Expected error: $($_.Exception.Message)" -ForegroundColor Yellow
}
```
Bash:
```bash
# This should fail with 401 Unauthorized
status=$(curl -s -o /dev/null -w "%{http_code}" \
  "$apimUrl/conferencehub/sessions")
echo "Status: $status (expected 401)"
```

### Test 4: Test Caching

PowerShell:
```powershell
# First request - should hit backend
Measure-Command {
    $response1 = Invoke-RestMethod `
      -Uri "$apimUrl/conferencehub/sessions/1" `
      -Method Get `
      -Headers @{
        "Ocp-Apim-Subscription-Key" = $subscriptionKey
      }
}

# Second request - should be cached (faster)
Measure-Command {
    $response2 = Invoke-RestMethod `
      -Uri "$apimUrl/conferencehub/sessions/1" `
      -Method Get `
      -Headers @{
        "Ocp-Apim-Subscription-Key" = $subscriptionKey
      }
}
```
Bash:
```bash
# First request - should hit backend
time curl -s -o /dev/null \
  -H "Ocp-Apim-Subscription-Key: $subscriptionKey" \
  "$apimUrl/conferencehub/sessions/1"

# Second request - should be cached (faster)
time curl -s -o /dev/null \
  -H "Ocp-Apim-Subscription-Key: $subscriptionKey" \
  "$apimUrl/conferencehub/sessions/1"
```

---

## Part 9: Configure Developer Portal

### Step 1: Enable Developer Portal

PowerShell:
```powershell
# The Developer Portal is enabled by default in APIM
# Access it at: https://$APIM_NAME.developer.azure-api.net

# Publish the portal
az apim api revision create `
  --resource-group $RG_NAME `
  --service-name $APIM_NAME `
  --api-id "conferencehub-api" `
  --api-revision "1"
```
Bash:
```bash
# The Developer Portal is enabled by default in APIM
# Access it at: https://$APIM_NAME.developer.azure-api.net

# Publish the portal
az apim api revision create \
  --resource-group $RG_NAME \
  --service-name $APIM_NAME \
  --api-id "conferencehub-api" \
  --api-revision "1"
```

### Step 2: Customize Developer Portal

1. Navigate to: https://apim-conferencehub.developer.azure-api.net/admin
2. Click "Customize" to edit the portal
3. Add branding, documentation, and API descriptions
4. Click "Publish" when done

### Step 3: Create API Documentation

Add XML documentation to controllers:
```csharp
/// <summary>
/// Get all conference sessions
/// </summary>
/// <returns>List of sessions</returns>
/// <response code="200">Returns the list of sessions</response>
[HttpGet]
[ProducesResponseType(typeof(List<Session>), 200)]
public async Task<IActionResult> Index()
{
    var sessions = await _dataService.GetSessionsAsync();
    return View(sessions);
}
```

Update `Program.cs` to include XML comments:
```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { /* ... */ });
    
    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
```

Update `.csproj` to generate XML documentation:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

---

## Part 10: Monitor and Analytics

### View API Analytics

PowerShell:
```powershell
# Get API usage metrics
az monitor metrics list `
  --resource "/subscriptions/YOUR_SUB_ID/resourceGroups/$RG_NAME/providers/Microsoft.ApiManagement/service/$APIM_NAME" `
  --metric "Requests" `
  --start-time 2024-01-01T00:00:00Z `
  --end-time 2024-12-31T23:59:59Z `
  --interval PT1H

# View in Azure Portal
# Navigate to: APIM → Analytics → Reports
```
Bash:
```bash
# Get API usage metrics
az monitor metrics list \
  --resource "/subscriptions/YOUR_SUB_ID/resourceGroups/$RG_NAME/providers/Microsoft.ApiManagement/service/$APIM_NAME" \
  --metric "Requests" \
  --start-time 2024-01-01T00:00:00Z \
  --end-time 2024-12-31T23:59:59Z \
  --interval PT1H

# View in Azure Portal
# Navigate to: APIM → Analytics → Reports
```

View real-time metrics:
1. Azure Portal → API Management
2. Navigate to "Analytics" blade
3. View requests, response times, errors
4. Filter by API, operation, product

---

## Summary

You've successfully:
- ✅ Deployed Azure API Management instance
- ✅ Imported Web App and Functions APIs
- ✅ Created API Products (Free and Premium tiers)
- ✅ Implemented rate limiting and quotas
- ✅ Added subscription key authentication
- ✅ Configured response caching
- ✅ Applied request/response transformation policies
- ✅ Secured APIs with JWT validation
- ✅ Customized Developer Portal
- ✅ Monitored API usage and analytics

## Next Steps

In **Learning Path 9**, you'll:
- Implement **Azure Event Grid** for blob upload notifications
- Use **Azure Event Hub** for streaming session feedback data
- Create event-driven workflows
- Process events in real-time

---

## Troubleshooting

### APIM provisioning takes too long
- Developer tier typically takes 30-45 minutes
- Check provisioning status with `az apim show`
- Use `--no-wait` flag and continue with other tasks

### Cannot import API from OpenAPI spec
- Verify Swagger endpoint is publicly accessible
- Check OpenAPI specification is valid
- Try importing manually through Azure Portal

### Rate limit not working
- Verify policy is applied at correct scope (Product, API, Operation)
- Check renewal period is correct (in seconds)
- Clear cache and test with new subscription key

### Subscription key not working
- Verify product requires subscription
- Check subscription is in "active" state
- Use correct header: `Ocp-Apim-Subscription-Key`
- Ensure subscription is linked to correct product

### Caching not working
- Check cache-lookup and cache-store policies are both applied
- Verify external cache is configured (for distributed caching)
- Check vary-by parameters match your request
- Review cache duration and TTL settings

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/08-APIM/azure-pipelines.yml`
- Bicep: `Learning Path/08-APIM/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `functionAppName`, `keyVaultUri`, `appConfigEndpoint`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`, `apiManagementName`, `apiManagementPublisherEmail`, `apiManagementPublisherName`, `ApiManagementSubscriptionKey`
- Notes: The pipeline provisions APIM and configures `ApiManagement__GatewayUrl` and `ApiManagement__SubscriptionKey` for the web app.

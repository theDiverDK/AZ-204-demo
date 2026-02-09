$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $true
$script_dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo_root = (Resolve-Path (Join-Path $script_dir "../..")).Path
. "$repo_root/tools/variables.ps1"
$app_insights_name = ""
$app_insights_connection_string = ""
$container_app_exists = ""
$key_vault_uri = ""
$project_dir = "$repo_root/ConferenceHub"
$web_publish_dir = "$repo_root/.deploy/lp11/web/publish"
$web_package_path = "$repo_root/.deploy/lp11/web/app.zip"
$functions_project_path = "$repo_root/$functions_project_dir/$functions_project_name.csproj"
$functions_publish_path = "$repo_root/.deploy/lp11/functions/publish"
$functions_zip_path = "$repo_root/.deploy/lp11/functions/functions.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationZipPath
    )

    if (Test-Path -LiteralPath $DestinationZipPath) {
        Remove-Item -LiteralPath $DestinationZipPath -Force
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SourceDirectory,
        $DestinationZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
}
if (-not [string]::IsNullOrEmpty($app_insights_component_name)) {
    $app_insights_name = "$app_insights_component_name"
} else {
    $app_insights_name = "$(az resource list  --resource-group `"$resource_group_name`"  --resource-type `"microsoft.insights/components`"  --query `"[0].name`"  -o tsv)"
}
if ([string]::IsNullOrEmpty($app_insights_name)) {
    Write-Host "Could not find an existing Application Insights component in '$resource_group_name'."
    exit 1
}
$app_insights_connection_string = "$(az resource show  --name `"$app_insights_name`"  --resource-group `"$resource_group_name`"  --resource-type `"microsoft.insights/components`"  --query `"properties.ConnectionString`"  -o tsv)"
if ([string]::IsNullOrEmpty($app_insights_connection_string)) {
    $app_insights_connection_string = "$(az resource show  --name `"$app_insights_name`"  --resource-group `"$resource_group_name`"  --resource-type `"microsoft.insights/components`"  --query `"properties.connectionString`"  -o tsv)"
}
if ([string]::IsNullOrEmpty($app_insights_connection_string)) {
    Write-Host "Could not read connection string from Application Insights '$app_insights_name'."
    exit 1
}
$key_vault_uri = "$(az keyvault show  --name `"$key_vault_name`"  --resource-group `"$resource_group_name`"  --query properties.vaultUri  -o tsv)"
az webapp config appsettings set  --resource-group "$resource_group_name"  --name "$web_app_name"  --settings  APPLICATIONINSIGHTS_CONNECTION_STRING="$app_insights_connection_string"  ApplicationInsights__ConnectionString="$app_insights_connection_string"  ApplicationInsights__EnableAdaptiveSampling=false  WEBSITE_CLOUD_ROLENAME=conferencehub-web  KeyVaultTelemetry__VaultUri="$key_vault_uri"  KeyVaultTelemetry__ProbeSecretName="$kv_secret_cosmos_key_name"
az functionapp config appsettings set  --resource-group "$resource_group_name"  --name "$function_app_name"  --settings  APPLICATIONINSIGHTS_CONNECTION_STRING="$app_insights_connection_string"  AzureFunctionsJobHost__logging__applicationInsights__samplingSettings__isEnabled=false  WEBSITE_CLOUD_ROLENAME=conferencehub-functions
$container_app_exists = "$(az webapp list  --resource-group `"$resource_group_name`"  --query `"[?name=='$container_web_app_name'].name | [0]`"  -o tsv)"
if (-not [string]::IsNullOrEmpty($container_app_exists)) {
    az webapp config appsettings set  --resource-group "$resource_group_name"  --name "$container_web_app_name"  --settings  APPLICATIONINSIGHTS_CONNECTION_STRING="$app_insights_connection_string"  ApplicationInsights__ConnectionString="$app_insights_connection_string"  ApplicationInsights__EnableAdaptiveSampling=false  WEBSITE_CLOUD_ROLENAME=conferencehub-container  KeyVaultTelemetry__VaultUri="$key_vault_uri"  KeyVaultTelemetry__ProbeSecretName="$kv_secret_cosmos_key_name"
}
# --------------------
if (Test-Path "$functions_publish_path") { Remove-Item -Recurse -Force "$functions_publish_path" }
New-Item -ItemType Directory -Path "$functions_publish_path" -Force | Out-Null
dotnet publish "$functions_project_path" -c Release -o "$functions_publish_path"
New-ZipFromDirectory -SourceDirectory "$functions_publish_path" -DestinationZipPath "$functions_zip_path"
az functionapp deployment source config-zip  --resource-group "$resource_group_name"  --name "$function_app_name"  --src "$functions_zip_path"
if (Test-Path "$web_publish_dir") { Remove-Item -Recurse -Force "$web_publish_dir" }
New-Item -ItemType Directory -Path "$web_publish_dir" -Force | Out-Null
dotnet publish "$project_dir/ConferenceHub.csproj" -c Release -o "$web_publish_dir"
New-ZipFromDirectory -SourceDirectory "$web_publish_dir" -DestinationZipPath "$web_package_path"
az webapp deploy  --resource-group "$resource_group_name"  --name "$web_app_name"  --src-path "$web_package_path"  --type zip
az functionapp restart  --resource-group "$resource_group_name"  --name "$function_app_name"
az webapp restart  --resource-group "$resource_group_name"  --name "$web_app_name"
if (-not [string]::IsNullOrEmpty($container_app_exists)) {
    az webapp restart  --resource-group "$resource_group_name"  --name "$container_web_app_name"
}
az webapp browse  --resource-group "$resource_group_name"  --name "$web_app_name"

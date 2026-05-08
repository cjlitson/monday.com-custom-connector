[CmdletBinding()]
param(
    [string]$SwaggerPath = "../connector/apiDefinition.swagger.json"
)

$ErrorActionPreference = "Stop"

$resolvedPath = Resolve-Path -Path $SwaggerPath
Write-Host "Validating $resolvedPath"

try {
    $jsonText = Get-Content -Path $resolvedPath -Raw
    $swagger = $jsonText | ConvertFrom-Json -Depth 100
}
catch {
    Write-Error "The OpenAPI file is not valid JSON. $($_.Exception.Message)"
    exit 1
}

if ($swagger.swagger -ne "2.0") {
    Write-Error "Expected Swagger/OpenAPI version 2.0 but found '$($swagger.swagger)'."
    exit 1
}

if (-not $swagger.info.title) {
    Write-Error "Missing info.title."
    exit 1
}

if ($swagger.host -ne "api.monday.com") {
    Write-Error "Expected host to be api.monday.com but found '$($swagger.host)'."
    exit 1
}

if ($swagger.basePath -ne "/v2") {
    Write-Error "Expected basePath to be /v2 but found '$($swagger.basePath)'."
    exit 1
}

if (-not $swagger.securityDefinitions.api_key) {
    Write-Error "Missing api_key security definition."
    exit 1
}

if ($swagger.securityDefinitions.api_key.name -ne "Authorization") {
    Write-Error "Expected api_key security header name to be Authorization."
    exit 1
}

$operationIds = New-Object System.Collections.Generic.List[string]
foreach ($pathProperty in $swagger.paths.PSObject.Properties) {
    foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
        if ($methodProperty.Value.operationId) {
            $operationIds.Add([string]$methodProperty.Value.operationId)
        }
    }
}
if ($swagger.'x-ms-paths') {
    foreach ($pathProperty in $swagger.'x-ms-paths'.PSObject.Properties) {
        foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
            if ($methodProperty.Value.operationId) {
                $operationIds.Add([string]$methodProperty.Value.operationId)
            }
        }
    }
}

$requiredOperationIds = @(
    "GetMondayItemDetails",
    "CreateMondayItemUpdate",
    "ChangeMondayStatus",
    "ChangeMondayColumnValue",
    "CreateMondayItem"
)

foreach ($operationId in $requiredOperationIds) {
    if (-not $operationIds.Contains($operationId)) {
        Write-Error "Missing operationId '$operationId'."
        exit 1
    }
}

Write-Host "OpenAPI JSON is valid, uses Swagger 2.0, and contains all required monday.com actions."

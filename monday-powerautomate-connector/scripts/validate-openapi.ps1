[CmdletBinding()]
param(
    [string]$SwaggerPath = "../connector/apiDefinition.swagger.json",
    [string]$ApiPropertiesPath = "../connector/apiProperties.json"
)

$ErrorActionPreference = "Stop"

$expectedOperationIds = @(
    "GetMondayItemDetails",
    "CreateMondayItemUpdate",
    "ChangeMondayStatus",
    "ChangeMondayColumnValue",
    "CreateMondayItem"
)

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

try {
    $apiPropertiesText = Get-Content -Path (Resolve-Path -Path $ApiPropertiesPath) -Raw
    $apiProperties = $apiPropertiesText | ConvertFrom-Json -Depth 100
}
catch {
    Write-Error "The API properties file is not valid JSON. $($_.Exception.Message)"
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

if ($swagger.'x-ms-paths') {
    Write-Error "The primary connector must not use x-ms-paths. Keep x-ms-paths definitions under connector/experimental/ only."
    exit 1
}

$operationIds = New-Object System.Collections.Generic.List[string]
$postPaths = New-Object System.Collections.Generic.List[string]
foreach ($pathProperty in $swagger.paths.PSObject.Properties) {
    foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
        if ($methodProperty.Name -eq "post") {
            $postPaths.Add([string]$pathProperty.Name)
        }
        if ($methodProperty.Value.operationId) {
            $operationIds.Add([string]$methodProperty.Value.operationId)
        }
    }
}

$duplicateOperationIds = $operationIds | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicateOperationIds) {
    Write-Error "Duplicate operationIds found: $(($duplicateOperationIds | ForEach-Object { $_.Name }) -join ', ')."
    exit 1
}

$duplicatePostPaths = $postPaths | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicatePostPaths) {
    Write-Error "Duplicate POST paths found: $(($duplicatePostPaths | ForEach-Object { $_.Name }) -join ', ')."
    exit 1
}

$missingFromSwagger = $expectedOperationIds | Where-Object { $operationIds -notcontains $_ }
$unexpectedInSwagger = $operationIds | Where-Object { $expectedOperationIds -notcontains $_ }
if ($missingFromSwagger -or $unexpectedInSwagger) {
    Write-Error "Swagger operationIds did not match expected friendly actions. Missing: $($missingFromSwagger -join ', '); Unexpected: $($unexpectedInSwagger -join ', ')."
    exit 1
}

$scriptOperations = @($apiProperties.properties.scriptOperations)
$missingScriptOps = $expectedOperationIds | Where-Object { $scriptOperations -notcontains $_ }
$unexpectedScriptOps = $scriptOperations | Where-Object { $expectedOperationIds -notcontains $_ }
if ($missingScriptOps -or $unexpectedScriptOps) {
    Write-Error "apiProperties scriptOperations did not match swagger operationIds. Missing: $($missingScriptOps -join ', '); Unexpected: $($unexpectedScriptOps -join ', ')."
    exit 1
}

Write-Host "OpenAPI and apiProperties JSON are valid. Friendly operationIds are unique, scriptOperations match, POST paths are unique, and the primary definition does not use x-ms-paths."

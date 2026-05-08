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
    "CreateMondayItem",
    "ListMondayWorkspaces",
    "ListMondayBoards",
    "ListMondayBoardGroups",
    "ListMondayBoardColumns",
    "ListMondayBoardItems",
    "ListMondayStatusLabels",
    "ChangeMondayDateColumn",
    "ChangeMondayTextColumn",
    "ChangeMondayNumberColumn",
    "CreateMondaySubitem",
    "GetMondaySubitems",
    "GetMondaySubitemDetails",
    "ChangeMondaySubitemColumnValue"
)

$resolvedPath = Resolve-Path -Path $SwaggerPath
$resolvedApiPropertiesPath = Resolve-Path -Path $ApiPropertiesPath
$repoRoot = Resolve-Path -Path (Join-Path $PSScriptRoot "../../")
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
    $apiPropertiesText = Get-Content -Path $resolvedApiPropertiesPath -Raw
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
$missingScriptOps = $operationIds | Where-Object { $scriptOperations -notcontains $_ }
$unexpectedScriptOps = $scriptOperations | Where-Object { $operationIds -notcontains $_ }
if ($missingScriptOps -or $unexpectedScriptOps) {
    Write-Error "apiProperties scriptOperations did not match swagger operationIds. Missing: $($missingScriptOps -join ', '); Unexpected: $($unexpectedScriptOps -join ', ')."
    exit 1
}

function Get-ExtensionOperationIds {
    param([object]$Node)
    $ids = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Node) { return $ids }
    if ($Node -is [System.Array]) {
        foreach ($item in $Node) { $ids.AddRange((Get-ExtensionOperationIds -Node $item)) }
        return $ids
    }
    if ($Node.PSObject.Properties) {
        foreach ($prop in $Node.PSObject.Properties) {
            if (($prop.Name -eq "x-ms-dynamic-values" -or $prop.Name -eq "x-ms-dynamic-list") -and $prop.Value.operationId) {
                $ids.Add([string]$prop.Value.operationId)
            }
            $ids.AddRange((Get-ExtensionOperationIds -Node $prop.Value))
        }
    }
    return $ids
}

$dynamicOperationIds = Get-ExtensionOperationIds -Node $swagger
$missingDynamicTargets = $dynamicOperationIds | Sort-Object -Unique | Where-Object { $operationIds -notcontains $_ }
if ($missingDynamicTargets) {
    Write-Error "Dynamic dropdown operationIds reference missing operations: $($missingDynamicTargets -join ', ')."
    exit 1
}

$secretPatterns = @(
    'monday[_-]?api[_-]?token\s*[:=]\s*[A-Za-z0-9_\-]{20,}',
    'Authorization\s*[:=]\s*Bearer\s+[A-Za-z0-9_\-\.]{20,}',
    'eyJ[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}'
)
$filesToScan = Get-ChildItem -Path $repoRoot -Recurse -File -Force |
    Where-Object { $_.FullName -notmatch "[\\/]\.git[\\/]" -and $_.FullName -notmatch "[\\/]node_modules[\\/]" }
foreach ($file in $filesToScan) {
    $text = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $secretPatterns) {
        if ($text -match $pattern) {
            Write-Error "Potential token/secret pattern found in $($file.FullName)."
            exit 1
        }
    }
}

Write-Host "OpenAPI and apiProperties JSON are valid. Swagger 2.0 is preserved, x-ms-paths is absent, operationIds and POST paths are unique, scriptOperations cover every scripted action, dynamic dropdown references resolve, and no token/secret patterns were found."

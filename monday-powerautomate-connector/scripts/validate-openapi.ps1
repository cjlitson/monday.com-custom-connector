[CmdletBinding()]
param(
    [string]$SwaggerPath = "../connector/apiDefinition.swagger.json",
    [string]$ApiPropertiesPath = "../connector/apiProperties.json"
)

$ErrorActionPreference = "Stop"

$expectedOperationIds = @(
    "GetMondayItemDetails",
    "GetMondayItemColumnValue",
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

$userFacingOperationIds = @(
    "GetMondayItemDetails",
    "GetMondayItemColumnValue",
    "CreateMondayItemUpdate",
    "ChangeMondayStatus",
    "ChangeMondayDateColumn",
    "ChangeMondayTextColumn",
    "ChangeMondayNumberColumn",
    "CreateMondayItem",
    "CreateMondaySubitem",
    "GetMondaySubitems",
    "GetMondaySubitemDetails"
)

$helperOperationIds = @(
    "ListMondayWorkspaces",
    "ListMondayBoards",
    "ListMondayBoardGroups",
    "ListMondayBoardColumns",
    "ListMondayBoardItems",
    "ListMondayStatusLabels"
)

$rawJsonOperationIds = @(
    "ChangeMondayColumnValue",
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

if ($null -ne $swagger.PSObject.Properties["x-ms-paths"]) {
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

function Find-JsonKeyOccurrences {
    param(
        [object]$Node,
        [string[]]$KeyNames,
        [string]$Path = "root"
    )

    $matches = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Node) { return $matches }

    if ($Node -is [System.Array]) {
        for ($i = 0; $i -lt $Node.Count; $i++) {
            $matches.AddRange((Find-JsonKeyOccurrences -Node $Node[$i] -KeyNames $KeyNames -Path "$Path[$i]"))
        }
        return $matches
    }

    if ($Node -isnot [pscustomobject]) { return $matches }

    foreach ($prop in $Node.PSObject.Properties) {
        $currentPath = "$Path/$($prop.Name)"
        if ($KeyNames -contains $prop.Name) {
            $matches.Add($currentPath)
        }
        $matches.AddRange((Find-JsonKeyOccurrences -Node $prop.Value -KeyNames $KeyNames -Path $currentPath))
    }

    return $matches
}

$forbiddenExtensionKeys = @("x-ms-dynamic-values", "x-ms-dynamic-list", "x-ms-paths")
$forbiddenExtensionOccurrences = Find-JsonKeyOccurrences -Node $swagger -KeyNames $forbiddenExtensionKeys
if ($forbiddenExtensionOccurrences) {
    Write-Error "Forbidden primary Swagger extension keys found: $($forbiddenExtensionOccurrences -join '; ')."
    exit 1
}

function Get-EmptyRequiredArrayErrors {
    param(
        [object]$Node,
        [string]$Path = "root"
    )

    $errors = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Node) { return $errors }

    if ($Node -is [System.Array]) {
        for ($i = 0; $i -lt $Node.Count; $i++) {
            $errors.AddRange((Get-EmptyRequiredArrayErrors -Node $Node[$i] -Path "$Path[$i]"))
        }
        return $errors
    }

    if ($Node -isnot [pscustomobject]) { return $errors }

    foreach ($prop in $Node.PSObject.Properties) {
        $currentPath = "$Path/$($prop.Name)"
        if ($prop.Name -eq "required" -and $prop.Value -is [System.Array] -and $prop.Value.Count -eq 0) {
            $errors.Add($currentPath)
        }
        $errors.AddRange((Get-EmptyRequiredArrayErrors -Node $prop.Value -Path $currentPath))
    }

    return $errors
}

$emptyRequiredArrays = Get-EmptyRequiredArrayErrors -Node $swagger
if ($emptyRequiredArrays) {
    Write-Error "Empty required arrays found in primary Swagger: $($emptyRequiredArrays -join '; '). Remove required when no fields are required."
    exit 1
}

$getItemDetailsResponseRef = $swagger.paths."/get-item-details".post.responses."200".schema."`$ref"
if ($getItemDetailsResponseRef -ne "#/definitions/GetMondayItemDetailsResponse") {
    Write-Error "GetMondayItemDetails 200 response must reference #/definitions/GetMondayItemDetailsResponse but found '$getItemDetailsResponseRef'."
    exit 1
}

$getItemDetailsResponse = $swagger.definitions.GetMondayItemDetailsResponse
if (-not $getItemDetailsResponse) {
    Write-Error "Missing GetMondayItemDetailsResponse definition."
    exit 1
}

$expectedGetItemDetailsResponseProperties = @(
    "success",
    "message",
    "itemId",
    "itemName",
    "boardId",
    "boardName",
    "groupId",
    "groupName",
    "parentItemId",
    "parentItemName",
    "columnValues",
    "columnValuesTextSummary",
    "columnValuesHtmlTable",
    "columnValuesJson",
    "rawResponseJson"
)

$missingGetItemDetailsResponseSummaries = New-Object System.Collections.Generic.List[string]
foreach ($propertyName in $expectedGetItemDetailsResponseProperties) {
    $property = $getItemDetailsResponse.properties.PSObject.Properties[$propertyName]
    if (-not $property) {
        $missingGetItemDetailsResponseSummaries.Add("$propertyName missing from GetMondayItemDetailsResponse")
    }
    elseif (-not $property.Value."x-ms-summary") {
        $missingGetItemDetailsResponseSummaries.Add("$propertyName missing x-ms-summary")
    }
}

if ($missingGetItemDetailsResponseSummaries.Count -gt 0) {
    Write-Error "GetMondayItemDetailsResponse user-facing property summary errors: $($missingGetItemDetailsResponseSummaries -join '; ')."
    exit 1
}

$getItemColumnValueOperation = $swagger.paths."/get-item-column-value".post
if (-not $getItemColumnValueOperation) {
    Write-Error "Missing GetMondayItemColumnValue operation at POST /get-item-column-value."
    exit 1
}

if ($getItemColumnValueOperation.operationId -ne "GetMondayItemColumnValue") {
    Write-Error "POST /get-item-column-value must use operationId GetMondayItemColumnValue."
    exit 1
}

$getItemColumnValueResponseRef = $getItemColumnValueOperation.responses."200".schema."`$ref"
if ($getItemColumnValueResponseRef -ne "#/definitions/GetMondayItemColumnValueResponse") {
    Write-Error "GetMondayItemColumnValue 200 response must reference #/definitions/GetMondayItemColumnValueResponse but found '$getItemColumnValueResponseRef'."
    exit 1
}

$getItemColumnValueResponse = $swagger.definitions.GetMondayItemColumnValueResponse
if (-not $getItemColumnValueResponse) {
    Write-Error "Missing GetMondayItemColumnValueResponse definition."
    exit 1
}

$expectedGetItemColumnValueResponseProperties = @(
    "success",
    "message",
    "itemId",
    "columnId",
    "columnTitle",
    "columnType",
    "columnText",
    "columnValueJson",
    "rawColumnJson",
    "rawResponseJson"
)

$missingGetItemColumnValueResponseSummaries = New-Object System.Collections.Generic.List[string]
foreach ($propertyName in $expectedGetItemColumnValueResponseProperties) {
    $property = $getItemColumnValueResponse.properties.PSObject.Properties[$propertyName]
    if (-not $property) {
        $missingGetItemColumnValueResponseSummaries.Add("$propertyName missing from GetMondayItemColumnValueResponse")
    }
    elseif (-not $property.Value."x-ms-summary") {
        $missingGetItemColumnValueResponseSummaries.Add("$propertyName missing x-ms-summary")
    }
}

if ($missingGetItemColumnValueResponseSummaries.Count -gt 0) {
    Write-Error "GetMondayItemColumnValueResponse user-facing property summary errors: $($missingGetItemColumnValueResponseSummaries -join '; ')."
    exit 1
}

$columnValueSummary = $swagger.definitions.MondayColumnValueSummary
if (-not $columnValueSummary) {
    Write-Error "Missing MondayColumnValueSummary definition."
    exit 1
}

foreach ($propertyName in @("columnId", "columnTitle", "columnType", "text", "valueJson")) {
    $property = $columnValueSummary.properties.PSObject.Properties[$propertyName]
    if (-not $property -or -not $property.Value."x-ms-summary") {
        Write-Error "MondayColumnValueSummary property '$propertyName' is missing or missing x-ms-summary."
        exit 1
    }
}

$bodyParametersMissingSummary = New-Object System.Collections.Generic.List[string]
$visibilityErrors = New-Object System.Collections.Generic.List[string]
foreach ($pathProperty in $swagger.paths.PSObject.Properties) {
    foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
        $operation = $methodProperty.Value
        $operationId = [string]$operation.operationId
        if (-not $operationId) { continue }

        $expectedVisibility = $null
        if ($userFacingOperationIds -contains $operationId) {
            $expectedVisibility = "important"
        }
        elseif (($helperOperationIds -contains $operationId) -or ($rawJsonOperationIds -contains $operationId)) {
            $expectedVisibility = "advanced"
        }

        $actualVisibility = $operation."x-ms-visibility"
        if ($expectedVisibility -and $actualVisibility -ne $expectedVisibility) {
            $visibilityErrors.Add("$operationId expected x-ms-visibility '$expectedVisibility' but found '$actualVisibility'")
        }

        foreach ($parameter in @($operation.parameters)) {
            if ($parameter.in -eq "body" -and $parameter.name -eq "body" -and -not $parameter."x-ms-summary") {
                $bodyParametersMissingSummary.Add("$operationId at $($pathProperty.Name) $($methodProperty.Name)")
            }
        }
    }
}

if ($bodyParametersMissingSummary.Count -gt 0) {
    Write-Error "Body parameters missing x-ms-summary: $($bodyParametersMissingSummary -join '; ')."
    exit 1
}

if ($visibilityErrors.Count -gt 0) {
    Write-Error "Operation visibility errors: $($visibilityErrors -join '; ')."
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

Write-Host "OpenAPI and apiProperties JSON are valid. Swagger 2.0 is preserved, GetMondayItemDetails returns enhanced column value fields and GetMondayItemColumnValue returns a typed response with x-ms-summary values, x-ms-paths is absent, x-ms-dynamic-values is absent, x-ms-dynamic-list is absent, required arrays are non-empty when present, operationIds and POST paths are unique, body parameters have x-ms-summary, action visibility matches the UX rules, scriptOperations cover every scripted action, and no token/secret patterns were found."

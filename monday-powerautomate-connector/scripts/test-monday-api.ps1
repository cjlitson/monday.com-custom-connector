[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Token,

    [Parameter(Mandatory = $true)]
    [string]$ItemId
)

$ErrorActionPreference = "Stop"

$uri = "https://api.monday.com/v2"
$headers = @{
    Authorization = $Token
    "Content-Type" = "application/json"
    "API-Version" = "2026-04"
}

$body = @{
    query = "query GetMondayItemDetails(`$itemId: [ID!]!) { items(ids: `$itemId) { id name board { id name } group { id title } column_values { id text value type } } }"
    variables = @{
        itemId = $ItemId
    }
} | ConvertTo-Json -Depth 20

Write-Host "Calling monday.com GraphQL API for item ID $ItemId."
$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body -ContentType "application/json"

if ($response.errors) {
    Write-Error ($response.errors | ConvertTo-Json -Depth 20)
    exit 1
}

$response | ConvertTo-Json -Depth 20

# LV_monday_com_Actions - Power Automate custom connector

This repository contains a GitHub-ready Microsoft Power Platform custom connector for friendly monday.com actions.

## Purpose

**LV_monday_com_Actions** lets Power Automate makers call common monday.com GraphQL operations without writing GraphQL. The connector exposes separate, friendly actions such as **Get monday item details**, **Create monday item update/comment**, and **Change monday status column**.

The connector still calls monday.com directly at `https://api.monday.com/v2`. API tokens are supplied through the connector connection/auth configuration and must never be committed to source control.

## Why this design

monday.com uses a single GraphQL endpoint, but Power Platform/APIM import validates operations by HTTP method and public path signature. A previous multi-action approach used `x-ms-paths` with query-parameter routing, which could fail import with duplicate signatures like `POST /?operation={operation}`.

The primary connector now avoids that issue by using:

1. **Unique public Swagger paths** for Power Automate actions, such as `POST /get-item-details` and `POST /create-item`.
2. **Power Platform custom connector C# code** in `connector/script.csx` to dispatch by `operationId`, build the monday.com GraphQL body, and rewrite the outbound request to `POST https://api.monday.com/v2`.

No Azure Functions, APIM middleware, external services, or hard-coded secrets are required.

## Repository structure

```text
monday-powerautomate-connector/
├── connector/
│   ├── apiDefinition.swagger.json
│   ├── apiProperties.json
│   ├── script.csx
│   ├── README.md
│   └── experimental/
│       ├── apiDefinition.multi-action.experimental.swagger.json
│       └── apiDefinition.single-action.graphql.swagger.json
├── docs/
├── samples/
│   ├── get-item-details.json
│   ├── create-update.json
│   ├── change-status.json
│   ├── change-column-value.json
│   ├── create-item.json
│   └── run-graphql-*.json
├── scripts/
│   ├── validate-openapi.ps1
│   └── test-monday-api.ps1
└── README.md
```

## Friendly actions

| Maker action | Operation ID | Public path | Required inputs | Optional inputs |
| --- | --- | --- | --- | --- |
| Get monday item details | `GetMondayItemDetails` | `POST /get-item-details` | `itemId` | None |
| Create monday item update/comment | `CreateMondayItemUpdate` | `POST /create-update` | `itemId`, `body` | None |
| Change monday status column | `ChangeMondayStatus` | `POST /change-status` | `boardId`, `itemId`, `label` | `columnId` defaults to `status` |
| Change monday column value | `ChangeMondayColumnValue` | `POST /change-column-value` | `boardId`, `itemId`, `columnId`, `value` | None |
| Create monday item | `CreateMondayItem` | `POST /create-item` | `boardId`, `itemName` | `groupId`, `columnValues` |

`value` and `columnValues` are strings containing monday-compatible JSON because monday column types require different JSON shapes. Example: `{"status":{"label":"Working on it"}}`.

## Import or update in Power Platform

1. Open **Power Automate** or **Power Apps**.
2. Go to **Custom connectors**.
3. Create or update the connector by importing `connector/apiDefinition.swagger.json`.
4. Confirm the connector name and metadata.
5. Configure security with the existing API key setting. The API key header name is `Authorization`.
6. Enable/upload custom code from `connector/script.csx` and ensure custom code is attached to these operations in `connector/apiProperties.json`:
   - `GetMondayItemDetails`
   - `CreateMondayItemUpdate`
   - `ChangeMondayStatus`
   - `ChangeMondayColumnValue`
   - `CreateMondayItem`
7. Create a connection and enter the monday.com API token when prompted.
8. Test each friendly action from the connector Test tab or from a Power Automate flow.

## Validation checklist

Before importing, verify:

- `connector/apiDefinition.swagger.json` is valid JSON and Swagger/OpenAPI 2.0.
- `connector/apiProperties.json` is valid JSON.
- Operation IDs are unique.
- `scriptOperations` exactly match the Swagger operation IDs.
- Public POST paths are unique.
- The primary Swagger file does **not** contain `x-ms-paths`.
- No monday.com API token or secret is committed.
- README import instructions and test payloads are present.

Run:

```powershell
cd monday-powerautomate-connector/scripts
pwsh ./validate-openapi.ps1
```

## Test payloads

### Get monday item details

Sample file: `samples/get-item-details.json`

```json
{
  "itemId": "<ITEM_ID>"
}
```

### Create monday item update/comment

Sample file: `samples/create-update.json`

```json
{
  "itemId": "<ITEM_ID>",
  "body": "Email was sent to the requester."
}
```

### Change monday status column

Sample file: `samples/change-status.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemId": "<ITEM_ID>",
  "columnId": "status",
  "label": "Done"
}
```

### Change monday column value

Sample file: `samples/change-column-value.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemId": "<ITEM_ID>",
  "columnId": "<COLUMN_ID>",
  "value": "{\"text\":\"Example value\"}"
}
```

### Create monday item

Sample file: `samples/create-item.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemName": "New item from Power Automate",
  "groupId": "<GROUP_ID>",
  "columnValues": "{\"status\":{\"label\":\"Working on it\"}}"
}
```

## Experimental and fallback files

The old `x-ms-paths` multi-action definition is retained only for reference at `connector/experimental/apiDefinition.multi-action.experimental.swagger.json`. It is deprecated for primary import because it can trigger duplicate APIM operation signatures.

The previous working single generic GraphQL connector is preserved at `connector/experimental/apiDefinition.single-action.graphql.swagger.json` as a fallback/reference. Use the primary `connector/apiDefinition.swagger.json` for this multi-action custom-code design.

## Existing webhook router pattern

This connector is for monday.com **actions**. Keep using the existing Power Automate HTTP webhook router to receive monday.com webhook events, then call these friendly connector actions from the router or downstream flows.

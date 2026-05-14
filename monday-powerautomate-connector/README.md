# LV_monday_com_Actions - Power Automate custom connector

This repository contains a GitHub-ready Microsoft Power Platform custom connector for friendly monday.com actions.

## Purpose

**LV_monday_com_Actions** lets Power Automate makers call common monday.com GraphQL operations without writing GraphQL. The connector exposes separate, friendly actions such as **Get monday item details**, **Create monday item update/comment**, **Change monday status column**, typed column update actions, metadata dropdown actions, and subitem actions.

The connector still calls monday.com directly at `https://api.monday.com/v2`. API tokens are supplied through the connector connection/auth configuration and must never be committed to source control.

## Why this design

monday.com uses a single GraphQL endpoint, but Power Platform/APIM import validates operations by HTTP method and public path signature. The primary connector avoids duplicate APIM signatures by using:

1. **Unique public Swagger paths** for Power Automate actions, such as `POST /get-item-details`, `POST /list-boards`, and `POST /create-subitem`.
2. **Power Platform custom connector C# code** in `connector/script.csx` to dispatch by `operationId`, build the monday.com GraphQL body, and rewrite the outbound request to `POST https://api.monday.com/v2`.

No Azure Functions, APIM middleware, external services, or hard-coded secrets are required. The primary Swagger stays on **Swagger/OpenAPI 2.0** and intentionally does **not** use `x-ms-paths`.

## Repository structure

```text
monday-powerautomate-connector/
├── connector/
│   ├── apiDefinition.swagger.json
│   ├── apiProperties.json
│   ├── script.csx
│   ├── README.md
│   └── experimental/
├── docs/
├── samples/
│   ├── get-item-details.json
│   ├── create-update.json
│   ├── change-status.json
│   ├── change-column-value.json
│   ├── create-item.json
│   ├── list-*.json
│   ├── change-date-column.json
│   ├── change-text-column.json
│   ├── change-number-column.json
│   ├── create-subitem.json
│   ├── get-subitems.json
│   ├── get-subitem-details.json
│   ├── change-subitem-column-value.json
│   └── run-graphql-*.json
├── scripts/
│   ├── validate-openapi.ps1
│   └── test-monday-api.ps1
└── README.md
```

## Friendly actions

### Normal user-facing actions

| Maker action | Operation ID | Public path | Required inputs | Optional inputs |
| --- | --- | --- | --- | --- |
| Get monday item details | `GetMondayItemDetails` | `POST /get-item-details` | `itemId` | None |
| Create monday item update/comment | `CreateMondayItemUpdate` | `POST /create-update` | `itemId`, `updateText` | None |
| Change monday status column | `ChangeMondayStatus` | `POST /change-status` | `boardId`, `itemId`, `label` | `columnId` defaults to `status` |
| Change monday date column | `ChangeMondayDateColumn` | `POST /change-date-column` | `boardId`, `itemId`, `columnId`, `date` | `time` |
| Change monday text column | `ChangeMondayTextColumn` | `POST /change-text-column` | `boardId`, `itemId`, `columnId`, `text` | None |
| Change monday number column | `ChangeMondayNumberColumn` | `POST /change-number-column` | `boardId`, `itemId`, `columnId`, `number` | None |
| Create monday item | `CreateMondayItem` | `POST /create-item` | `boardId`, `itemName` | `groupId`, `columnValues` |
| Create monday subitem | `CreateMondaySubitem` | `POST /create-subitem` | `parentItemId`, `itemName` | `columnValues` |
| Get monday subitems | `GetMondaySubitems` | `POST /get-subitems` | `parentItemId` | None |
| Get monday subitem details | `GetMondaySubitemDetails` | `POST /get-subitem-details` | `subitemId` | None |

These are the normal user-facing actions and are marked `x-ms-visibility: important` so makers see them first in Power Automate. The create-update input is now named `updateText` in the maker experience. Existing flows or older samples that still send `body` continue to work through the connector script fallback, but new flows should use `updateText`.

### Advanced raw JSON actions

| Maker action | Operation ID | Public path | When to use |
| --- | --- | --- | --- |
| Change monday column value | `ChangeMondayColumnValue` | `POST /change-column-value` | Advanced escape hatch for monday column types not covered by typed actions. |
| Change monday subitem column value | `ChangeMondaySubitemColumnValue` | `POST /change-subitem-column-value` | Advanced escape hatch for subitem column types not covered by typed actions. |

Raw JSON actions are marked `x-ms-visibility: advanced`. Prefer the typed date, text, number, and status actions when possible because makers do not need to handcraft monday-compatible JSON. Use raw JSON only when monday requires a column value shape that the typed actions do not yet cover.

`value` and `columnValues` are strings containing monday-compatible JSON, for example `{"status":{"label":"Working on it"}}`.

### Metadata/list actions for IDs and troubleshooting

| Maker action | Operation ID | Public path | Purpose |
| --- | --- | --- | --- |
| List monday workspaces | `ListMondayWorkspaces` | `POST /list-workspaces` | Returns workspace `id` and `name` values. |
| List monday boards | `ListMondayBoards` | `POST /list-boards` | Returns board `id`, `name`, workspace details, and `hierarchy_type` when monday returns them. |
| List monday board groups | `ListMondayBoardGroups` | `POST /list-board-groups` | Returns group `id`, `title`, and `name` aliases for a selected board. |
| List monday board columns | `ListMondayBoardColumns` | `POST /list-board-columns` | Returns column `id`, `title`, `type`, and `settings_str`; exposed as a normal troubleshooting action. |
| List monday board items | `ListMondayBoardItems` | `POST /list-board-items` | Uses `items_page`, returns item `id` and `name`, and includes `cursor` when available. |
| List monday status labels | `ListMondayStatusLabels` | `POST /list-status-labels` | Queries board columns, finds the selected status column, parses `settings_str`, and returns label keys/titles. |

These helper/list operations are marked `x-ms-visibility: advanced` because they are mainly used to look up IDs, feed future dropdowns, or troubleshoot connector behavior. Keeping them advanced reduces clutter for normal flow makers while preserving every working action.

These operations return a dropdown-friendly response shape:

```json
{
  "value": [
    { "id": "123", "name": "Example" }
  ],
  "cursor": "<NEXT_CURSOR_IF_RETURNED>",
  "raw": { "data": {} }
}
```

## Finding monday IDs

Until dynamic dropdowns are restored, enter IDs manually:

- **Board IDs**: run **List monday boards** or copy the numeric board ID from the board URL.
- **Item IDs**: run **List monday board items**, inspect webhook payloads where item IDs may appear as `pulseId`, or open the item in monday and copy its item ID.
- **Group IDs**: run **List monday board groups** for the board and use the returned group `id`.
- **Column IDs**: run **List monday board columns** and use the returned column `id`, not the display name.
- **Status labels**: run **List monday status labels** for a board and status column, then use the visible label such as `Done` or `Working on it`.

Field descriptions in the connector match this guidance: board, item, group, and column fields point makers to the relevant list action, and date/time fields use `yyyy-MM-dd` and `HH:mm` formats.

## Dynamic dropdown behavior

Dynamic dropdowns are planned for a later version after this basic UX cleanup is stable. They remain temporarily disabled in the primary connector so the Swagger imports cleanly in Power Platform. Board, item, column, group, and status-label fields remain manual ID entry fields.

The list/metadata actions remain available for direct use and testing:

- `ListMondayWorkspaces`
- `ListMondayBoards`
- `ListMondayBoardGroups`
- `ListMondayBoardColumns`
- `ListMondayBoardItems`
- `ListMondayStatusLabels`

These actions still return `value` arrays suitable for future dropdown binding while preserving the raw monday GraphQL response under `raw` for troubleshooting. Dropdowns will be added back in a later version after the metadata actions are restructured for Power Platform dynamic parameter binding. Until then, run the relevant list action from the connector Test tab and paste the returned IDs into the manual fields.

## Typed column update actions

| Maker action | Operation ID | Public path | Value built by connector |
| --- | --- | --- | --- |
| Change monday date column | `ChangeMondayDateColumn` | `POST /change-date-column` | `{"date":"yyyy-MM-dd"}` or `{"date":"yyyy-MM-dd","time":"HH:mm"}` |
| Change monday text column | `ChangeMondayTextColumn` | `POST /change-text-column` | Sends the text as the monday JSON scalar value accepted by text columns. |
| Change monday number column | `ChangeMondayNumberColumn` | `POST /change-number-column` | Sends the numeric value as the monday JSON scalar value accepted by number columns. |

Use these when makers should not have to handcraft monday JSON. Continue using **Change monday column value** for advanced/custom column JSON.

## Subitem actions

| Maker action | Operation ID | Public path | Purpose |
| --- | --- | --- | --- |
| Create monday subitem | `CreateMondaySubitem` | `POST /create-subitem` | Calls `create_subitem` and returns the created subitem `id` and `name`. |
| Get monday subitems | `GetMondaySubitems` | `POST /get-subitems` | Queries a parent item and returns subitems with parent, board, and column values when available. |
| Get monday subitem details | `GetMondaySubitemDetails` | `POST /get-subitem-details` | Queries one subitem by ID using the standard item details pattern because subitems are items too. |
| Change monday subitem column value | `ChangeMondaySubitemColumnValue` | `POST /change-subitem-column-value` | Calls `change_column_value` for a subitem. Classic boards may require the hidden subitems board ID; multi-level boards use the main board ID. |

## Webhook triggers and future native trigger approach

Native webhook triggers are **not implemented yet** in this connector. monday.com webhook subscription setup requires challenge-response verification, and the current Power Automate HTTP router already handles that verification flow.

Recommended current pattern:

1. Keep monday.com webhook events pointed at the existing Power Automate HTTP router.
2. Let the router answer monday.com's challenge-response validation.
3. From the router or downstream flows, call these friendly connector actions for monday.com reads/writes.

Future/experimental native trigger work should account for monday's challenge-response handshake before being promoted into the primary connector.

## Import or update in Power Platform

1. Open **Power Automate** or **Power Apps**.
2. Go to **Custom connectors**.
3. Create or update the connector by importing `connector/apiDefinition.swagger.json`.
4. Confirm the connector name and metadata.
5. Configure security with the existing API key setting. The API key header name is `Authorization`.
6. Enable/upload custom code from `connector/script.csx` and ensure custom code is attached to all operations listed in `connector/apiProperties.json` under `scriptOperations`.
7. Create a connection and enter the monday.com API token when prompted.
8. Test helper/list actions first to find IDs, then test typed column, item, update, and subitem actions. Normal user-facing actions are marked important; helper/list and raw JSON actions are marked advanced.

## Validation checklist

Before importing, verify:

- `connector/apiDefinition.swagger.json` is valid JSON and Swagger/OpenAPI 2.0.
- `connector/apiProperties.json` is valid JSON.
- Operation IDs are unique.
- Public POST paths are unique.
- `scriptOperations` include every scripted operation.
- The primary Swagger file does **not** contain `x-ms-paths`.
- The primary Swagger file does **not** contain `x-ms-dynamic-values`.
- The primary Swagger file does **not** contain `x-ms-dynamic-list`.
- No `required` array is empty.
- Every body parameter has a friendly `x-ms-summary`.
- User-facing actions are marked `x-ms-visibility: important`.
- Helper/list and raw JSON actions are marked `x-ms-visibility: advanced`.
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
  "updateText": "Email was sent to the requester."
}
```

Backward compatibility note: `script.csx` still accepts the old `body` property for existing flows, but new samples and maker-facing fields use `updateText`.

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

### List monday workspaces

Sample file: `samples/list-workspaces.json`

```json
{}
```

### List monday boards

Sample file: `samples/list-boards.json`

```json
{
  "workspaceId": "<WORKSPACE_ID_OPTIONAL>"
}
```

### List monday board groups

Sample file: `samples/list-board-groups.json`

```json
{
  "boardId": "<BOARD_ID>"
}
```

### List monday board columns

Sample file: `samples/list-board-columns.json`

```json
{
  "boardId": "<BOARD_ID>",
  "columnType": "status"
}
```

### List monday board items

Sample file: `samples/list-board-items.json`

```json
{
  "boardId": "<BOARD_ID>",
  "limit": 100
}
```

### List monday status labels

Sample file: `samples/list-status-labels.json`

```json
{
  "boardId": "<BOARD_ID>",
  "columnId": "status"
}
```

### Change monday date column

Sample file: `samples/change-date-column.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemId": "<ITEM_ID>",
  "columnId": "<DATE_COLUMN_ID>",
  "date": "2026-05-08",
  "time": "09:30"
}
```

### Change monday text column

Sample file: `samples/change-text-column.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemId": "<ITEM_ID>",
  "columnId": "<TEXT_COLUMN_ID>",
  "text": "Updated from Power Automate"
}
```

### Change monday number column

Sample file: `samples/change-number-column.json`

```json
{
  "boardId": "<BOARD_ID>",
  "itemId": "<ITEM_ID>",
  "columnId": "<NUMBER_COLUMN_ID>",
  "number": 42
}
```

### Create monday subitem

Sample file: `samples/create-subitem.json`

```json
{
  "parentItemId": "<PARENT_ITEM_ID>",
  "itemName": "New subitem from Power Automate",
  "columnValues": "{\"status\":{\"label\":\"Working on it\"}}"
}
```

### Get monday subitems

Sample file: `samples/get-subitems.json`

```json
{
  "parentItemId": "<PARENT_ITEM_ID>"
}
```

### Get monday subitem details

Sample file: `samples/get-subitem-details.json`

```json
{
  "subitemId": "<SUBITEM_ID>"
}
```

### Change monday subitem column value

Sample file: `samples/change-subitem-column-value.json`

```json
{
  "boardId": "<SUBITEMS_BOARD_ID_OR_MAIN_BOARD_ID>",
  "subitemId": "<SUBITEM_ID>",
  "columnId": "<COLUMN_ID>",
  "value": "{\"text\":\"Updated subitem value\"}"
}
```

## Experimental and fallback files

The old `x-ms-paths` multi-action definition is retained only for reference at `connector/experimental/apiDefinition.multi-action.experimental.swagger.json`. It is deprecated for primary import because it can trigger duplicate APIM operation signatures.

The previous working single generic GraphQL connector is preserved at `connector/experimental/apiDefinition.single-action.graphql.swagger.json` as a fallback/reference. Use the primary `connector/apiDefinition.swagger.json` for this multi-action custom-code design.

# LV_monday_com_Actions connector files

This folder contains the primary Power Platform custom connector definition for **LV_monday_com_Actions**.

## Files

- `apiDefinition.swagger.json` - primary Swagger/OpenAPI 2.0 definition with friendly, unique public action paths.
- `apiProperties.json` - Power Platform connection metadata, publisher metadata, and `scriptOperations` bindings.
- `script.csx` - Power Platform custom connector C# script that converts friendly action inputs to monday.com GraphQL requests.
- `experimental/apiDefinition.multi-action.experimental.swagger.json` - deprecated `x-ms-paths` multi-action reference; do not use for primary import.
- `experimental/apiDefinition.single-action.graphql.swagger.json` - previous working single generic GraphQL fallback/reference.

## Endpoint and authentication

- Public connector actions use distinct paths such as `/get-item-details` and `/create-item`.
- `script.csx` rewrites each supported action to `POST https://api.monday.com/v2`.
- Authentication remains the monday.com API token supplied by the connector connection in the `Authorization` header.
- No token, board ID, item ID, or organization-specific secret is stored in these files.

## Friendly actions

| Action | Operation ID | Purpose |
| --- | --- | --- |
| Get monday item details | `GetMondayItemDetails` | Returns item id, name, board, group, and column values. |
| Create monday item update/comment | `CreateMondayItemUpdate` | Creates an update/comment on a monday item. |
| Change monday status column | `ChangeMondayStatus` | Changes a status column by friendly label; blank `columnId` defaults to `status`. |
| Change monday column value | `ChangeMondayColumnValue` | Changes any column using a monday-compatible JSON string. |
| Create monday item | `CreateMondayItem` | Creates an item with optional group and column values JSON. |

## Import notes

Use `apiDefinition.swagger.json` as the primary import file and upload/enable `script.csx` as connector custom code. The primary definition intentionally does not use `x-ms-paths`; unique public paths avoid the duplicate APIM signature problem while custom code still sends requests directly to monday.com's single GraphQL endpoint.

# LV_monday_com_Actions connector files

This folder contains the primary Power Platform custom connector definition for **LV_monday_com_Actions**.

## Files

- `apiDefinition.swagger.json` - primary Swagger/OpenAPI 2.0 definition with friendly, unique public action paths and dynamic dropdown metadata.
- `apiProperties.json` - Power Platform connection metadata, publisher metadata, and `scriptOperations` bindings for every custom-code action.
- `script.csx` - Power Platform custom connector C# script that converts friendly action inputs to monday.com GraphQL requests and rewrites calls to `https://api.monday.com/v2`.
- `experimental/apiDefinition.multi-action.experimental.swagger.json` - deprecated `x-ms-paths` multi-action reference; do not use for primary import.
- `experimental/apiDefinition.single-action.graphql.swagger.json` - previous working single generic GraphQL fallback/reference.

## Endpoint and authentication

- Public connector actions use distinct paths such as `/get-item-details`, `/list-boards`, `/change-date-column`, and `/create-subitem`.
- `script.csx` rewrites each supported action to `POST https://api.monday.com/v2`.
- Authentication remains the monday.com API token supplied by the connector connection in the `Authorization` header.
- No token, board ID, item ID, or organization-specific secret is stored in these files.

## Action groups

| Group | Operations |
| --- | --- |
| Preserved item actions | `GetMondayItemDetails`, `CreateMondayItemUpdate`, `ChangeMondayStatus`, `ChangeMondayColumnValue`, `CreateMondayItem` |
| Dropdown metadata actions | `ListMondayWorkspaces`, `ListMondayBoards`, `ListMondayBoardGroups`, `ListMondayBoardColumns`, `ListMondayBoardItems`, `ListMondayStatusLabels` |
| Typed column updates | `ChangeMondayDateColumn`, `ChangeMondayTextColumn`, `ChangeMondayNumberColumn` |
| Subitem actions | `CreateMondaySubitem`, `GetMondaySubitems`, `GetMondaySubitemDetails`, `ChangeMondaySubitemColumnValue` |

## Dynamic dropdowns

The Swagger uses `x-ms-dynamic-values` and companion `x-ms-dynamic-list` metadata on board, group, item, column, and status label fields. The metadata actions return a `value` array suitable for Power Platform dropdown binding while preserving the raw monday GraphQL response under `raw` for troubleshooting.

## Subitems

Subitems are treated as monday items for detail and update operations. When changing subitem column values, classic boards may require the hidden subitems board ID; multi-level boards generally use the main board ID.

## Webhooks/triggers

Native webhook triggers are intentionally not implemented in the primary connector yet. monday.com webhook setup requires challenge-response verification, and the existing Power Automate HTTP router already handles that challenge flow. Keep using the router for webhook events and call these actions from router/downstream flows.

## Import notes

Use `apiDefinition.swagger.json` as the primary import file and upload/enable `script.csx` as connector custom code. The primary definition intentionally does not use `x-ms-paths`; unique public paths avoid the duplicate APIM signature problem while custom code still sends requests directly to monday.com's single GraphQL endpoint.

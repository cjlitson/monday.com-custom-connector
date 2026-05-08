# LV monday.com Actions connector files

This folder contains the Power Platform custom connector definition for **LV monday.com Actions**.

## Files

- `apiDefinition.swagger.json` - Swagger/OpenAPI 2.0 definition for direct monday.com GraphQL actions.
- `apiProperties.json` - Power Platform connection metadata for the monday.com API token.

## Endpoint and authentication

- Base endpoint: `https://api.monday.com/v2`
- Authentication: monday.com API token supplied by the connector connection in the `Authorization` header.
- No token, board ID, item ID, or organization-specific secret is stored in these files.

## Actions

| Action | Operation ID | Purpose |
| --- | --- | --- |
| Get monday item details | `GetMondayItemDetails` | Reads item, board, group, and column value details. |
| Create monday item update/comment | `CreateMondayItemUpdate` | Adds an update/comment to an item. |
| Change monday status column | `ChangeMondayStatus` | Changes a status column by label. |
| Change monday column value | `ChangeMondayColumnValue` | Changes a column using raw monday column value JSON. |
| Create monday item | `CreateMondayItem` | Creates a new item in a board group. |

## Import notes

Power Automate custom connectors import one OpenAPI operation per unique HTTP method/path. Because monday.com GraphQL uses one `POST /v2` endpoint for every action, this definition keeps one standard `paths` action and uses the Microsoft `x-ms-paths` extension to expose additional maker-friendly operations.

Each operation includes a prebuilt GraphQL query/mutation and a `variables` object for the action inputs. Keep the prebuilt query/mutation unchanged unless monday.com changes its API schema or you intentionally want to customize the selected fields.

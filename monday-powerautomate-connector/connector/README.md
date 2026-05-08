# LV monday.com GraphQL connector files

This folder contains the Power Platform custom connector definition for **LV monday.com GraphQL**.

## Files

- `apiDefinition.swagger.json` - primary Swagger/OpenAPI 2.0 definition for the reliable Version 1 single-action connector.
- `apiProperties.json` - Power Platform connection metadata for the monday.com API token.
- `experimental/apiDefinition.multi-action.experimental.swagger.json` - previous multi-action Swagger file retained for reference only.

## Endpoint and authentication

- Base endpoint: `https://api.monday.com/v2`
- Authentication: monday.com API token supplied by the connector connection in the `Authorization` header.
- No token, board ID, item ID, or organization-specific secret is stored in these files.

## Version 1 action

| Action | Operation ID | Purpose |
| --- | --- | --- |
| Run monday GraphQL request | `RunMondayGraphQL` | Runs any monday.com GraphQL query or mutation by accepting a required `query` string and optional `variables` object. |

## Import notes

Power Automate custom connectors import one OpenAPI operation per unique HTTP method/path signature. Because monday.com GraphQL uses one `POST /v2` endpoint for every query and mutation, the primary Version 1 connector exposes only one generic action and does not use `x-ms-paths`.

The experimental multi-action Swagger file is not the primary import file. It can be used as a reference for future designs, but friendly actions should be added later through Azure middleware, custom connector code, or Power Automate child flows so that the imported connector avoids duplicate GraphQL endpoint signatures.

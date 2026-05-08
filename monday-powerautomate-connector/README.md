# LV monday.com Actions - Power Automate custom connector

This repository contains a GitHub-ready Power Platform custom connector project for monday.com. The connector wraps common monday.com GraphQL API actions so Power Automate flow makers can call monday.com without manually creating generic HTTP actions or hand-building GraphQL payloads for every flow.

## What this connector does

**LV monday.com Actions** is a direct Power Automate custom connector for monday.com actions.

- It calls monday.com directly at `https://api.monday.com/v2`.
- It uses Swagger/OpenAPI 2.0 for Power Platform import compatibility.
- It uses the monday.com API token entered when a connector connection is created.
- It does **not** hard-code or store API tokens in the repository.
- It does **not** require Azure Functions, Azure App Service, Logic Apps middleware, or any Azure-hosted deployment.
- It is for monday.com **actions only**. It is not a webhook receiver and does not replace your existing webhook intake flow.

Your existing Power Automate HTTP webhook router should continue receiving monday.com webhooks. This connector is intended to be called from that router or downstream flows after a monday webhook has already been received.

## Repository structure

```text
monday-powerautomate-connector/
├── connector/
│   ├── apiDefinition.swagger.json
│   ├── apiProperties.json
│   └── README.md
├── docs/
│   ├── webhook-router-design.md
│   ├── webhook-url-generator.md
│   └── route-configuration-table.md
├── samples/
│   ├── get-item-details-request.json
│   ├── create-update-request.json
│   ├── change-status-request.json
│   ├── change-column-value-request.json
│   └── create-item-request.json
├── scripts/
│   ├── validate-openapi.ps1
│   └── test-monday-api.ps1
└── README.md
```

## Connector actions

| Maker action | Operation ID | Inputs | monday.com GraphQL operation |
| --- | --- | --- | --- |
| Get monday item details | `GetMondayItemDetails` | `itemId` | Returns item id, item name, board id/name, group id/title, and `column_values` id/text/value/type. |
| Create monday item update/comment | `CreateMondayItemUpdate` | `itemId`, `body` | Calls `create_update(item_id, body)`. |
| Change monday status column | `ChangeMondayStatus` | `boardId`, `itemId`, `columnId`, `statusLabel` | Calls `change_column_value` with a monday status label value. |
| Change monday column value | `ChangeMondayColumnValue` | `boardId`, `itemId`, `columnId`, `columnValueJson` | Calls `change_column_value` using a raw JSON value string. |
| Create monday item | `CreateMondayItem` | `boardId`, `groupId`, `itemName`, `columnValuesJson` | Calls `create_item(board_id, group_id, item_name, column_values)`. |

## Setup in Power Automate

1. Get a monday.com API token from monday.com.
2. Go to **Power Automate**.
3. Open **Data > Custom connectors**.
4. Select **New custom connector > Import an OpenAPI file**.
5. Upload `connector/apiDefinition.swagger.json`.
6. Configure and test the connector.
7. Create a connector connection and enter the monday.com API token when prompted.

Do not paste real API tokens into any file in this repository. The token belongs only in the Power Platform connector connection.

## Using this connector with the existing webhook router

Keep the current webhook architecture:

```text
monday webhook receives event
  → existing Power Automate HTTP webhook router extracts the monday item ID
  → router or child flow calls Get monday item details
  → flow reads item name, board, group, and column values
  → flow sends email, creates an update, changes status, or updates another column
```

Example flow pattern:

1. The existing Power Automate HTTP trigger receives a monday webhook event.
2. The router gets the item ID from the webhook payload.
3. The router calls **Get monday item details** (`GetMondayItemDetails`).
4. The flow uses the returned item name and column values to compose an email.
5. After the email sends, the flow calls **Create monday item update/comment** or **Change monday status column**.


## Reusable monday webhook pattern

Use one existing Power Automate HTTP webhook router flow for many monday.com webhook automations. Each monday.com webhook automation gets a generated URL that points to the same router and includes a route query string value.

```text
monday webhook template
  → Power Automate HTTP webhook router
  → route lookup
  → monday custom connector action
  → email / Teams / SharePoint / monday update
```

Example generated route URL:

```text
[Router HTTP URL]&route=statusReadyEmail
```

In this pattern:

- The monday webhook template sends events to the existing Power Automate HTTP webhook router.
- The router handles monday challenge validation by returning the received `challenge` value.
- The router reads the `route` query string value and looks up the matching route configuration.
- The router calls **LV monday.com Actions** after the webhook is received when it needs current item details or needs to update monday.
- Users paste generated URLs into monday webhook automation templates instead of editing the router flow for each new automation.

More details are in:

- `docs/webhook-router-design.md`
- `docs/webhook-url-generator.md`
- `docs/route-configuration-table.md`

### Triggers vs actions

- The custom connector actions call monday.com.
- The HTTP webhook router receives monday.com events.
- True custom connector webhook triggers are possible in Power Platform, but they are not recommended in this phase because monday challenge validation and webhook lifecycle handling are more complex without middleware.

This project does not replace the existing HTTP webhook router with a true custom connector trigger yet.

### Router expressions

| Value | Power Automate expression |
| --- | --- |
| Route | `coalesce(triggerOutputs()?['queries']?['route'], 'default')` |
| Has Challenge | `not(empty(triggerBody()?['challenge']))` |
| Item ID | `coalesce(triggerBody()?['event']?['pulseId'], triggerBody()?['event']?['itemId'], 'Unknown item ID')` |
| Board ID | `coalesce(triggerBody()?['event']?['boardId'], 'Unknown board ID')` |
| Event Type | `coalesce(triggerBody()?['event']?['type'], 'unknown')` |

### Use this URL in monday.com

```text
Use this URL in monday.com

Route name:
{{DisplayName}}

Webhook URL:
{{GeneratedWebhookUrl}}

Where to paste it:
Open monday board > Automate or Integrate > Webhooks > choose event template > paste URL > save.

How to test it:
Create or update a test item that matches the selected monday webhook event. Confirm the Power Automate router run succeeds and the expected email, Teams message, SharePoint action, or monday update occurs.

Who to contact if it fails:
Contact {{SupportOwnerOrTeam}} with the route name, board name, test item ID, and approximate test time.
```

### Future enhancements

- Add a security secret back to the URL.
- Add SharePoint/Dataverse route lookup.
- Add an admin Power App for generating URLs.
- Add a logging table for every webhook received.
- Add true connector webhook trigger feasibility later.

## Implementation plan

### Phase 1: Import connector and test Get item details

Import `connector/apiDefinition.swagger.json`, create a connection with a monday.com API token, and test `GetMondayItemDetails` against a non-production sample item.

### Phase 2: Add connector action into existing monday webhook router flow

Add the imported connector action to the existing Power Automate HTTP webhook router flow after the webhook payload has been parsed.

### Phase 3: Use Get item details before sending emails

Use `GetMondayItemDetails` to fetch the current item name, board, group, and columns before building email subject/body content.

### Phase 4: Add update/status actions after email is sent

After the email action succeeds, call `CreateMondayItemUpdate`, `ChangeMondayStatus`, or `ChangeMondayColumnValue` to record the outcome back to monday.com.

## Local validation

Run the basic OpenAPI validation script from the repository root:

```powershell
pwsh ./monday-powerautomate-connector/scripts/validate-openapi.ps1 -SwaggerPath ./monday-powerautomate-connector/connector/apiDefinition.swagger.json
```

Optionally test monday.com API connectivity with your own token and item ID. Do not commit either value.

```powershell
pwsh ./monday-powerautomate-connector/scripts/test-monday-api.ps1 -Token "<YOUR_MONDAY_API_TOKEN>" -ItemId "<YOUR_ITEM_ID>"
```

## Troubleshooting

### Invalid API token

Symptoms include HTTP 401/403 responses or GraphQL errors indicating authorization failure.

- Confirm the token was copied from monday.com correctly.
- Confirm the token was entered in the connector connection, not in a repository file.
- Recreate the Power Automate connection if the token was rotated.

### Item not found

Symptoms include an empty `items` array or permission-related errors.

- Confirm the item ID exists.
- Confirm the token owner has access to the board containing the item.
- Confirm the flow is passing the monday item ID, not a pulse name or board ID.

### Malformed column value JSON

Symptoms include GraphQL validation errors or monday errors when changing column values.

- Confirm `columnValueJson` and `columnValuesJson` are valid JSON strings.
- Escape quotes if entering JSON inside another JSON body.
- Match the JSON shape required by the monday.com column type.
- For status updates, prefer `ChangeMondayStatus` and pass the label instead of raw JSON.

### monday.com API rate limits

Symptoms include rate limit errors or intermittent failures during high-volume webhook processing.

- Add retry policies in Power Automate where appropriate.
- Reduce duplicate calls in the webhook router.
- Fetch item details once and reuse the output in later actions.
- Review monday.com API usage and complexity limits for the account.

### OpenAPI import errors

Symptoms include Power Automate refusing to import the connector or hiding actions.

- Confirm `connector/apiDefinition.swagger.json` is valid JSON.
- Confirm the file uses `"swagger": "2.0"`, not OpenAPI 3.0.
- Run `scripts/validate-openapi.ps1`.
- Keep operation IDs unique.
- If Power Automate flags a schema as too complex, simplify the response schema and re-import.

## Security notes

- Never commit real monday.com API tokens.
- Never commit production board IDs, item IDs, or organization-specific secrets.
- Use environment-specific test items when validating connector actions.
- Rotate the monday.com API token if it is accidentally exposed.

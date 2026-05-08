# LV monday.com GraphQL - Power Automate custom connector

This repository contains a GitHub-ready Power Platform custom connector project for monday.com.

Version 1 is intentionally a **single-action** connector. monday.com exposes GraphQL through one endpoint, `https://api.monday.com/v2`, and Power Automate custom connector import treats actions with the same HTTP method/path signature as duplicates. To avoid the duplicate signature import error, the primary connector imports one reliable generic GraphQL action instead of several `POST /` actions.

## What this connector does

**LV monday.com GraphQL** is a direct Power Automate custom connector for monday.com GraphQL requests.

- It calls monday.com directly at `https://api.monday.com/v2`.
- It uses Swagger/OpenAPI 2.0 for Power Platform import compatibility.
- It exposes exactly one action: **Run monday GraphQL request** (`RunMondayGraphQL`).
- It uses the monday.com API token entered when a connector connection is created.
- It does **not** hard-code or store API tokens in the repository.
- It does **not** require Azure Functions, Azure App Service, Logic Apps middleware, or any Azure-hosted deployment.
- It does **not** use `x-ms-paths` in the primary import file.
- It is for monday.com **actions only**. It is not a webhook receiver and does not replace your existing webhook intake flow.

Friendly, separate actions can be added later with Azure middleware, custom connector code, or child flows. Those options can translate maker-friendly inputs into GraphQL payloads while keeping the imported connector free of duplicate `POST /` signatures.

Your existing Power Automate HTTP webhook router should continue receiving monday.com webhooks. This connector is intended to be called from that router or downstream flows after a monday webhook has already been received.

## Repository structure

```text
monday-powerautomate-connector/
├── connector/
│   ├── apiDefinition.swagger.json
│   ├── apiProperties.json
│   ├── README.md
│   └── experimental/
│       └── apiDefinition.multi-action.experimental.swagger.json
├── docs/
│   ├── webhook-router-design.md
│   ├── webhook-url-generator.md
│   └── route-configuration-table.md
├── samples/
│   ├── run-graphql-get-item-details.json
│   ├── run-graphql-create-update.json
│   ├── run-graphql-change-status.json
│   ├── run-graphql-change-column-value.json
│   └── run-graphql-create-item.json
├── scripts/
│   ├── validate-openapi.ps1
│   └── test-monday-api.ps1
└── README.md
```

## Connector action

| Maker action | Operation ID | Inputs | Response |
| --- | --- | --- | --- |
| Run monday GraphQL request | `RunMondayGraphQL` | `query` string, optional `variables` object | Raw monday.com GraphQL response with `data` and optional `errors`. |

### Authentication

The connector uses API key authentication with the header name `Authorization`. The user or admin enters the monday.com API token when creating the connector connection. Do not paste real API tokens into any file in this repository.

## Setup in Power Automate

1. Get a monday.com API token from monday.com.
2. Go to **Power Automate**.
3. Open **Data > Custom connectors**.
4. Select **New custom connector > Import an OpenAPI file**.
5. Upload `connector/apiDefinition.swagger.json`.
6. Configure and test the connector.
7. Create a connector connection and enter the monday.com API token when prompted.

## RunMondayGraphQL examples

Use **Run monday GraphQL request** for every monday.com query or mutation. Put the GraphQL operation in `query` and pass dynamic values in `variables`.

### 1. Get item details

Sample file: `samples/run-graphql-get-item-details.json`

```json
{
  "query": "query GetMondayItemDetails($itemId: [ID!]!) { items(ids: $itemId) { id name board { id name } group { id title } column_values { id text value type } } }",
  "variables": {
    "itemId": "<ITEM_ID>"
  }
}
```

### 2. Create item update/comment

Sample file: `samples/run-graphql-create-update.json`

```json
{
  "query": "mutation CreateMondayItemUpdate($itemId: ID!, $body: String!) { create_update(item_id: $itemId, body: $body) { id body created_at } }",
  "variables": {
    "itemId": "<ITEM_ID>",
    "body": "Email was sent to the requester."
  }
}
```

### 3. Change status column

Sample file: `samples/run-graphql-change-status.json`

```json
{
  "query": "mutation ChangeMondayStatus($boardId: ID!, $itemId: ID!, $columnId: String!, $statusValue: JSON!) { change_column_value(board_id: $boardId, item_id: $itemId, column_id: $columnId, value: $statusValue) { id } }",
  "variables": {
    "boardId": "<BOARD_ID>",
    "itemId": "<ITEM_ID>",
    "columnId": "<STATUS_COLUMN_ID>",
    "statusValue": {
      "label": "Done"
    }
  }
}
```

### 4. Change raw column value

Sample file: `samples/run-graphql-change-column-value.json`

```json
{
  "query": "mutation ChangeMondayColumnValue($boardId: ID!, $itemId: ID!, $columnId: String!, $columnValue: JSON!) { change_column_value(board_id: $boardId, item_id: $itemId, column_id: $columnId, value: $columnValue) { id } }",
  "variables": {
    "boardId": "<BOARD_ID>",
    "itemId": "<ITEM_ID>",
    "columnId": "<COLUMN_ID>",
    "columnValue": {
      "text": "Example value"
    }
  }
}
```

### 5. Create item

Sample file: `samples/run-graphql-create-item.json`

```json
{
  "query": "mutation CreateMondayItem($boardId: ID!, $groupId: String!, $itemName: String!, $columnValues: JSON) { create_item(board_id: $boardId, group_id: $groupId, item_name: $itemName, column_values: $columnValues) { id name board { id } } }",
  "variables": {
    "boardId": "<BOARD_ID>",
    "groupId": "<GROUP_ID>",
    "itemName": "New item from Power Automate",
    "columnValues": {
      "status": {
        "label": "Working on it"
      }
    }
  }
}
```

## Experimental multi-action file

The previous multi-action Swagger definition has been moved to `connector/experimental/apiDefinition.multi-action.experimental.swagger.json`. It is retained for reference only because Power Automate can reject multiple GraphQL actions that all resolve to the same `POST /?operation={operation}` signature. Do not use the experimental file as the primary import file.

## Using this connector with the existing webhook router

Keep the current webhook architecture:

```text
monday webhook receives event
  → existing Power Automate HTTP webhook router extracts the monday item ID
  → router or child flow calls Run monday GraphQL request
  → flow reads item name, board, group, and column values
  → flow sends email, creates an update, changes status, or updates another column
```

Example flow pattern:

1. The existing Power Automate HTTP trigger receives a monday webhook event.
2. The router gets the item ID from the webhook payload.
3. The router calls **Run monday GraphQL request** (`RunMondayGraphQL`) with the get item details query.
4. The flow uses the returned item name and column values to compose an email.
5. After the email sends, the flow calls **Run monday GraphQL request** again with a create update or change column mutation.

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
- The router calls **LV monday.com GraphQL** after the webhook is received when it needs current item details or needs to update monday.
- Users paste generated URLs into monday webhook automation templates instead of editing the router flow for each new automation.

More details are in:

- `docs/webhook-router-design.md`
- `docs/webhook-url-generator.md`
- `docs/route-configuration-table.md`

### Triggers vs actions

- The custom connector action calls monday.com.
- The HTTP webhook router receives monday.com events.
- True custom connector webhook triggers are possible in Power Platform, but they are not recommended in this phase because monday webhook validation and route management are already handled by the existing HTTP router.
- Keep webhook receiving in the router unless there is a separate project to build and validate a connector trigger later.

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
- Add friendly monday actions using Azure middleware, custom connector code, or child flows if makers need guided inputs.

## Implementation plan

### Phase 1: Import connector and test RunMondayGraphQL

Import `connector/apiDefinition.swagger.json`, create a connection with a monday.com API token, and test `RunMondayGraphQL` against a non-production sample item.

### Phase 2: Add connector action into existing monday webhook router flow

Add the imported connector action to the existing Power Automate HTTP webhook router flow after the webhook payload has been parsed.

### Phase 3: Use item details before sending emails

Use `RunMondayGraphQL` with the get item details query to fetch the current item name, board, group, and columns before building email subject/body content.

### Phase 4: Add update/status mutations after email is sent

After the email action succeeds, call `RunMondayGraphQL` with a create update, change status, or change column value mutation to record the outcome back to monday.com.

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

- Confirm `columnValue` and `columnValues` are valid JSON objects for the target monday.com column types.
- If you choose to pass JSON strings instead, escape quotes when entering JSON inside another JSON body.
- Match the JSON shape required by the monday.com column type.
- For status updates, pass a value such as `{ "label": "Done" }` for the status column.

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
- Confirm the primary import file contains only `RunMondayGraphQL`.
- Do not add multiple direct GraphQL actions to `paths` or `x-ms-paths` in the primary import file.

## Security notes

- Never commit real monday.com API tokens.
- Never commit production board IDs, item IDs, or organization-specific secrets.
- Use environment-specific test items when validating connector actions.
- Rotate the monday.com API token if it is accidentally exposed.

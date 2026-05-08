# monday webhook router design

This design keeps monday.com webhook receiving in the existing Power Automate HTTP webhook router flow and keeps the **LV monday.com Actions** custom connector focused on monday.com API actions.

No Azure Functions, Azure App Service, or Azure-hosted middleware are required.

## Purpose

The webhook router is the actual receiver for monday.com webhooks. monday.com sends webhook validation challenges and real event payloads to the router's Power Automate HTTP trigger URL.

The custom connector is used **after** a webhook is received when the flow needs to call monday.com, for example to:

- Get current item details.
- Create an item update/comment.
- Change a status column.
- Change another monday column value.
- Create a monday item.

## URL pattern

Each reusable automation is selected by a query string route value added to the same router HTTP URL.

```text
[Router HTTP URL]&route=statusReadyEmail
```

Users paste generated URLs into monday.com webhook automation templates. They do not need to edit the Power Automate router flow just to add another monday board automation when the route is already configured.

## Challenge validation

monday.com sends a challenge payload when a webhook URL is registered or validated. The router must detect that challenge and return it immediately.

Existing response body:

```json
{
  "challenge": "@{triggerBody()?['challenge']}"
}
```

Recommended challenge condition expression:

```text
not(empty(triggerBody()?['challenge']))
```

If the condition is true, the router should return the challenge response and stop further processing.

## Event routing

For real monday.com events, the router reads the query string route value and uses it to decide what action to perform.

Recommended route expression:

```text
coalesce(triggerOutputs()?['queries']?['route'], 'default')
```

Typical route handling pattern:

1. Receive the monday.com webhook event in the existing Power Automate HTTP trigger.
2. If a challenge is present, return the challenge response.
3. Read the `route` query string value.
4. Look up the route configuration in a SharePoint list, Dataverse table, or switch action.
5. If the route is disabled or unknown, send an alert or log the event.
6. Extract the monday item ID and board ID from the event payload.
7. Call **Get monday item details** from the custom connector if current item fields are needed.
8. Send email, post to Teams, write to SharePoint, create a monday update, or change monday status based on the route configuration.

## Recommended router expressions

| Value | Power Automate expression |
| --- | --- |
| Route | `coalesce(triggerOutputs()?['queries']?['route'], 'default')` |
| Has Challenge | `not(empty(triggerBody()?['challenge']))` |
| Item ID | `coalesce(triggerBody()?['event']?['pulseId'], triggerBody()?['event']?['itemId'], 'Unknown item ID')` |
| Board ID | `coalesce(triggerBody()?['event']?['boardId'], 'Unknown board ID')` |
| Event Type | `coalesce(triggerBody()?['event']?['type'], 'unknown')` |

## Architecture

```text
monday webhook template
  → Power Automate HTTP webhook router
  → challenge validation or route lookup
  → LV monday.com Actions custom connector action
  → email / Teams / SharePoint / monday update
```

## Route examples

| Route key | Example URL | Intended behavior |
| --- | --- | --- |
| `newProjectEmail` | `[Router HTTP URL]&route=newProjectEmail` | Send a new project notification email. |
| `statusReadyEmail` | `[Router HTTP URL]&route=statusReadyEmail` | Send an email when an item reaches a ready status. |
| `approvalRequest` | `[Router HTTP URL]&route=approvalRequest` | Send an approval request and optionally update monday. |
| `unknownRouteAlert` | `[Router HTTP URL]&route=unknownRouteAlert` | Alert admins when an unmapped route is used. |

## Triggers vs actions

- The custom connector actions call monday.com.
- The HTTP webhook router receives monday.com events.
- True custom connector webhook triggers are possible in Power Platform, but they are not recommended in this phase because monday challenge validation and webhook lifecycle handling are more complex without middleware.

This phase intentionally avoids replacing the existing HTTP webhook router with a custom connector trigger.

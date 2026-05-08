# Route configuration table design

A reusable monday webhook router should avoid hard-coded route behavior where possible. Use a SharePoint list or Dataverse table to define route behavior that the Power Automate router can look up at runtime.

## Recommended storage options

| Option | When to use |
| --- | --- |
| SharePoint list | Best for quick setup, lightweight admin editing, and organizations already using SharePoint for Power Automate configuration. |
| Dataverse table | Best for stronger data typing, app-driven administration, environments with Power Platform ALM, or more complex security requirements. |

## Suggested table/list name

```text
MondayWebhookRoutes
```

## Fields

| Field | Suggested type | Required | Notes |
| --- | --- | --- | --- |
| `RouteKey` | Single line text / Dataverse text | Yes | Unique route key used in the webhook URL, such as `statusReadyEmail`. |
| `DisplayName` | Single line text / Dataverse text | Yes | Friendly route name for admins and support users. |
| `Description` | Multiple lines text / Dataverse multiline text | No | What the route does. |
| `Enabled` | Yes/No / Dataverse boolean | Yes | Router should ignore or alert on disabled routes. |
| `ActionType` | Choice | Yes | Suggested values: `SendEmail`, `TeamsMessage`, `SharePointCreateItem`, `CreateMondayItemUpdate`, `ChangeMondayStatus`, `Composite`. |
| `EmailTo` | Single line text / Dataverse text | No | Required for email routes. Supports one or more addresses based on organization standards. |
| `EmailCc` | Single line text / Dataverse text | No | Optional CC recipients. |
| `EmailSubjectTemplate` | Single line text / Dataverse text | No | Template for email subjects. May include placeholders such as `{ItemName}` or `{BoardName}`. |
| `EmailBodyTemplate` | Multiple lines text / Dataverse multiline text | No | Template for email bodies. May include placeholders such as `{ItemName}`, `{ItemId}`, `{BoardName}`, or column values. |
| `CreateMondayUpdateAfterSend` | Yes/No / Dataverse boolean | Yes | If true, router should call the connector action `CreateMondayItemUpdate` after sending. |
| `SetStatusAfterSend` | Single line text / Dataverse text | No | Optional status label to set after successful processing, such as `Done`. |
| `Notes` | Multiple lines text / Dataverse multiline text | No | Support notes, owner, or implementation details. |

## Sample rows

| RouteKey | DisplayName | Description | Enabled | ActionType | EmailTo | EmailCc | EmailSubjectTemplate | EmailBodyTemplate | CreateMondayUpdateAfterSend | SetStatusAfterSend | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `newProjectEmail` | New project email | Send a notification when a new project item is created. | Yes | `SendEmail` | `projects@example.com` |  | `New project: {ItemName}` | `A new project was created on {BoardName}. Item ID: {ItemId}.` | Yes |  | Use with item-created monday webhook templates. |
| `statusReadyEmail` | Status ready email | Send an email when an item reaches the ready status. | Yes | `Composite` | `operations@example.com` | `manager@example.com` | `Ready for review: {ItemName}` | `{ItemName} is ready for review. Current board: {BoardName}.` | Yes | `Email Sent` | Router should get item details before building the email. |
| `approvalRequest` | Approval request | Send an approval request for the item. | Yes | `SendEmail` | `approvals@example.com` |  | `Approval requested: {ItemName}` | `Please review item {ItemName} from {BoardName}.` | Yes | `Pending Approval` | Consider replacing email with Power Automate approvals later. |
| `unknownRouteAlert` | Unknown route alert | Alert admins when a webhook arrives with an unmapped route. | Yes | `TeamsMessage` |  |  | `Unknown monday webhook route` | `A monday webhook used an unknown route: {RouteKey}. Item ID: {ItemId}.` | No |  | Use this as the router fallback behavior. |

The email addresses above are placeholders. Replace them with organization-approved distribution lists or configuration values before production use.

## Router lookup logic

1. Read the route key from the HTTP trigger query string.
2. Query the route configuration table/list where `RouteKey` equals the route key.
3. If no row is found, use `unknownRouteAlert` behavior.
4. If the row is disabled, log the event and stop or alert admins.
5. If the row is enabled, execute behavior based on `ActionType` and other fields.
6. If `CreateMondayUpdateAfterSend` is true, call the custom connector's `CreateMondayItemUpdate` action with `itemId` and `body`.
7. If `SetStatusAfterSend` has a value, call the custom connector's `ChangeMondayStatus` action with `boardId`, `itemId`, optional `columnId`, and `label`.

## Future admin experience

A future admin Power App can sit on top of this table to let authorized users create route records, generate URLs, view recent webhook logs, and disable routes without editing the router flow.

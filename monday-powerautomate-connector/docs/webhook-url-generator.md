# Generate monday webhook URL helper flow

This document designs a helper Power Automate flow named **Generate monday webhook URL**.

The helper flow produces a user-facing monday.com webhook URL and setup instructions so makers can onboard additional monday automations without editing the webhook router flow every time.

## Flow name

```text
Generate monday webhook URL
```

## Purpose

The flow accepts a route key and descriptive metadata, combines the route key with the existing Power Automate HTTP webhook router URL, and returns instructions that can be sent to the monday.com board owner or automation requester.

The helper flow does not receive monday.com webhooks itself. It only generates URLs that point to the existing webhook router.

## Inputs

| Input | Required | Description |
| --- | --- | --- |
| `RouteKey` | Yes | Stable route key appended to the router URL, such as `statusReadyEmail`. |
| `DisplayName` | Yes | Friendly name shown to users, such as `Status ready email`. |
| `Purpose` | Yes | Short explanation of what the automation does. |
| `EmailTo` | Yes | Primary email recipient for email-based routes. |
| `EmailCc` | No | Optional carbon-copy recipients. |
| `BoardName` | No | Optional monday board name where the URL will be installed. |
| `Notes` | No | Optional implementation or support notes. |

## Required configuration value

Store the existing router HTTP trigger URL in a secure environment variable, flow variable, or protected configuration location.

```text
RouterHttpUrl = [Router HTTP URL]
```

Do not commit real Power Automate HTTP trigger URLs to source control.

## URL generation logic

If the router URL already contains a query string, append `&route=`. If it does not contain a query string, append `?route=`.

Power Automate expression example:

```text
concat(variables('RouterHttpUrl'), if(contains(variables('RouterHttpUrl'), '?'), '&route=', '?route='), triggerBody()?['RouteKey'])
```

Example output URL:

```text
[Router HTTP URL]&route=statusReadyEmail
```

## Suggested flow steps

1. Trigger the helper flow manually, from Power Apps, or from a SharePoint/Dataverse route request list.
2. Collect `RouteKey`, `DisplayName`, `Purpose`, `EmailTo`, optional `EmailCc`, optional `BoardName`, and optional `Notes`.
3. Build the full webhook URL by appending the route query string to the router HTTP URL.
4. Return or email the generated URL and setup instructions.
5. Optionally write the route metadata to the route configuration table.

## Example output

```text
Webhook URL:
[Router HTTP URL]&route=statusReadyEmail

Instructions:
Open monday board > Automate or Integrate > Webhooks > choose event template > paste URL > save > test.
```

## User-facing instruction template: Use this URL in monday.com

```text
Use this URL in monday.com

Route name:
{{DisplayName}}

Purpose:
{{Purpose}}

Webhook URL:
{{GeneratedWebhookUrl}}

Where to paste it:
Open the monday board{{ if BoardName is provided: " named " + BoardName }}.
Go to Automate or Integrate > Webhooks.
Choose the webhook event template that matches this automation.
Paste the webhook URL into the URL field.
Save the automation.

How to test it:
Create or update a test item that matches the selected monday webhook event.
Confirm that the Power Automate router run succeeds.
Confirm that the expected email, Teams message, SharePoint update, or monday update happened.

Support:
If it fails, contact {{SupportOwnerOrTeam}} with the route name, board name, test item ID, and approximate test time.

Notes:
{{Notes}}
```

## Recommended output object

```json
{
  "routeKey": "statusReadyEmail",
  "displayName": "Status ready email",
  "purpose": "Send an email when an item reaches the ready status.",
  "webhookUrl": "[Router HTTP URL]&route=statusReadyEmail",
  "instructions": "Open monday board > Automate or Integrate > Webhooks > choose event template > paste URL > save > test."
}
```

## Security note

The generated URL should be shared only with monday.com administrators or trusted board owners. A future phase should add a route secret or signature back to the URL.

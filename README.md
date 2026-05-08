# monday.com custom connector

This repository contains Power Platform custom connector assets for monday.com.

The main project is in [`monday-powerautomate-connector/`](monday-powerautomate-connector/). It includes:

- A direct Swagger/OpenAPI 2.0 Power Automate custom connector for monday.com GraphQL.
- A reliable Version 1 primary connector with one generic action: **Run monday GraphQL request** (`RunMondayGraphQL`).
- Sample request bodies for common monday.com GraphQL operations.
- PowerShell validation and API test scripts.
- Reusable monday webhook router design documentation for the existing Power Automate HTTP webhook router.

Version 1 uses a single generic action because monday.com uses one GraphQL endpoint, `https://api.monday.com/v2`, for queries and mutations. Friendly actions can be added later with Azure middleware, custom connector code, or child flows without reintroducing duplicate direct GraphQL operation signatures in the primary import file.

## Reusable monday webhook pattern

The custom connector is for monday.com **actions**. The existing Power Automate HTTP webhook router remains the monday.com webhook **receiver**.

```text
monday webhook template
  → Power Automate HTTP webhook router
  → route lookup
  → monday custom connector action
  → email / Teams / SharePoint / monday update
```

Use generated URLs such as `[Router HTTP URL]&route=statusReadyEmail` in monday webhook automation templates. See [`monday-powerautomate-connector/docs/webhook-router-design.md`](monday-powerautomate-connector/docs/webhook-router-design.md) for the router design.

# monday.com custom connector

This repository contains Power Platform custom connector assets for monday.com.

The main project is in [`monday-powerautomate-connector/`](monday-powerautomate-connector/). It includes:

- A friendly multi-action custom connector named **LV_monday_com_Actions**.
- A primary Swagger/OpenAPI 2.0 definition with unique public paths for Power Automate actions.
- Power Platform custom connector C# code that rewrites friendly requests to `POST https://api.monday.com/v2`.
- Existing connection/auth configuration for monday.com API tokens in the `Authorization` header.
- Experimental/deprecated references for the old `x-ms-paths` design and the previous single generic GraphQL connector.
- Sample request bodies and validation scripts.

The primary design avoids `x-ms-paths` so Power Platform/APIM does not collapse the friendly operations into duplicate `POST /?operation={operation}` signatures. Makers see separate actions, but the connector still sends requests directly to monday.com without Azure Functions, middleware, or committed secrets.

Dynamic dropdowns are temporarily disabled in the primary connector. Board, item, column, group, and status-label fields remain manual ID entry fields; dropdowns will be added back in a later version after the metadata actions are restructured for Power Platform dynamic parameter binding.

See [`monday-powerautomate-connector/README.md`](monday-powerautomate-connector/README.md) for import instructions, validation, and test payloads.

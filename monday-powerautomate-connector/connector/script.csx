using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase
{
    private const string MondayGraphQlEndpoint = "https://api.monday.com/v2";
    private const string DefaultMondayApiVersion = "2026-04";

    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        string operationId = NormalizeOperationId(this.Context.OperationId);
        JObject input;
        try
        {
            input = await ReadJsonBodyAsync().ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return BadRequest("InvalidJson", "Request body must be a valid JSON object.");
        }

        JObject graphQlBody;
        HttpResponseMessage validationError = TryBuildGraphQlBody(operationId, input, out graphQlBody);
        if (validationError != null)
        {
            return validationError;
        }

        HttpRequestMessage request = this.Context.Request;
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri(MondayGraphQlEndpoint);
        request.Content = new StringContent(graphQlBody.ToString(Formatting.None), Encoding.UTF8, "application/json");

        // Authentication is configured on the connector connection. Keep any Authorization
        // header that Power Platform already applied to this request and only add the monday
        // API version header when the caller/connection has not already supplied one.
        if (!request.Headers.Contains("API-Version"))
        {
            request.Headers.TryAddWithoutValidation("API-Version", DefaultMondayApiVersion);
        }

        return await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeOperationId(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return operationId;
        }

        string trimmed = operationId.Trim();
        if (IsKnownOperation(trimmed))
        {
            return trimmed;
        }

        // Some Power Platform regions can expose custom-code operation ids as base64.
        // Decode defensively and fall back to the original value if it is not base64
        // or does not decode to a supported operation id.
        try
        {
            string normalized = trimmed.Replace('-', '+').Replace('_', '/');
            string padded = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return IsKnownOperation(decoded) ? decoded : trimmed;
        }
        catch
        {
            return trimmed;
        }
    }

    private async Task<JObject> ReadJsonBodyAsync()
    {
        if (this.Context.Request.Content == null)
        {
            return new JObject();
        }

        string content = await this.Context.Request.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new JObject();
        }

        return JObject.Parse(content);
    }

    private static HttpResponseMessage TryBuildGraphQlBody(string operationId, JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;

        switch (operationId)
        {
            case "GetMondayItemDetails":
                return BuildGetMondayItemDetails(input, out graphQlBody);
            case "CreateMondayItemUpdate":
                return BuildCreateMondayItemUpdate(input, out graphQlBody);
            case "ChangeMondayStatus":
                return BuildChangeMondayStatus(input, out graphQlBody);
            case "ChangeMondayColumnValue":
                return BuildChangeMondayColumnValue(input, out graphQlBody);
            case "CreateMondayItem":
                return BuildCreateMondayItem(input, out graphQlBody);
            default:
                return BadRequest("UnsupportedOperation", $"Operation '{operationId}' is not supported by this connector script.");
        }
    }

    private static HttpResponseMessage BuildGetMondayItemDetails(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string itemId = RequiredString(input, "itemId");
        if (itemId == null)
        {
            return MissingField("itemId");
        }

        graphQlBody = GraphQl(
            "query GetMondayItemDetails($itemIds: [ID!]!) { items(ids: $itemIds) { id name board { id name } group { id title } column_values { id text value type } } }",
            new JObject { ["itemIds"] = new JArray(itemId) });
        return null;
    }

    private static HttpResponseMessage BuildCreateMondayItemUpdate(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string itemId = RequiredString(input, "itemId");
        string body = RequiredString(input, "body");
        if (itemId == null) return MissingField("itemId");
        if (body == null) return MissingField("body");

        graphQlBody = GraphQl(
            "mutation CreateMondayItemUpdate($itemId: ID!, $body: String!) { create_update(item_id: $itemId, body: $body) { id } }",
            new JObject { ["itemId"] = itemId, ["body"] = body });
        return null;
    }

    private static HttpResponseMessage BuildChangeMondayStatus(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemId = RequiredString(input, "itemId");
        string columnId = OptionalString(input, "columnId");
        string label = RequiredString(input, "label");
        if (boardId == null) return MissingField("boardId");
        if (itemId == null) return MissingField("itemId");
        if (label == null) return MissingField("label");

        if (string.IsNullOrWhiteSpace(columnId))
        {
            columnId = "status";
        }

        // monday.com's JSON scalar is safest through Power Platform when the variable
        // value is a string containing monday-compatible JSON. Do not parse and re-emit
        // user JSON here; pass raw JSON scalar strings through to monday.com.
        string statusValue = new JObject { ["label"] = label }.ToString(Formatting.None);

        graphQlBody = GraphQl(
            "mutation ChangeMondayStatus($boardId: ID!, $itemId: ID!, $columnId: String!, $statusValue: JSON!) { change_column_value(board_id: $boardId, item_id: $itemId, column_id: $columnId, value: $statusValue) { id } }",
            new JObject { ["boardId"] = boardId, ["itemId"] = itemId, ["columnId"] = columnId, ["statusValue"] = statusValue });
        return null;
    }

    private static HttpResponseMessage BuildChangeMondayColumnValue(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemId = RequiredString(input, "itemId");
        string columnId = RequiredString(input, "columnId");
        string value = RequiredString(input, "value");
        if (boardId == null) return MissingField("boardId");
        if (itemId == null) return MissingField("itemId");
        if (columnId == null) return MissingField("columnId");
        if (value == null) return MissingField("value");

        // value is a monday-compatible JSON scalar string, for example:
        // {"text":"Example"} or {"label":"Done"}. Keep it as a string
        // in GraphQL variables so it is not double-escaped by hand-built GraphQL.
        graphQlBody = GraphQl(
            "mutation ChangeMondayColumnValue($boardId: ID!, $itemId: ID!, $columnId: String!, $columnValue: JSON!) { change_column_value(board_id: $boardId, item_id: $itemId, column_id: $columnId, value: $columnValue) { id } }",
            new JObject { ["boardId"] = boardId, ["itemId"] = itemId, ["columnId"] = columnId, ["columnValue"] = value });
        return null;
    }

    private static HttpResponseMessage BuildCreateMondayItem(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemName = RequiredString(input, "itemName");
        string groupId = OptionalString(input, "groupId");
        string columnValues = OptionalString(input, "columnValues");
        if (boardId == null) return MissingField("boardId");
        if (itemName == null) return MissingField("itemName");

        bool hasGroupId = !string.IsNullOrWhiteSpace(groupId);
        bool hasColumnValues = !string.IsNullOrWhiteSpace(columnValues);

        string declarations = "$boardId: ID!, $itemName: String!";
        string arguments = "board_id: $boardId, item_name: $itemName";
        JObject variables = new JObject { ["boardId"] = boardId, ["itemName"] = itemName };

        if (hasGroupId)
        {
            declarations += ", $groupId: String!";
            arguments += ", group_id: $groupId";
            variables["groupId"] = groupId;
        }

        if (hasColumnValues)
        {
            declarations += ", $columnValues: JSON";
            arguments += ", column_values: $columnValues";
            // columnValues is an optional monday-compatible JSON scalar string.
            // Leave the string intact rather than parsing it to avoid changing the
            // caller's intended column-type-specific JSON shape.
            variables["columnValues"] = columnValues;
        }

        graphQlBody = GraphQl(
            $"mutation CreateMondayItem({declarations}) {{ create_item({arguments}) {{ id name board {{ id }} }} }}",
            variables);
        return null;
    }

    private static JObject GraphQl(string query, JObject variables)
    {
        return new JObject
        {
            ["query"] = query,
            ["variables"] = variables
        };
    }

    private static string RequiredString(JObject input, string fieldName)
    {
        string value = OptionalString(input, fieldName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string OptionalString(JObject input, string fieldName)
    {
        JToken token = input == null ? null : input[fieldName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
    }

    private static HttpResponseMessage MissingField(string fieldName)
    {
        return BadRequest("MissingRequiredField", $"The '{fieldName}' field is required.");
    }

    private static HttpResponseMessage BadRequest(string error, string message)
    {
        JObject body = new JObject
        {
            ["error"] = error,
            ["message"] = message
        };

        return new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };
    }

    private static bool IsKnownOperation(string operationId)
    {
        return operationId == "GetMondayItemDetails"
            || operationId == "CreateMondayItemUpdate"
            || operationId == "ChangeMondayStatus"
            || operationId == "ChangeMondayColumnValue"
            || operationId == "CreateMondayItem";
    }
}

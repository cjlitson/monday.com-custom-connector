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
        request.Content = new StringContent(graphQlBody.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");

        // Authentication is configured on the connector connection. Keep any Authorization
        // header that Power Platform already applied to this request and only add the monday
        // API version header when the caller/connection has not already supplied one.
        if (!request.Headers.Contains("API-Version"))
        {
            request.Headers.TryAddWithoutValidation("API-Version", DefaultMondayApiVersion);
        }

        HttpResponseMessage response = await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        if (operationId == "GetMondayItemDetails")
        {
            return await BuildGetMondayItemDetailsResponseAsync(input, response).ConfigureAwait(false);
        }

        if (!IsMetadataListOperation(operationId))
        {
            return response;
        }

        return await BuildDropdownResponseAsync(operationId, input, response).ConfigureAwait(false);
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
            case "ListMondayWorkspaces":
                return BuildListMondayWorkspaces(input, out graphQlBody);
            case "ListMondayBoards":
                return BuildListMondayBoards(input, out graphQlBody);
            case "ListMondayBoardGroups":
                return BuildListMondayBoardGroups(input, out graphQlBody);
            case "ListMondayBoardColumns":
                return BuildListMondayBoardColumns(input, out graphQlBody);
            case "ListMondayBoardItems":
                return BuildListMondayBoardItems(input, out graphQlBody);
            case "ListMondayStatusLabels":
                return BuildListMondayStatusLabels(input, out graphQlBody);
            case "ChangeMondayDateColumn":
                return BuildChangeMondayDateColumn(input, out graphQlBody);
            case "ChangeMondayTextColumn":
                return BuildChangeMondayTextColumn(input, out graphQlBody);
            case "ChangeMondayNumberColumn":
                return BuildChangeMondayNumberColumn(input, out graphQlBody);
            case "CreateMondaySubitem":
                return BuildCreateMondaySubitem(input, out graphQlBody);
            case "GetMondaySubitems":
                return BuildGetMondaySubitems(input, out graphQlBody);
            case "GetMondaySubitemDetails":
                return BuildGetMondaySubitemDetails(input, out graphQlBody);
            case "ChangeMondaySubitemColumnValue":
                return BuildChangeMondaySubitemColumnValue(input, out graphQlBody);
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
            "query GetMondayItemDetails($itemIds: [ID!]!) { items(ids: $itemIds) { id name board { id name } group { id title } parent_item { id name } column_values { id text value type } } }",
            new JObject { ["itemIds"] = new JArray(itemId) });
        return null;
    }

    private static HttpResponseMessage BuildCreateMondayItemUpdate(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string itemId = RequiredString(input, "itemId");
        string updateText = RequiredString(input, "updateText");

        // Backward compatibility for flows and test payloads created before the
        // maker-facing field was renamed from body to updateText.
        if (updateText == null)
        {
            updateText = RequiredString(input, "body");
        }

        if (itemId == null) return MissingField("itemId");
        if (updateText == null) return MissingField("updateText");

        graphQlBody = GraphQl(
            "mutation CreateMondayItemUpdate($itemId: ID!, $body: String!) { create_update(item_id: $itemId, body: $body) { id } }",
            new JObject { ["itemId"] = itemId, ["body"] = updateText });
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

        string statusValue = new JObject { ["label"] = label }.ToString(Newtonsoft.Json.Formatting.None);

        graphQlBody = ChangeColumnValueGraphQl("ChangeMondayStatus", boardId, itemId, columnId, statusValue);
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

        graphQlBody = ChangeColumnValueGraphQl("ChangeMondayColumnValue", boardId, itemId, columnId, value);
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
            variables["columnValues"] = columnValues;
        }

        graphQlBody = GraphQl(
            $"mutation CreateMondayItem({declarations}) {{ create_item({arguments}) {{ id name board {{ id }} }} }}",
            variables);
        return null;
    }

    private static HttpResponseMessage BuildListMondayWorkspaces(JObject input, out JObject graphQlBody)
    {
        graphQlBody = GraphQl("query ListMondayWorkspaces { workspaces { id name } }", new JObject());
        return null;
    }

    private static HttpResponseMessage BuildListMondayBoards(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string workspaceId = OptionalString(input, "workspaceId");
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            graphQlBody = GraphQl("query ListMondayBoards { boards(limit: 500) { id name hierarchy_type workspace { id name } } }", new JObject());
        }
        else
        {
            graphQlBody = GraphQl("query ListMondayBoards($workspaceIds: [ID!]) { boards(limit: 500, workspace_ids: $workspaceIds) { id name hierarchy_type workspace { id name } } }", new JObject { ["workspaceIds"] = new JArray(workspaceId) });
        }

        return null;
    }

    private static HttpResponseMessage BuildListMondayBoardGroups(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        if (boardId == null) return MissingField("boardId");

        graphQlBody = GraphQl("query ListMondayBoardGroups($boardIds: [ID!]!) { boards(ids: $boardIds) { id groups { id title } } }", new JObject { ["boardIds"] = new JArray(boardId) });
        return null;
    }

    private static HttpResponseMessage BuildListMondayBoardColumns(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        if (boardId == null) return MissingField("boardId");

        graphQlBody = GraphQl("query ListMondayBoardColumns($boardIds: [ID!]!) { boards(ids: $boardIds) { id columns { id title type settings_str } } }", new JObject { ["boardIds"] = new JArray(boardId) });
        return null;
    }

    private static HttpResponseMessage BuildListMondayBoardItems(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        int limit = OptionalInt(input, "limit", 100);
        if (boardId == null) return MissingField("boardId");
        if (limit < 1 || limit > 500) return BadRequest("InvalidLimit", "The 'limit' field must be between 1 and 500.");

        graphQlBody = GraphQl("query ListMondayBoardItems($boardIds: [ID!]!, $limit: Int!) { boards(ids: $boardIds) { id items_page(limit: $limit) { cursor items { id name } } } }", new JObject { ["boardIds"] = new JArray(boardId), ["limit"] = limit });
        return null;
    }

    private static HttpResponseMessage BuildListMondayStatusLabels(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string columnId = RequiredString(input, "columnId");
        if (boardId == null) return MissingField("boardId");
        if (columnId == null) return MissingField("columnId");

        graphQlBody = GraphQl("query ListMondayStatusLabels($boardIds: [ID!]!) { boards(ids: $boardIds) { id columns { id title type settings_str } } }", new JObject { ["boardIds"] = new JArray(boardId) });
        return null;
    }

    private static HttpResponseMessage BuildChangeMondayDateColumn(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemId = RequiredString(input, "itemId");
        string columnId = RequiredString(input, "columnId");
        string date = RequiredString(input, "date");
        string time = OptionalString(input, "time");
        if (boardId == null) return MissingField("boardId");
        if (itemId == null) return MissingField("itemId");
        if (columnId == null) return MissingField("columnId");
        if (date == null) return MissingField("date");

        JObject value = new JObject { ["date"] = date };
        if (!string.IsNullOrWhiteSpace(time))
        {
            value["time"] = time;
        }

        graphQlBody = ChangeColumnValueGraphQl("ChangeMondayDateColumn", boardId, itemId, columnId, value.ToString(Newtonsoft.Json.Formatting.None));
        return null;
    }

    private static HttpResponseMessage BuildChangeMondayTextColumn(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemId = RequiredString(input, "itemId");
        string columnId = RequiredString(input, "columnId");
        string text = RequiredString(input, "text");
        if (boardId == null) return MissingField("boardId");
        if (itemId == null) return MissingField("itemId");
        if (columnId == null) return MissingField("columnId");
        if (text == null) return MissingField("text");

        // monday text columns accept a JSON scalar string through change_column_value.
        // Passing the raw text as the JSON variable value avoids forcing makers to type
        // {"text":"..."} while still using monday's documented JSON scalar path.
        graphQlBody = ChangeColumnValueGraphQl("ChangeMondayTextColumn", boardId, itemId, columnId, text);
        return null;
    }

    private static HttpResponseMessage BuildChangeMondayNumberColumn(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string itemId = RequiredString(input, "itemId");
        string columnId = RequiredString(input, "columnId");
        JToken numberToken = input == null ? null : input["number"];
        if (boardId == null) return MissingField("boardId");
        if (itemId == null) return MissingField("itemId");
        if (columnId == null) return MissingField("columnId");
        if (numberToken == null || numberToken.Type == JTokenType.Null) return MissingField("number");

        string numberValue = numberToken.Type == JTokenType.String ? (string)numberToken : numberToken.ToString(Newtonsoft.Json.Formatting.None);
        graphQlBody = ChangeColumnValueGraphQl("ChangeMondayNumberColumn", boardId, itemId, columnId, numberValue);
        return null;
    }

    private static HttpResponseMessage BuildCreateMondaySubitem(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string parentItemId = RequiredString(input, "parentItemId");
        string itemName = RequiredString(input, "itemName");
        string columnValues = OptionalString(input, "columnValues");
        if (parentItemId == null) return MissingField("parentItemId");
        if (itemName == null) return MissingField("itemName");

        string declarations = "$parentItemId: ID!, $itemName: String!";
        string arguments = "parent_item_id: $parentItemId, item_name: $itemName";
        JObject variables = new JObject { ["parentItemId"] = parentItemId, ["itemName"] = itemName };

        if (!string.IsNullOrWhiteSpace(columnValues))
        {
            declarations += ", $columnValues: JSON";
            arguments += ", column_values: $columnValues";
            variables["columnValues"] = columnValues;
        }

        graphQlBody = GraphQl($"mutation CreateMondaySubitem({declarations}) {{ create_subitem({arguments}) {{ id name }} }}", variables);
        return null;
    }

    private static HttpResponseMessage BuildGetMondaySubitems(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string parentItemId = RequiredString(input, "parentItemId");
        if (parentItemId == null) return MissingField("parentItemId");

        graphQlBody = GraphQl("query GetMondaySubitems($itemIds: [ID!]!) { items(ids: $itemIds) { id name subitems { id name parent_item { id name } board { id name } column_values { id text value type } } } }", new JObject { ["itemIds"] = new JArray(parentItemId) });
        return null;
    }

    private static HttpResponseMessage BuildGetMondaySubitemDetails(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string subitemId = RequiredString(input, "subitemId");
        if (subitemId == null) return MissingField("subitemId");

        graphQlBody = GraphQl("query GetMondaySubitemDetails($itemIds: [ID!]!) { items(ids: $itemIds) { id name board { id name } group { id title } parent_item { id name } column_values { id text value type } } }", new JObject { ["itemIds"] = new JArray(subitemId) });
        return null;
    }

    private static HttpResponseMessage BuildChangeMondaySubitemColumnValue(JObject input, out JObject graphQlBody)
    {
        graphQlBody = null;
        string boardId = RequiredString(input, "boardId");
        string subitemId = RequiredString(input, "subitemId");
        string columnId = RequiredString(input, "columnId");
        string value = RequiredString(input, "value");
        if (boardId == null) return MissingField("boardId");
        if (subitemId == null) return MissingField("subitemId");
        if (columnId == null) return MissingField("columnId");
        if (value == null) return MissingField("value");

        graphQlBody = ChangeColumnValueGraphQl("ChangeMondaySubitemColumnValue", boardId, subitemId, columnId, value);
        return null;
    }

    private static JObject ChangeColumnValueGraphQl(string operationName, string boardId, string itemId, string columnId, string value)
    {
        return GraphQl(
            $"mutation {operationName}($boardId: ID!, $itemId: ID!, $columnId: String!, $columnValue: JSON!) {{ change_column_value(board_id: $boardId, item_id: $itemId, column_id: $columnId, value: $columnValue) {{ id }} }}",
            new JObject { ["boardId"] = boardId, ["itemId"] = itemId, ["columnId"] = columnId, ["columnValue"] = value });
    }

    private static async Task<HttpResponseMessage> BuildGetMondayItemDetailsResponseAsync(JObject input, HttpResponseMessage mondayResponse)
    {
        string itemId = OptionalString(input, "itemId");
        string content = mondayResponse.Content == null ? null : await mondayResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        JObject raw;

        try
        {
            raw = JObject.Parse(content ?? string.Empty);
        }
        catch (JsonException)
        {
            JObject nonJsonBody = new JObject
            {
                ["success"] = false,
                ["message"] = "monday.com returned a non-JSON response.",
                ["itemId"] = itemId,
                ["rawResponseJson"] = content ?? string.Empty
            };

            return JsonResponse(mondayResponse.StatusCode, nonJsonBody);
        }

        string rawResponseJson = raw.ToString(Newtonsoft.Json.Formatting.None);
        JArray errors = raw["errors"] as JArray;
        if (errors != null && errors.Count > 0)
        {
            JObject errorBody = new JObject
            {
                ["success"] = false,
                ["message"] = BuildMondayErrorMessage(errors),
                ["itemId"] = itemId,
                ["rawResponseJson"] = rawResponseJson
            };

            return JsonResponse(mondayResponse.StatusCode, errorBody);
        }

        JToken item = raw.SelectToken("data.items[0]");
        if (item == null || item.Type == JTokenType.Null)
        {
            JObject notFoundBody = new JObject
            {
                ["success"] = false,
                ["message"] = "Item not found or token does not have access to this item.",
                ["itemId"] = itemId,
                ["rawResponseJson"] = rawResponseJson
            };

            return JsonResponse(mondayResponse.StatusCode, notFoundBody);
        }

        JToken columnValues = item["column_values"];
        JObject body = new JObject
        {
            ["success"] = true,
            ["message"] = "Item found.",
            ["itemId"] = AsString(item["id"]) ?? itemId,
            ["itemName"] = AsString(item["name"]),
            ["boardId"] = AsString(item["board"]?["id"]),
            ["boardName"] = AsString(item["board"]?["name"]),
            ["groupId"] = AsString(item["group"]?["id"]),
            ["groupName"] = AsString(item["group"]?["title"]),
            ["parentItemId"] = AsString(item["parent_item"]?["id"]),
            ["parentItemName"] = AsString(item["parent_item"]?["name"]),
            ["columnValuesJson"] = columnValues == null || columnValues.Type == JTokenType.Null ? "[]" : columnValues.ToString(Newtonsoft.Json.Formatting.None),
            ["rawResponseJson"] = rawResponseJson
        };

        return JsonResponse(mondayResponse.StatusCode, body);
    }

    private static string BuildMondayErrorMessage(JArray errors)
    {
        JArray messages = new JArray();
        foreach (JToken error in errors)
        {
            string message = AsString(error["message"]);
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        return messages.Count == 0 ? "monday.com returned an error." : string.Join("; ", messages.Values<string>());
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, JObject body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
        };
    }

    private static async Task<HttpResponseMessage> BuildDropdownResponseAsync(string operationId, JObject input, HttpResponseMessage mondayResponse)
    {
        string content = mondayResponse.Content == null ? null : await mondayResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!mondayResponse.IsSuccessStatusCode || string.IsNullOrWhiteSpace(content))
        {
            mondayResponse.Content = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json");
            return mondayResponse;
        }

        JObject raw;
        try
        {
            raw = JObject.Parse(content);
        }
        catch (JsonException)
        {
            mondayResponse.Content = new StringContent(content, Encoding.UTF8, "application/json");
            return mondayResponse;
        }

        JArray values = new JArray();
        string cursor = null;

        if (operationId == "ListMondayWorkspaces")
        {
            foreach (JToken workspace in raw.SelectTokens("data.workspaces[*]"))
            {
                values.Add(new JObject { ["id"] = AsString(workspace["id"]), ["name"] = AsString(workspace["name"]) });
            }
        }
        else if (operationId == "ListMondayBoards")
        {
            foreach (JToken board in raw.SelectTokens("data.boards[*]"))
            {
                values.Add(new JObject
                {
                    ["id"] = AsString(board["id"]),
                    ["name"] = AsString(board["name"]),
                    ["workspaceId"] = AsString(board["workspace"]?["id"]),
                    ["workspaceName"] = AsString(board["workspace"]?["name"]),
                    ["hierarchy_type"] = AsString(board["hierarchy_type"])
                });
            }
        }
        else if (operationId == "ListMondayBoardGroups")
        {
            foreach (JToken group in raw.SelectTokens("data.boards[*].groups[*]"))
            {
                string title = AsString(group["title"]);
                values.Add(new JObject { ["id"] = AsString(group["id"]), ["title"] = title, ["name"] = title });
            }
        }
        else if (operationId == "ListMondayBoardColumns" || operationId == "ListMondayStatusLabels")
        {
            string requestedColumnType = OptionalString(input, "columnType");
            string requestedColumnId = OptionalString(input, "columnId");
            foreach (JToken column in raw.SelectTokens("data.boards[*].columns[*]"))
            {
                if (operationId == "ListMondayBoardColumns")
                {
                    if (string.IsNullOrWhiteSpace(requestedColumnType) || string.Equals(AsString(column["type"]), requestedColumnType, StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(new JObject { ["id"] = AsString(column["id"]), ["title"] = AsString(column["title"]), ["name"] = AsString(column["title"]), ["type"] = AsString(column["type"]), ["settings_str"] = AsString(column["settings_str"]) });
                    }
                }
                else if (AsString(column["id"]) == requestedColumnId && AsString(column["type"]) == "status")
                {
                    AddStatusLabels(values, AsString(column["settings_str"]));
                }
            }
        }
        else if (operationId == "ListMondayBoardItems")
        {
            foreach (JToken page in raw.SelectTokens("data.boards[*].items_page"))
            {
                cursor = cursor ?? AsString(page["cursor"]);
                foreach (JToken item in page.SelectTokens("items[*]"))
                {
                    values.Add(new JObject { ["id"] = AsString(item["id"]), ["name"] = AsString(item["name"]) });
                }
            }
        }

        JObject friendly = new JObject { ["value"] = values, ["raw"] = raw };
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            friendly["cursor"] = cursor;
        }

        return new HttpResponseMessage(mondayResponse.StatusCode)
        {
            Content = new StringContent(friendly.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
        };
    }

    private static void AddStatusLabels(JArray values, string settings)
    {
        if (string.IsNullOrWhiteSpace(settings))
        {
            return;
        }

        JObject parsed;
        try
        {
            parsed = JObject.Parse(settings);
        }
        catch (JsonException)
        {
            return;
        }

        JObject labels = parsed["labels"] as JObject;
        if (labels == null)
        {
            return;
        }

        foreach (JProperty label in labels.Properties())
        {
            values.Add(new JObject { ["id"] = label.Name, ["key"] = label.Name, ["title"] = AsString(label.Value), ["name"] = AsString(label.Value) });
        }
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

        return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static int OptionalInt(JObject input, string fieldName, int defaultValue)
    {
        JToken token = input == null ? null : input[fieldName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return defaultValue;
        }

        int value;
        return int.TryParse(token.ToString(), out value) ? value : defaultValue;
    }

    private static string AsString(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
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
            Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
        };
    }

    private static bool IsMetadataListOperation(string operationId)
    {
        return operationId == "ListMondayWorkspaces"
            || operationId == "ListMondayBoards"
            || operationId == "ListMondayBoardGroups"
            || operationId == "ListMondayBoardColumns"
            || operationId == "ListMondayBoardItems"
            || operationId == "ListMondayStatusLabels";
    }

    private static bool IsKnownOperation(string operationId)
    {
        return operationId == "GetMondayItemDetails"
            || operationId == "CreateMondayItemUpdate"
            || operationId == "ChangeMondayStatus"
            || operationId == "ChangeMondayColumnValue"
            || operationId == "CreateMondayItem"
            || operationId == "ListMondayWorkspaces"
            || operationId == "ListMondayBoards"
            || operationId == "ListMondayBoardGroups"
            || operationId == "ListMondayBoardColumns"
            || operationId == "ListMondayBoardItems"
            || operationId == "ListMondayStatusLabels"
            || operationId == "ChangeMondayDateColumn"
            || operationId == "ChangeMondayTextColumn"
            || operationId == "ChangeMondayNumberColumn"
            || operationId == "CreateMondaySubitem"
            || operationId == "GetMondaySubitems"
            || operationId == "GetMondaySubitemDetails"
            || operationId == "ChangeMondaySubitemColumnValue";
    }
}

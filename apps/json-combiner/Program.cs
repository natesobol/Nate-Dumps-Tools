using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/combine", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with at least one JSON file." });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files were uploaded." });
    }

    var parsedNodes = new List<JsonNode>();
    var errors = new List<object>();

    foreach (var file in files)
    {
        if (file.Length == 0)
        {
            errors.Add(new { file = file.FileName, message = "File was empty." });
            continue;
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            var jsonNode = JsonNode.Parse(content);

            if (jsonNode is null)
            {
                errors.Add(new { file = file.FileName, message = "File did not contain valid JSON content." });
                continue;
            }

            parsedNodes.Add(jsonNode);
        }
        catch (JsonException jsonEx)
        {
            errors.Add(new { file = file.FileName, message = jsonEx.Message });
        }
    }

    if (parsedNodes.Count == 0)
    {
        return Results.BadRequest(new { error = "Unable to parse any JSON payloads.", details = errors });
    }

    var combined = CombineNodes(parsedNodes);

    return Results.Ok(new
    {
        combinedType = combined switch
        {
            JsonArray => "array",
            JsonObject => "object",
            _ => "mixed"
        },
        combined,
        parseErrors = errors
    });
});

app.Run();

static JsonNode CombineNodes(IEnumerable<JsonNode> nodes)
{
    var parsedList = nodes.ToList();

    if (parsedList.All(n => n is JsonArray))
    {
        var mergedArray = new JsonArray();
        foreach (var node in parsedList.Cast<JsonArray>())
        {
            foreach (var item in node)
            {
                mergedArray.Add(item?.DeepClone());
            }
        }

        return mergedArray;
    }

    if (parsedList.All(n => n is JsonObject))
    {
        var mergedObject = new JsonObject();
        foreach (var source in parsedList.Cast<JsonObject>())
        {
            MergeObjects(mergedObject, source);
        }

        return mergedObject;
    }

    var mixedWrapper = new JsonArray();
    foreach (var node in parsedList)
    {
        mixedWrapper.Add(node?.DeepClone());
    }

    return mixedWrapper;
}

static void MergeObjects(JsonObject target, JsonObject source)
{
    foreach (var property in source)
    {
        var sourceValue = property.Value;

        if (sourceValue is null)
        {
            target[property.Key] = null;
            continue;
        }

        if (target[property.Key] is JsonObject targetObject && sourceValue is JsonObject sourceObject)
        {
            MergeObjects(targetObject, sourceObject);
            continue;
        }

        if (target[property.Key] is JsonArray targetArray && sourceValue is JsonArray sourceArray)
        {
            foreach (var item in sourceArray)
            {
                targetArray.Add(item?.DeepClone());
            }

            continue;
        }

        target[property.Key] = sourceValue.DeepClone();
    }
}

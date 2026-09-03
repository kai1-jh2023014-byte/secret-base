using System.Text.Json;

namespace SecretBase.Core.Connectors;

/// <summary>Minimal JSON Schema subset: type, required, properties, additionalProperties, enum, maxLength.</summary>
public static class JsonSchemaLite
{
    public static bool TryValidate(JsonElement schema, JsonElement instance, out string error)
    {
        error = string.Empty;
        if (schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return true;
        }

        return Validate(schema, instance, "$", out error);
    }

    private static bool Validate(JsonElement schema, JsonElement instance, string path, out string error)
    {
        error = string.Empty;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (schema.TryGetProperty("type", out var typeEl))
        {
            var expected = typeEl.GetString();
            if (!TypeMatches(expected, instance.ValueKind))
            {
                error = $"Schema type mismatch at {path}.";
                return false;
            }
        }

        if (schema.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
        {
            var ok = enumEl.EnumerateArray().Any(item => JsonEquals(item, instance));
            if (!ok)
            {
                error = $"Value is not in enum at {path}.";
                return false;
            }
        }

        if (instance.ValueKind == JsonValueKind.String
            && schema.TryGetProperty("maxLength", out var maxLen)
            && maxLen.TryGetInt32(out var max)
            && (instance.GetString()?.Length ?? 0) > max)
        {
            error = $"String too long at {path}.";
            return false;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object
                ? props
                : default;
            var additional = !schema.TryGetProperty("additionalProperties", out var add) || add.ValueKind != JsonValueKind.False;

            if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var name in required.EnumerateArray())
                {
                    var key = name.GetString();
                    if (!string.IsNullOrWhiteSpace(key) && !instance.TryGetProperty(key, out _))
                    {
                        error = $"Missing required property '{key}' at {path}.";
                        return false;
                    }
                }
            }

            foreach (var property in instance.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object
                    && properties.TryGetProperty(property.Name, out var childSchema))
                {
                    if (!Validate(childSchema, property.Value, path + "." + property.Name, out error))
                    {
                        return false;
                    }
                }
                else if (!additional)
                {
                    error = $"Unexpected property '{property.Name}' at {path}.";
                    return false;
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("maxItems", out var maxItems)
            && maxItems.TryGetInt32(out var maxCount)
            && instance.GetArrayLength() > maxCount)
        {
            error = $"Array too long at {path}.";
            return false;
        }

        return true;
    }

    private static bool TypeMatches(string? expected, JsonValueKind kind) =>
        expected switch
        {
            null or "" => true,
            "object" => kind == JsonValueKind.Object,
            "array" => kind == JsonValueKind.Array,
            "string" => kind == JsonValueKind.String,
            "number" => kind is JsonValueKind.Number,
            "integer" => kind == JsonValueKind.Number,
            "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "null" => kind == JsonValueKind.Null,
            _ => false
        };

    private static bool JsonEquals(JsonElement left, JsonElement right) =>
        left.GetRawText() == right.GetRawText();
}

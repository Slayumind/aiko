using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// Telling a file that holds nothing from a file that holds rubbish.
///
/// Every parser here answers a broken file with "no data", which is right for reading and wrong
/// for writing: the next save would put defaults over choices somebody may still want back. So the
/// store asks this first, and puts an unreadable file aside instead of overwriting it.
public static class JsonText
{
    public static bool IsObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(json) is JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

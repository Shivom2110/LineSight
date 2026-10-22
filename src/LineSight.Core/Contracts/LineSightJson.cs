using System.Text.Json;
using System.Text.Json.Serialization;

namespace LineSight.Core.Contracts;

/// <summary>JSON settings used by every HTTP client of the API: camelCase, enums as strings ("Running").</summary>
public static class LineSightJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

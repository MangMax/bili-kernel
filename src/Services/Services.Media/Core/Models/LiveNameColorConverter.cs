using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>History uses a numeric RGB value (zero means default) or a color string.</summary>
internal sealed class LiveNameColorConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        var rgb = reader.GetInt32();
        return rgb == 0 ? null : "#" + (rgb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

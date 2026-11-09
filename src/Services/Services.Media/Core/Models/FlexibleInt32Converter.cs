// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 宽松的 Int32 转换器：直播接口的 user_level 等字段偶尔返回数组或字符串，
/// 不能让一条历史弹幕把整次补屏打挂.
/// </summary>
internal sealed class FlexibleInt32Converter : JsonConverter<int>
{
    /// <inheritdoc/>
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var number) ? number : 0;
            case JsonTokenType.String:
                return int.TryParse(reader.GetString(), out var parsed) ? parsed : 0;
            case JsonTokenType.StartArray:
                var result = 0;
                var hasValue = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (!hasValue && reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var item))
                    {
                        result = item;
                        hasValue = true;
                    }
                }

                return result;
            case JsonTokenType.Null:
                return 0;
            default:
                reader.Skip();
                return 0;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

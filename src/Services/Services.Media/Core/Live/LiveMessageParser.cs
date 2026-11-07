// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;
using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 将直播叶子包解析为强类型事件；未知命令与损坏消息只分类，不抛异常.
/// </summary>
internal static class LiveMessageParser
{
    /// <summary>
    /// 解析一条普通通知消息.
    /// </summary>
    /// <param name="payload">包体.</param>
    /// <param name="receivedAt">接收时间.</param>
    /// <param name="liveEvent">解析出的事件.</param>
    /// <returns>解析分类.</returns>
    public static LiveMessageParseKind Parse(ReadOnlySpan<byte> payload, DateTimeOffset receivedAt, out LiveDanmakuEvent? liveEvent)
    {
        liveEvent = null;
        if (payload.IsEmpty)
        {
            return LiveMessageParseKind.Malformed;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload.ToArray());
        }
        catch (JsonException)
        {
            return LiveMessageParseKind.Malformed;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetString(root, "cmd", out var command)
                || string.IsNullOrEmpty(command))
            {
                return LiveMessageParseKind.Malformed;
            }

            try
            {
                return ParseCommand(command, root, receivedAt, out liveEvent);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
            {
                liveEvent = null;
                return LiveMessageParseKind.Malformed;
            }
        }
    }

    /// <summary>
    /// 解析心跳回包：前 4 字节为无符号大端人气值，其后可跟心跳原文.
    /// </summary>
    /// <param name="payload">包体.</param>
    /// <param name="onlineCount">人气值；纯回执时为 null.</param>
    /// <returns>是否为可识别的心跳回包.</returns>
    public static bool TryParseHeartbeatReply(ReadOnlySpan<byte> payload, out int? onlineCount)
    {
        onlineCount = null;
        if (payload.Length >= 4 && payload[0] != (byte)'{')
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(payload[..4]);
            onlineCount = value > int.MaxValue ? int.MaxValue : (int)value;
            return true;
        }

        if (payload.IsEmpty)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 解析认证回执.
    /// </summary>
    /// <param name="payload">包体.</param>
    /// <param name="code">回执代码.</param>
    /// <param name="message">回执文本.</param>
    /// <returns>是否包含有效的 code.</returns>
    public static bool TryParseAuthReply(ReadOnlySpan<byte> payload, out int code, out string? message)
    {
        code = -1;
        message = null;
        if (payload.IsEmpty)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("code", out var codeElement)
                || codeElement.ValueKind != JsonValueKind.Number
                || !codeElement.TryGetInt32(out code))
            {
                return false;
            }

            if (root.TryGetProperty("message", out var messageElement))
            {
                message = ElementString(messageElement);
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static LiveMessageParseKind ParseCommand(string command, JsonElement root, DateTimeOffset receivedAt, out LiveDanmakuEvent? liveEvent)
    {
        liveEvent = null;
        if (HasCommandPrefix(command, "DANMU_MSG"))
        {
            liveEvent = ParseDanmaku(command, root, receivedAt);
            return liveEvent is null ? LiveMessageParseKind.Malformed : LiveMessageParseKind.Event;
        }

        if (command.StartsWith("PLAYURL_RELOAD", StringComparison.Ordinal))
        {
            liveEvent = new LivePlayUrlReloadEvent(root.GetRawText(), command, receivedAt);
            return LiveMessageParseKind.Event;
        }

        switch (command)
        {
            case "SUPER_CHAT_MESSAGE":
                liveEvent = ParseSuperChat(command, root, receivedAt);
                return liveEvent is null ? LiveMessageParseKind.Malformed : LiveMessageParseKind.Event;
            case "SUPER_CHAT_MESSAGE_JPN":
                // 日文醒目留言是同一消息的重复，忽略以免双计.
                return LiveMessageParseKind.Ignored;
            case "SUPER_CHAT_MESSAGE_DELETE":
                liveEvent = ParseSuperChatDelete(command, root, receivedAt);
                return LiveMessageParseKind.Event;
            case "WATCHED_CHANGE":
                liveEvent = ParseWatchedChange(command, root, receivedAt);
                return liveEvent is null ? LiveMessageParseKind.Malformed : LiveMessageParseKind.Event;
            case "ROOM_CHANGE":
                liveEvent = ParseRoomChange(command, root, receivedAt);
                return liveEvent is null ? LiveMessageParseKind.Malformed : LiveMessageParseKind.Event;
            case "PREPARING":
                liveEvent = new LiveRoomNotStartedEvent(command, receivedAt);
                return LiveMessageParseKind.Event;
            case "LIVE":
                liveEvent = new LiveStartedEvent(command, receivedAt);
                return LiveMessageParseKind.Event;
            case "CUT_OFF":
                liveEvent = new LiveRoomCutOffEvent(GetOptionalDataString(root, "msg", "message"), command, receivedAt);
                return LiveMessageParseKind.Event;
            default:
                return LiveMessageParseKind.Unknown;
        }
    }

    private static LiveDanmakuReceivedEvent? ParseDanmaku(string command, JsonElement root, DateTimeOffset receivedAt)
    {
        if (!root.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Array || info.GetArrayLength() < 3)
        {
            return null;
        }

        var text = ElementString(info[1]);
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var user = info[2];
        var userId = user.ValueKind == JsonValueKind.Array && user.GetArrayLength() > 0 ? ElementLong(user[0]) : 0;
        var userName = user.ValueKind == JsonValueKind.Array && user.GetArrayLength() > 1 ? ElementString(user[1]) ?? string.Empty : string.Empty;
        var isAdmin = user.ValueKind == JsonValueKind.Array && user.GetArrayLength() > 2 && ElementLong(user[2]) == 1;
        var isVip = user.ValueKind == JsonValueKind.Array
            && user.GetArrayLength() > 4
            && (ElementLong(user[3]) == 1 || ElementLong(user[4]) == 1);

        var textColor = info[0].ValueKind == JsonValueKind.Array && info[0].GetArrayLength() > 3
            ? FormatColor(info[0][3])
            : "#FFFFFF";

        var medalName = info.GetArrayLength() > 3 && info[3].ValueKind == JsonValueKind.Array && info[3].GetArrayLength() > 1
            ? ElementString(info[3][1])
            : null;
        var medalLevel = info.GetArrayLength() > 3 && info[3].ValueKind == JsonValueKind.Array && info[3].GetArrayLength() > 0
            ? ElementString(info[3][0])
            : null;

        var userLevel = info.GetArrayLength() > 4 && info[4].ValueKind == JsonValueKind.Array && info[4].GetArrayLength() > 0
            ? "UL" + ElementString(info[4][0])
            : string.Empty;
        var levelColor = info.GetArrayLength() > 4 && info[4].ValueKind == JsonValueKind.Array && info[4].GetArrayLength() > 2
            ? ElementString(info[4][2]) ?? string.Empty
            : string.Empty;
        var userTitle = info.GetArrayLength() > 5 && info[5].ValueKind == JsonValueKind.Array && info[5].GetArrayLength() > 0
            ? ElementString(info[5][0])
            : null;

        var danmaku = new LiveDanmakuInformation(text, textColor, userName, userLevel, levelColor, isAdmin)
        {
            UserId = userId,
            MedalName = medalName,
            MedalLevel = medalLevel,
            IsVip = isVip,
            UserTitle = userTitle,
        };
        return new LiveDanmakuReceivedEvent(danmaku, command, receivedAt);
    }

    private static LiveSuperChatEvent? ParseSuperChat(string command, JsonElement root, DateTimeOffset receivedAt)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var user = data.TryGetProperty("user_info", out var userInfo) && userInfo.ValueKind == JsonValueKind.Object
            ? userInfo
            : default;
        var id = GetOptionalString(data, "message_id") ?? string.Empty;
        var message = GetOptionalString(data, "message") ?? string.Empty;
        var price = GetOptionalInt32(data, "price");
        var userName = GetOptionalString(user, "uname") ?? string.Empty;
        var userId = user.ValueKind == JsonValueKind.Object ? GetOptionalInt64(user, "uid") : 0;
        var start = DateTimeOffset.FromUnixTimeSeconds(GetOptionalInt64(data, "start_time"));
        var end = DateTimeOffset.FromUnixTimeSeconds(GetOptionalInt64(data, "end_time"));
        var background = GetOptionalString(data, "background_color");

        return new LiveSuperChatEvent(
            new LiveSuperChatInformation(id, message, price, userName, userId, start, end, background),
            command,
            receivedAt);
    }

    private static LiveSuperChatDeletedEvent ParseSuperChatDelete(string command, JsonElement root, DateTimeOffset receivedAt)
    {
        var ids = new List<string>();
        if (root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("message_ids", out var messageIds)
            && messageIds.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in messageIds.EnumerateArray())
            {
                var id = ElementString(item);
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }

        return new LiveSuperChatDeletedEvent(ids, command, receivedAt);
    }

    private static LiveOnlineCountChangedEvent? ParseWatchedChange(string command, JsonElement root, DateTimeOffset receivedAt)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var count = GetOptionalInt32(data, "num");
        var text = GetOptionalString(data, "text_large") ?? GetOptionalString(data, "text_small");
        return new LiveOnlineCountChangedEvent(count, LiveOnlineCountSource.Watched, text, command, receivedAt);
    }

    private static LiveRoomTitleChangedEvent? ParseRoomChange(string command, JsonElement root, DateTimeOffset receivedAt)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = GetOptionalString(data, "title");
        return string.IsNullOrEmpty(title)
            ? null
            : new LiveRoomTitleChangedEvent(title, GetOptionalString(data, "area_name"), command, receivedAt);
    }

    private static string? GetOptionalDataString(JsonElement root, params string[] names)
    {
        if (!root.TryGetProperty("data", out var data))
        {
            return null;
        }

        if (data.ValueKind == JsonValueKind.String)
        {
            return data.GetString();
        }

        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            var value = GetOptionalString(data, name);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool HasCommandPrefix(string command, string expected)
    {
        if (!command.StartsWith(expected, StringComparison.Ordinal))
        {
            return false;
        }

        return command.Length == expected.Length || command[expected.Length] == ':';
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
        {
            return false;
        }

        value = ElementString(property);
        return value is not null;
    }

    private static string? GetOptionalString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? ElementString(property)
            : null;

    private static int GetOptionalInt32(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? (int)Math.Clamp(ElementLong(property), int.MinValue, int.MaxValue)
            : 0;

    private static long GetOptionalInt64(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? ElementLong(property)
            : 0;

    private static string? ElementString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null,
        };

    private static long ElementLong(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var value) ? value : 0,
            JsonValueKind.String => long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
            _ => 0,
        };

    private static string FormatColor(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return "#" + (number & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);
        }

        var text = ElementString(element);
        return string.IsNullOrEmpty(text) ? "#FFFFFF" : text;
    }
}

/// <summary>
/// 直播消息解析分类.
/// </summary>
internal enum LiveMessageParseKind
{
    /// <summary>产出强类型事件.</summary>
    Event,

    /// <summary>未知命令.</summary>
    Unknown,

    /// <summary>损坏消息.</summary>
    Malformed,

    /// <summary>已知但刻意忽略的重复消息.</summary>
    Ignored,
}

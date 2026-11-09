// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace Richasy.BiliKernel.Services.Media.Core;

internal static class LiveAdapter
{
    public static LiveInformation ToLiveInformation(this LiveRoomCard card)
    {
        var title = card.Title;
        var roomId = card.RoomId.ToString();
        var user = UserAdapterBase.CreateUserProfile(card.UserId ?? 0, card.UpName, default, 0d);
        var viewerCount = card.CoverRightContent.Text.ToCountNumber();
        var subtitle = card.CoverLeftContent.Text;
        var cover = card.Cover.ToVideoCover();
        var identifier = new MediaIdentifier(roomId, title, cover);
        var info = new LiveInformation(identifier, user, default);
        info.AddExtensionIfNotNull(LiveExtensionDataId.ViewerCount, viewerCount);
        info.AddExtensionIfNotNull(LiveExtensionDataId.Subtitle, subtitle);
        return info;
    }

    public static LiveInformation ToLiveInformation(this LiveFeedRoom room)
    {
        var title = room.Title;
        var roomId = room.RoomId.ToString();
        var viewerCount = System.Convert.ToDouble(room.ViewerCount);
        var user = UserAdapterBase.CreateUserProfile(room.UserId ?? 0, room.UserName, room.UserAvatar, 48d);
        var cover = room.Cover.ToVideoCover();
        var subtitle = room.AreaName;
        var identifier = new MediaIdentifier(roomId, title, cover);
        var info = new LiveInformation(identifier, user);
        info.AddExtensionIfNotNull(LiveExtensionDataId.ViewerCount, viewerCount);
        info.AddExtensionIfNotNull(LiveExtensionDataId.Subtitle, subtitle);
        return info;
    }

    public static LivePlayerView ToLivePlayerView(this LiveRoomDetailResponse detail)
    {
        var roomInfo = detail.RoomInformation;
        var title = roomInfo.Title;
        var roomId = roomInfo.RoomId.ToString();
        var desc = string.IsNullOrEmpty(roomInfo.Description) ? default : Regex.Replace(roomInfo.Description, @"<[^>]*>", string.Empty);

        if (!string.IsNullOrEmpty(desc) && desc.Length > 100)
        {
            desc = WebUtility.HtmlDecode(desc).Trim();
        }

        if (string.IsNullOrEmpty(desc))
        {
            desc = "暂无直播间介绍";
        }

        var viewerCount = roomInfo.ViewerCount;
        var cover = (roomInfo.Cover ?? roomInfo.Keyframe).ToVideoCover();
        var userInfo = detail.AnchorInformation.UserBasicInformation;
        var userProfile = UserAdapterBase.CreateUserProfile(roomInfo.UserId, userInfo.UserName, userInfo.Avatar, 96d);
        var partition = new LiveTag(roomInfo.AreaId.ToString(), roomInfo.AreaName, default);
        var startTime = DateTimeOffset.FromUnixTimeSeconds(roomInfo.LiveStartTime).ToLocalTime();
        var identifier = new MediaIdentifier(roomId, title, cover);
        var info = new LiveInformation(identifier, userProfile);
        info.AddExtensionIfNotNull(LiveExtensionDataId.ViewerCount, viewerCount);
        info.AddExtensionIfNotNull(LiveExtensionDataId.Description, desc);
        info.AddExtensionIfNotNull(LiveExtensionDataId.IsLiving, roomInfo.LiveStatus == 1);
        info.AddExtensionIfNotNull(LiveExtensionDataId.StartTime, startTime);
        return new LivePlayerView(info, partition);
    }

    public static LiveMediaInformation ToLiveMediaInformation(this LiveAppPlayResponse information)
    {
        if (information.PlayUrlInfo is null)
        {
            return default;
        }

        var id = information.RoomId.ToString();
        var playInfo = information.PlayUrlInfo.PlayUrl;
        // g_qn_desc 是平台完整清晰度表，不等于本房间真正可用的清晰度；
        // 只保留各编码 accept_qn / current_qn 里实际出现的 qn，避免直播里冒出 4K/杜比。
        var availableQualities = new HashSet<int>();
        foreach (var stream in playInfo.StreamList ?? [])
        {
            foreach (var format in stream.FormatList ?? [])
            {
                foreach (var codec in format.CodecList ?? [])
                {
                    if (codec.AcceptQualities is { Count: > 0 })
                    {
                        foreach (var quality in codec.AcceptQualities)
                        {
                            availableQualities.Add(quality);
                        }
                    }

                    if (codec.CurrentQuality > 0)
                    {
                        availableQualities.Add(codec.CurrentQuality);
                    }
                }
            }
        }

        var descriptions = playInfo.Descriptions ?? [];
        var formats = new List<PlayerFormatInformation>();
        foreach (var item in descriptions
            .Where(item => availableQualities.Count == 0 || availableQualities.Contains(item.Quality))
            .OrderByDescending(item => item.Quality))
        {
            formats.Add(new PlayerFormatInformation(item.Quality, item.Description, false, false));
        }

        var lines = new List<LiveLineInformation>();
        foreach (var stream in playInfo.StreamList)
        {
            foreach (var format in stream.FormatList)
            {
                foreach (var codec in format.CodecList)
                {
                    var name = codec.CodecName;
                    var urls = codec.Urls.Select(p => new LivePlayUrl(stream.ProtocolName, p.Host, codec.BaseUrl, p.Extra)).ToList();
                    lines.Add(new LiveLineInformation(
                        name,
                        codec.CurrentQuality,
                        codec.AcceptQualities,
                        urls,
                        string.IsNullOrWhiteSpace(format.FormatName) ? null : format.FormatName));
                }
            }
        }

        return new LiveMediaInformation(id, formats, lines);
    }

    public static IReadOnlyList<LiveDanmakuInformation> ToRecentLiveDanmaku(this LiveHistoryDanmakuResponse response)
    {
        if (response is null)
        {
            return [];
        }

        var result = new List<LiveDanmakuInformation>();
        AppendHistoryItems(result, response.Admin);
        AppendHistoryItems(result, response.Room);
        return result;
    }

    private static void AppendHistoryItems(List<LiveDanmakuInformation> result, List<LiveHistoryDanmakuItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Text))
            {
                continue;
            }

            // gethistory 有两套结构：旧版平铺 nickname/user_level，新版嵌在 user.base / user.medal 里。
            // 按新版优先解析，再回退旧版，最后才用可读占位，避免首屏出现无发言人。
            var userId = item.User?.Uid > 0 ? item.User.Uid : item.UserId;
            var userName = item.User?.Base?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = item.NickName;
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = item.UserName;
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = userId > 0 ? $"用户{userId}" : "匿名用户";
            }

            var level = item.UserLevel;
            var medalName = item.User?.Medal is { } medal
                ? (!string.IsNullOrEmpty(medal.Name) ? medal.Name : medal.LegacyName)
                : null;
            var medalLevel = item.User?.Medal is { } medalInfo
                ? (medalInfo.Level > 0 ? medalInfo.Level : medalInfo.LegacyLevel)
                : 0;
            var medalColor = item.User?.Medal is { } medalValue
                ? ToHexColor(ParseMedalColor(medalValue.StartColor) ?? ParseMedalColor(medalValue.LegacyColor) ?? 0)
                : null;
            var guardLevel = item.User?.Guard?.Level ?? item.GuardLevel;
            var emotes = BuildHistoryEmotes(item);
            var danmaku = new LiveDanmakuInformation(
                item.Text,
                ToHexColor(item.Color),
                userName,
                level > 0 ? "UL" + level.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                string.Empty,
                item.IsAdmin == 1)
            {
                UserId = userId,
                GuardLevel = guardLevel,
                UserLevelNumber = level,
                UserFace = item.User?.Base?.Face,
                MedalName = string.IsNullOrEmpty(medalName) ? null : medalName,
                MedalLevel = medalLevel > 0 ? medalLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                MedalColor = string.IsNullOrEmpty(medalName) ? null : medalColor,
                Emotes = emotes,
            };
            result.Add(danmaku);
        }
    }

    private static int? ParseMedalColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.StartsWith('#'))
        {
            return int.TryParse(raw[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex)
                ? hex
                : null;
        }

        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static IReadOnlyDictionary<string, LiveDanmakuEmote>? BuildHistoryEmotes(LiveHistoryDanmakuItem item)
    {
        var result = new Dictionary<string, LiveDanmakuEmote>(StringComparer.Ordinal);
        if (item.Emots is { Count: > 0 })
        {
            foreach (var pair in item.Emots)
            {
                if (!string.IsNullOrEmpty(pair.Value?.Url))
                {
                    result[pair.Key.Trim('[', ']')] = new LiveDanmakuEmote(pair.Value.Url, pair.Value.Width, pair.Value.Height);
                }
            }
        }

        if (!string.IsNullOrEmpty(item.Emoticon?.Url))
        {
            // 旧字段只给一个表情，通常就是整条正文；用正文本身作为键。
            result[item.Text.Trim('[', ']')] = new LiveDanmakuEmote(item.Emoticon.Url, item.Emoticon.Width, item.Emoticon.Height);
        }

        return result.Count == 0 ? null : result;
    }

    private static string ToHexColor(int color)
        => color <= 0 ? "#FFFFFF" : "#" + (color & 0xFFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);

    public static Partition ToPartition(this LiveAreaGroup group)
    {
        var id = group.Id.ToString();
        var name = group.Name;
        var children = group.AreaList.Select(p => p.ToPartition()).ToList();
        return new Partition(id, name, children: children);
    }

    public static Partition ToPartition(this LiveArea area)
    {
        var id = area.Id.ToString();
        var name = area.Name;
        var logo = string.IsNullOrEmpty(area.Cover) ? default : area.Cover.ToImage();
        return new Partition(id, name, logo, parentId: area.ParentId.ToString());
    }

    public static LiveTag ToLiveTag(this LiveAreaDetailTag tag)
    {
        var id = tag.Id.ToString();
        var name = tag.Name;
        var sortType = tag.SortType;
        return new LiveTag(id, name, sortType);
    }
}

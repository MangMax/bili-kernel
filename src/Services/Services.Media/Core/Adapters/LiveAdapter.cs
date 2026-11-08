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

            var danmaku = new LiveDanmakuInformation(
                item.Text,
                ToHexColor(item.Color),
                item.NickName ?? string.Empty,
                item.UserLevel > 0 ? "UL" + item.UserLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                string.Empty,
                item.IsAdmin == 1)
            {
                UserId = item.UserId,
                GuardLevel = item.GuardLevel,
            };
            result.Add(danmaku);
        }
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

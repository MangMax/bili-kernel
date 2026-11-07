// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Bilibili.App.Playurl.V1;
using Bilibili.App.View.V1;
using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Base;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Models;
using System.Text.RegularExpressions;

namespace Richasy.BiliKernel.Services.Media.Core;

internal static class VideoAdapter
{
    private static void AddCategory(VideoInformation video, long? tagId, string? tagName, long? regionId = null, string? regionName = null)
    {
        var hasTag = !string.IsNullOrWhiteSpace(tagName);
        var name = hasTag ? tagName : regionName;
        if (string.IsNullOrWhiteSpace(name)) return;
        var id = hasTag ? tagId : regionId;
        video.AddExtensionIfNotNull(VideoExtensionDataId.TagName, name.Trim());
        if (id is > 0) video.AddExtensionIfNotNull(VideoExtensionDataId.TagId, id);
    }

    public static VideoInformation ToVideoInformation(this CuratedPlaylistVideo video)
    {
        var identifier = new MediaIdentifier(video.AvId.ToString(), video.Title, video.Cover.ToVideoCover());
        var publisher = video.Owner.ToPublisherProfile();
        var communityInfo = video.Stat.ToVideoCommunityInformation();
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(video.PublishTime ?? 0).ToLocalTime();
        var info = new VideoInformation(
            identifier,
            publisher,
            video.Duration,
            video.BvId,
            publishTime,
            communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, video.Cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.RecommendReason, video.RecommendReason?.Content);
        info.RecommendationFeedback = new RecommendationFeedbackContext
        {
            Source = RecommendationFeedbackSource.Web, VideoId = video.AvId.ToString(),
            PublisherId = video.Owner?.UserId, Goto = video.Goto, TrackId = video.TrackId,
            Reasons = new List<RecommendationFeedbackReason>
            {
                new() { Id = 1, Operation = RecommendationFeedbackOperation.NotInterested },
                new() { Id = 2, Operation = RecommendationFeedbackOperation.AvoidPublisher, Mid = video.Owner?.UserId },
            }.AsReadOnly(),
        };
        AddCategory(info, video.PartitionId, video.PartitionName);
        bool? following = video.IsFollowed switch { 0 => false, 1 => true, _ => null };
        info.AddExtensionIfNotNull(VideoExtensionDataId.PublisherIsFollowed, following);

        return info;
    }

    public static VideoInformation ToVideoInformation(this RecommendCard card)
    {
        var identifier = new MediaIdentifier(card.Args.Aid.ToString(), card.Title, card.Cover.ToVideoCover());
        var publisher = new PublisherInfo
        {
            Publisher = card.Args.UpName,
            UserId = card.Args.UpId ?? 0,
            PublisherAvatar = string.IsNullOrWhiteSpace(card.Mask?.Avatar?.Cover) ? card.Avatar?.Cover : card.Mask.Avatar.Cover,
        };
        var communityInfo = new VideoCommunityInformation(card.Args.Aid.ToString(), card.PlayCountText.ToCountNumber(), card.DanmakuCountText.ToCountNumber());
        var duration = card.DurationText.ToDurationSeconds();
        var info = new VideoInformation(
            identifier,
            publisher.ToPublisherProfile(),
            card.PlayerArgs?.Duration ?? duration,
            publishTime: card.PublishTime is > 0 ? DateTimeOffset.FromUnixTimeSeconds(card.PublishTime.Value).ToLocalTime() : null,
            communityInformation: communityInfo);

        info.RecommendationFeedback = new RecommendationFeedbackContext
        {
            Source = RecommendationFeedbackSource.App,
            VideoId = card.Parameter ?? card.Args.Aid.ToString(), Goto = card.Goto ?? card.CardGoto,
            PublisherId = card.Args.UpId, TrackId = card.TrackId,
            Reasons = (card.OverflowFlyout ?? []).SelectMany(menu => (menu.Reasons ?? []).Select(reason =>
            {
                var option = new RecommendationFeedbackReason
                {
                    Id = reason.Id ?? 0, Type = menu.Type, Name = reason.Name,
                    Mid = reason.Mid, Rid = reason.Rid, TagId = reason.TagId,
                };
                option.Operation = option.GetProtocolOperation(RecommendationFeedbackSource.App);
                return option;
            })).ToList().AsReadOnly(),
        };
        var flyoutItems = (card.OverflowFlyout ?? []).Where(p => p.Reasons != null).Select(p => new VideoOverflowFlyoutItem
        {
            Id = p.Type,
            Title = p.Title,
            Options = p.Reasons.GroupBy(r => r.Id).ToDictionary(g => g.Key.ToString() ?? string.Empty, g => g.First().Name ?? string.Empty),
        }).ToList();

        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, card.PlayerArgs?.Cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.RecommendReason, card.RecommendReason ?? card.BottomReason);
        var following = card.IsAttended ?? (string.Equals(card.BottomReason?.Trim(), "已关注", StringComparison.Ordinal) ? true : (bool?)null);
        info.AddExtensionIfNotNull(VideoExtensionDataId.PublisherIsFollowed, following);
        // This is a server-provided display description, not a fabricated timestamp.
        var extra = card.MultipleDescription?.Extra ?? card.Description;
        var separator = extra?.LastIndexOf('·') ?? -1;
        if (separator >= 0 && separator < extra!.Length - 1)
            info.AddExtensionIfNotNull(VideoExtensionDataId.Subtitle, extra.Substring(separator + 1).Trim());
        AddCategory(info, card.Args?.TagId, card.Args?.TagName, card.Args?.Rid, card.Args?.Rname);
        info.AddExtensionIfNotNull(VideoExtensionDataId.OverflowFlyout, new VideoOverflowFlyout { Items = flyoutItems });

        return info;
    }

    public static VideoInformation ToVideoInformation(this Bilibili.App.Card.V1.Card card)
    {
        var v5 = card.SmallCoverV5;
        var baseCard = v5.Base;
        var shareInfo = baseCard.ThreePointV4.SharePlane;
        var title = baseCard.Title;
        var id = shareInfo.Aid.ToString();
        var bvId = shareInfo.Bvid;
        var publisherInfo = new PublisherInfo
        {
            Publisher = shareInfo.Author,
            UserId = shareInfo.AuthorId,
            PublisherAvatar = v5.Up?.Avatar?.Cover,
        };
        var description = shareInfo.Desc;

        var cover = baseCard.Cover.ToVideoCover();
        var highlight = v5.RcmdReasonStyle?.Text ?? string.Empty;
        var duration = v5.CoverRightText1.ToDurationSeconds();
        var identifier = new MediaIdentifier(id, title, cover);
        var publishTimeText = v5.RightDesc2.Split('·').Last().Trim();

        var playCount = baseCard.ThreePointV4.SharePlane.PlayNumber.ToCountNumber("次");
        var communityInfo = new VideoCommunityInformation(id, playCount);

        var info = new VideoInformation(
            identifier,
            publisherInfo.ToPublisherProfile(),
            duration,
            bvId,
            communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Subtitle, publishTimeText);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Description, description);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.RecommendReason, highlight);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, shareInfo.FirstCid);
        AddCategory(info, baseCard.Args?.Tid, baseCard.Args?.Tname, baseCard.Args?.Rid, baseCard.Args?.Rname);

        return info;
    }

    public static VideoInformation ToVideoInformation(this Bilibili.App.Show.Rank.V1.Item item)
    {
        var id = item.Param;
        var title = item.Title;
        var duration = item.Duration;
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(item.PubDate).ToLocalTime();

        var user = new PublisherInfo { UserId = item.Mid, Publisher = item.Name, PublisherAvatar = item.Face }.ToPublisherProfile();
        var cover = item.Cover.ToVideoCover();
        var communityInfo = new VideoCommunityInformation(
            item.Param,
            item.Play,
            item.Danmaku,
            item.Like,
            item.Favourite,
            commentCount: item.Reply);

        var identifier = new MediaIdentifier(id, title, cover);
        var info = new VideoInformation(
            identifier,
            user,
            duration,
            publishTime: publishTime,
            communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, item.Cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagId, item.Rid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagName, item.Rname);
        return info;
    }

    public static VideoInformation ToVideoInformation(this PartitionVideo video)
    {
        var identifier = new MediaIdentifier(video.Parameter, video.Title, video.Cover.ToVideoCover());
        var communityInfo = new VideoCommunityInformation(video.Parameter, video.PlayCount, video.DanmakuCount, video.LikeCount, favoriteCount: video.FavouriteCount, commentCount: video.ReplyCount);
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(video.PublishDateTime ?? 0).ToLocalTime();
        var info = new VideoInformation(
            identifier,
            default,
            video.Duration,
            publishTime: publishTime,
            communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Subtitle, video.Publisher);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagName, video.PartitionName);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        return info;
    }

    public static VideoInformation ToVideoInformation(this ViewReply reply)
    {
        var archive = reply.Arc;
        var identifier = new MediaIdentifier(archive.Aid.ToString(), archive.Title, archive.Pic.ToVideoCover());
        var communityInfo = new VideoCommunityInformation(archive.Aid.ToString(), archive.Stat.View, archive.Stat.Danmaku, archive.Stat.Like, archive.Stat.Fav, archive.Stat.Coin, archive.Stat.Reply, archive.Stat.Share);
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(archive.Pubdate).ToLocalTime();
        var author = archive.Author.ToPublisherProfile();
        var desc = archive.Desc;
        var isOriginal = archive.Copyright == 1;
        var collaborators = reply.Staff?.Select(p => p.ToPublisherProfile()).ToList();
        var createTime = DateTimeOffset.FromUnixTimeSeconds(archive.Ctime).ToLocalTime();
        var videoInfo = new VideoInformation(identifier, author, archive.Duration, reply.Bvid, publishTime, collaborators, communityInfo);
        AddCategory(videoInfo, null, archive.Tag, archive.TypeId, archive.TypeName);
        videoInfo.AddExtensionIfNotNull(VideoExtensionDataId.Description, desc);
        videoInfo.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        videoInfo.AddExtensionIfNotNull(VideoExtensionDataId.Cid, archive.FirstCid);
        videoInfo.AddExtensionIfNotNull(VideoExtensionDataId.CreateTime, createTime);
        videoInfo.AddExtensionIfNotNull(VideoExtensionDataId.IsOriginal, isOriginal);
        return videoInfo;
    }

    public static VideoInformation ToVideoInformation(this Relate relate)
    {
        var title = relate.Title;
        var identifier = new MediaIdentifier(relate.Aid.ToString(), title, relate.Pic.ToVideoCover());
        var desc = relate.Desc;
        var publisher = relate.Author.ToPublisherProfile();
        var communityInfo = new VideoCommunityInformation(relate.Aid.ToString(), relate.Stat.View, relate.Stat.Danmaku, relate.Stat.Like, relate.Stat.Fav, relate.Stat.Coin, relate.Stat.Reply, relate.Stat.Share);
        var info = new VideoInformation(identifier, publisher, relate.Duration, communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Description, desc);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, relate.Cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagName, relate.TagName);
        return info;
    }

    public static VideoInformation ToVideoInformation(this Episode episode)
    {
        var id = episode.Aid.ToString();
        var cid = episode.Cid;
        var title = Regex.Replace(episode.Title, "<[^>]+>", string.Empty);
        var duration = episode.Page.Duration;
        var publisher = episode.Author.ToPublisherProfile();
        var communityInfo = new VideoCommunityInformation(id, episode.Stat.View, episode.Stat.Danmaku, episode.Stat.Like, episode.Stat.Fav, episode.Stat.Coin, episode.Stat.Reply, episode.Stat.Share);
        var cover = episode.Cover.ToVideoCover();
        var identifier = new MediaIdentifier(id, title, cover);
        var info = new VideoInformation(identifier, publisher, duration, communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        return info;
    }

    public static VideoInformation ToVideoInformation(this VideoPageVideo video)
    {
        var title = video.title;
        var id = video.aid.ToString();
        var bvid = video.bvid;
        var duration = video.duration;
        var cover = video.pic.ToVideoCover();
        var publisher = video.owner.ToPublisherProfile();
        var desc = video.desc;
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(video.pubdate).ToLocalTime();
        var communityInfo = video.stat.ToVideoCommunityInformation();
        var identifier = new MediaIdentifier(id, title, cover);
        var info = new VideoInformation(identifier, publisher, duration, bvid, publishTime, collaborators: video.staff?.Select(p => p.ToPublisherProfile()).ToList(), communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Description, desc);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, video.cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.IsCollaborativeSubmission, video.rights?.is_cooperation == 1);
        info.AddExtensionIfNotNull(VideoExtensionDataId.IsChargingExclusive, video.is_upower_exclusive);
        AddCategory(info, video.tid, video.tname);
        return info;
    }

    public static VideoInformation ToVideoInformation(this VideoEpisode episode)
    {
        var id = episode.aid.ToString();
        var cid = episode.cid.ToString();
        var title = Regex.Replace(episode.title, "<[^>]+>", string.Empty);
        var duration = episode.arc.duration;
        var communityInfo = episode.arc.stat.ToVideoCommunityInformation();
        var cover = episode.arc.pic?.ToVideoCover();
        var time = DateTimeOffset.FromUnixTimeSeconds(episode.arc.pubdate).ToLocalTime();
        var identifier = new MediaIdentifier(id, title, cover);
        var info = new VideoInformation(identifier, default, duration, publishTime: time, communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, episode.cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        return info;
    }

    public static VideoInformation ToVideoInformation(this VideoPageRelated video)
    {
        var id = video.aid.ToString();
        var title = video.title;
        var duration = video.duration;
        var cover = video.pic.ToVideoCover();
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(video.pubdate).ToLocalTime();
        var publisher = video.owner.ToPublisherProfile();
        var communityInfo = video.stat.ToVideoCommunityInformation();
        var identifier = new MediaIdentifier(id, title, cover);
        var info = new VideoInformation(identifier, publisher, duration, video.bvid, publishTime, communityInformation: communityInfo);
        info.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Description, video.desc);
        info.AddExtensionIfNotNull(VideoExtensionDataId.Cid, video.cid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagId, video.tid);
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagName, video.tname);
        info.AddExtensionIfNotNull(VideoExtensionDataId.FirstFrame, video.first_frame);
        info.AddExtensionIfNotNull(VideoExtensionDataId.IsOriginal, video.copyright == 1);
        info.AddExtensionIfNotNull(VideoExtensionDataId.PublishLocation, video.pub_location);
        info.AddExtensionIfNotNull(VideoExtensionDataId.ShortLink, video.short_link_v2);
        return info;
    }

    public static VideoPart ToVideoPart(this ViewPage data)
    {
        var identifier = new MediaIdentifier(data.Page.Cid.ToString(), data.Page.Part, data.Page.FirstFrame?.ToVideoCover());
        var duration = data.Page.Duration;
        return new VideoPart(identifier, data.Page.Page_, Convert.ToInt32(duration));
    }

    public static VideoPart ToVideoPart(this VideoPagePart part)
    {
        var identifier = new MediaIdentifier(part.cid.ToString(), part.part, part.first_frame?.ToImage());
        var duration = part.duration;
        return new VideoPart(identifier, part.page, duration);
    }

    public static VideoCommunityInformation ToVideoCommunityInformation(this VideoStatusInfo statusInfo)
        => new VideoCommunityInformation(
            statusInfo.Aid.ToString(),
            statusInfo.PlayCount,
            statusInfo.DanmakuCount,
            statusInfo.LikeCount,
            favoriteCount: statusInfo.FavoriteCount,
            coinCount: statusInfo.CoinCount,
            commentCount: statusInfo.ReplyCount);

    public static InteractiveVideoSnapshot ToInteractiveVideoSnapshot(this Interaction interaction)
    {
        if (interaction is null)
        {
            return default;
        }

        var partId = interaction.HistoryNode?.Cid.ToString();
        var nodeId = interaction.HistoryNode?.NodeId.ToString();
        var graphVersion = interaction.GraphVersion.ToString();

        return new InteractiveVideoSnapshot(graphVersion);
    }

    public static Partition ToPartition(this VideoPartition partition)
    {
        var id = partition.Tid.ToString();
        var name = partition.Name;
        var logo = string.IsNullOrEmpty(partition.Logo) || !partition.Logo.StartsWith("http")
            ? null
            : partition.Logo.ToImage();
        var children = partition.Children?.Select(p => p.ToPartition()).ToList();
        if (children?.Count > 0)
        {
            children.ForEach(p => p.ParentId = id);
        }

        return new Partition(id, name, logo, children);
    }

    public static VideoSeason ToVideoSeason(this Section section)
    {
        var id = section.Id.ToString();
        var title = section.Title;
        var videos = section.Episodes.Select(p => p.ToVideoInformation()).ToList();
        return new VideoSeason(id, title, videos);
    }

    public static VideoSeason ToVideoSeason(this VideoPageSection section)
    {
        var id = section.id.ToString();
        var title = section.title;
        var videos = section.episodes.Select(p => p.ToVideoInformation()).ToList();
        return new VideoSeason(id, title, videos);
    }

    public static PlayerProgress ToPlayerProgress(this ViewReply reply)
    {
        // 当历史记录为空，或者当前视频为交互视频时（交互视频按照节点记录历史），返回 null.
        if (reply.History == null || reply.Interaction != null)
        {
            return null;
        }

        var history = reply.History;
        var status = history.Progress switch
        {
            0 => PlayerProgressStatus.NotStarted,
            -1 => PlayerProgressStatus.Finish,
            _ => PlayerProgressStatus.Playing,
        };

        var progress = status == PlayerProgressStatus.Playing ? history.Progress : 0;
        var id = history.Cid.ToString();
        return new PlayerProgress(progress, status, id);
    }

    /// <summary>
    /// 从播放器 WBI 响应中提取视频章节.
    /// </summary>
    /// <param name="response">播放器 WBI 响应.</param>
    /// <returns>章节列表，未标记章节时为空.</returns>
    /// <remarks>
    /// 章节数据只出现在 /x/player/wbi/v2 响应里，
    /// 播放地址接口 /x/player/playurl 不返回 view_points.
    /// </remarks>
    public static List<VideoChapter> ToVideoChapters(this PlayerWbiResponse response)
    {
        var points = response?.ViewPoints;
        if (points is null || points.Count == 0)
        {
            return [];
        }

        return points
            .Where(p => !string.IsNullOrWhiteSpace(p.Content) && p.To > p.From && p.From >= 0)
            .Select(p => new VideoChapter(p.Content.Trim(), p.From, p.To))
            .ToList();
    }

    public static PlayerProgress ToPlayerProgress(this PlayerWbiResponse response)
    {
        var status = response.last_play_time switch
        {
            0 => PlayerProgressStatus.NotStarted,
            -1 => PlayerProgressStatus.Finish,
            _ => PlayerProgressStatus.Playing,
        };

        var progress = status == PlayerProgressStatus.Playing ? response.last_play_time : 0;
        var id = response.last_play_cid.ToString();
        return new PlayerProgress(progress, status, id);
    }

    public static VideoOperationInformation ToVideoOperationInformation(this ViewReply reply)
    {
        var reqUser = reply.ReqUser;
        var isLiked = reqUser.Like == 1;
        var isCoined = reqUser.Coin == 1;
        var isFavorited = reqUser.Favorite == 1;
        return new VideoOperationInformation(
            reply.Arc.Aid.ToString(),
            isLiked,
            isCoined,
            isFavorited,
            reqUser.Attention > 0);
    }

    public static VideoOperationInformation ToVideoOperationInformation(this ArchiveRelationResponse response, string aid)
    {
        return new VideoOperationInformation(
            aid,
            response.like,
            response.coin > 0,
            response.favorite,
            response.attention);
    }

    public static VideoPlayerView ToVideoPlayerView(this ViewReply reply, IList<VideoSeason>? seasons)
    {
        var vinfo = reply.ToVideoInformation();
        var userCommunity = reply.ToUserCommunityInformation();
        var parts = reply.Pages?.Select(p => p.ToVideoPart()).ToList();
        var interaction = reply.Interaction.ToInteractiveVideoSnapshot();
        var operation = reply.ToVideoOperationInformation();
        var recommends = reply.Relates?.Where(p => p.Goto == "av").Select(p => p.ToVideoInformation()).ToList();
        var history = reply.ToPlayerProgress();
        var tags = reply.Tag.Select(p => new BiliTag(p.Id.ToString(), p.Name)).ToList();
        var ratio = BasicExtensions.CalcVideoAspectRatio(Convert.ToInt32(reply.Arc.Dimension.Width), Convert.ToInt32(reply.Arc.Dimension.Height));
        var isInteraction = reply.Interaction != null;
        return new VideoPlayerView(vinfo, userCommunity, parts, seasons, recommends, tags, history, interaction, operation, ratio, isInteraction);
    }

    public static VideoPlayerView ToVideoPlayerView(this VideoPageResponse response, PlayerProgress? progress, VideoOperationInformation? operation)
    {
        var vinfo = response.View.ToVideoInformation();
        var userCommunity = response.Card.ToUserCommunityInformation();
        var parts = response.View.pages?.Select(p => p.ToVideoPart()).ToList();
        var seasons = response.View.ugc_season?.sections?.Select(p => p.ToVideoSeason()).ToList();
        var recommends = response.Related?.Select(p => p.ToVideoInformation()).ToList();
        var tags = response.Tags?.Select(p => new BiliTag(p.tag_id.ToString(), p.tag_name)).ToList();
        var ratio = BasicExtensions.CalcVideoAspectRatio(response.View.dimension.width, response.View.dimension.height);
        var isInteraction = response.View.rights.is_stein_gate == 1;
        return new VideoPlayerView(vinfo, userCommunity, parts, seasons, recommends, tags, progress, operation: operation, aspectRatio: ratio, isInteractionVideo: isInteraction);
    }

    public static DashMediaInformation ToDashMediaInformation(this PlayViewReply reply)
    {
        if (reply.VideoInfo is null)
        {
            return default;
        }

        var info = reply.VideoInfo;
        var videoStreams = info.StreamList?.ToList();
        var audioStreams = info.DashAudio?.ToList();
        var videos = new List<DashSegmentInformation>();
        var audios = new List<DashSegmentInformation>();
        var formats = new List<PlayerFormatInformation>();

        if (videoStreams is not null)
        {
            foreach (var video in videoStreams)
            {
                if (video.DashVideo is null)
                {
                    continue;
                }

                var seg = new DashSegmentInformation(
                    video.StreamInfo.Quality.ToString(),
                    video.DashVideo.BaseUrl,
                    default,
                    default,
                    video.DashVideo.Codecid.ToString(),
                    default,
                    default,
                    default,
                    default,
                    video.DashVideo.BackupUrl.ToList());
                var format = new PlayerFormatInformation(
                    Convert.ToInt32(video.StreamInfo.Quality),
                    video.StreamInfo.Description,
                    video.StreamInfo.NeedVip,
                    video.StreamInfo.NeedLogin);
                videos.Add(seg);
                formats.Add(format);
            }
        }

        if (audioStreams is not null)
        {
            foreach (var audio in audioStreams)
            {
                var seg = new DashSegmentInformation(
                    audio.Id.ToString(),
                    audio.BaseUrl,
                    default,
                    default,
                    audio.Codecid.ToString(),
                    default,
                    default,
                    default,
                    default,
                    audio.BackupUrl.ToList());

                audios.Add(seg);
            }
        }

        if (videos.Count == 0)
        {
            videos = default;
        }

        if (audios.Count == 0)
        {
            audios = default;
        }

        return new DashMediaInformation(videos, audios, formats);
    }

    public static DashMediaInformation ToDashMediaInformation(this PlayerInformation information, string? playCheck = null, bool isPreview = false)
    {
        var dash = information.VideoInformation;

        var videos = new List<DashSegmentInformation>();
        var audios = new List<DashSegmentInformation>();
        var formats = new List<PlayerFormatInformation>();

        if (information.SupportFormats?.Count > 0)
        {
            foreach (var format in information.SupportFormats)
            {
                var playerFormat = new PlayerFormatInformation(
                    format.Quality,
                    format.Description,
                    needVip: format.Quality >= 112,
                    needLogin: format.Quality >= 80);

                formats.Add(playerFormat);
            }
        }

        var acceptedQualities = information.AcceptQualities is { Count: > 0 } ? information.AcceptQualities : default;

        if (dash == null)
        {
            if (!isPreview)
            {
                return default;
            }

            var previewUrls = information.FlvInformation?
                .Where(p => !string.IsNullOrEmpty(p.Url))
                .Select(p => p.Url)
                .ToList();
            previewUrls = previewUrls?.Count > 0 ? previewUrls : default;

            return new DashMediaInformation(default, default, formats, information.Duration / 1000, playCheck: playCheck, isPreview: true, previewUrls: previewUrls, acceptedQualities: acceptedQualities);
        }

        if (dash.Video?.Count > 0)
        {
            foreach (var video in dash.Video)
            {
                var seg = new DashSegmentInformation(
                    video.Id.ToString(),
                    video.BaseUrl,
                    video.BandWidth,
                    video.MimeType,
                    video.Codecs,
                    video.Width,
                    video.Height,
                    video.SegmentBase.Initialization,
                    video.SegmentBase.IndexRange,
                    video.BackupUrl,
                    video.CodecId);

                videos.Add(seg);
            }
        }

        if (dash.Audio?.Count > 0)
        {
            foreach (var audio in dash.Audio)
            {
                var seg = new DashSegmentInformation(
                    audio.Id.ToString(),
                    audio.BaseUrl,
                    audio.BandWidth,
                    audio.MimeType,
                    audio.Codecs,
                    audio.Width,
                    audio.Height,
                    audio.SegmentBase.Initialization,
                    audio.SegmentBase.IndexRange,
                    audio.BackupUrl,
                    audio.CodecId);

                audios.Add(seg);
            }
        }

        if (videos.Count == 0)
        {
            videos = default;
        }

        if (audios.Count == 0)
        {
            audios = default;
        }

        // Web playurl 的 timelength 单位是毫秒，对外统一为秒.
        return new DashMediaInformation(videos, audios, formats, information.Duration / 1000, playCheck: playCheck, isPreview: isPreview, acceptedQualities: acceptedQualities);
    }
}

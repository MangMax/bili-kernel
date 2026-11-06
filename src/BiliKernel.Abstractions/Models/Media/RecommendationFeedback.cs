// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>推荐反馈操作，独立于视频点踩及账号关系。</summary>
public enum RecommendationFeedbackOperation
{
    /// <summary>不感兴趣。</summary>
    NotInterested,
    /// <summary>不想看此作者。</summary>
    AvoidPublisher,
}

/// <summary>推荐协议来源。</summary>
public enum RecommendationFeedbackSource
{
    /// <summary>App 推荐。</summary>
    App,
    /// <summary>网页推荐。</summary>
    Web,
}

/// <summary>单个服务端原因。相同 ID 的不同目标保持独立。</summary>
public sealed class RecommendationFeedbackReason
{
    /// <summary>原因 ID。</summary>
    public long Id { get; set; }
    /// <summary>原始菜单类型。</summary>
    public string? Type { get; set; }
    /// <summary>展示名称，不参与协议判断。</summary>
    public string? Name { get; set; }
    /// <summary>作者目标。</summary>
    public long? Mid { get; set; }
    /// <summary>分区目标。</summary>
    public long? Rid { get; set; }
    /// <summary>标签目标。</summary>
    public long? TagId { get; set; }
    /// <summary>可执行操作；未知或不支持的原因为空。</summary>
    public RecommendationFeedbackOperation? Operation { get; set; }
}

/// <summary>推荐条目自含的提交上下文。普通视频不具有此能力。</summary>
public sealed class RecommendationFeedbackContext
{
    /// <summary>来源。</summary>
    public RecommendationFeedbackSource Source { get; set; }
    /// <summary>推荐视频目标。</summary>
    public string VideoId { get; set; } = string.Empty;
    /// <summary>服务端跳转类型。</summary>
    public string? Goto { get; set; }
    /// <summary>推荐作者。</summary>
    public long? PublisherId { get; set; }
    /// <summary>服务端追踪标识。</summary>
    public string? TrackId { get; set; }
    /// <summary>全部原始原因。</summary>
    public IReadOnlyList<RecommendationFeedbackReason> Reasons { get; set; } = Array.Empty<RecommendationFeedbackReason>();
    /// <summary>可提交选项。空列表表示该条目不支持反馈。</summary>
    public IReadOnlyList<RecommendationFeedbackReason> Options => Reasons.Where(p =>
        Goto == "av" && long.TryParse(VideoId, out var id) && id > 0
        && (p.Operation == RecommendationFeedbackOperation.NotInterested || p.Operation == RecommendationFeedbackOperation.AvoidPublisher)
        && (p.Operation != RecommendationFeedbackOperation.AvoidPublisher || (p.Mid ?? PublisherId) > 0)
        && (Source == RecommendationFeedbackSource.Web
            ? p.Id == (p.Operation == RecommendationFeedbackOperation.NotInterested ? 1 : 2)
            : Source == RecommendationFeedbackSource.App && p.Type == "dislike"
                && p.Id == (p.Operation == RecommendationFeedbackOperation.NotInterested ? 1 : 4)))
        .ToList().AsReadOnly();
}

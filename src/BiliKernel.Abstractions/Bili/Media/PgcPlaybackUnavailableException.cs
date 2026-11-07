// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using RichasyKernel;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 番剧播放不可用的原因.
/// </summary>
public enum PgcPlaybackUnavailableReason
{
    /// <summary>
    /// 当前账号没有观看该集的权益（如未购买或未开通大会员）.
    /// </summary>
    NotEntitled,

    /// <summary>
    /// 需要登录后才能观看.
    /// </summary>
    LoginRequired,

    /// <summary>
    /// 内容受地区限制.
    /// </summary>
    RegionRestricted,

    /// <summary>
    /// 内容不可用（如下架或不存在）.
    /// </summary>
    ContentUnavailable,
}

/// <summary>
/// 番剧播放不可用，携带可判定的原因与服务端原始响应.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only the classified server response may construct this exception; standard constructors would lose the reason and server response.")]
public sealed class PgcPlaybackUnavailableException : KernelException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcPlaybackUnavailableException"/> class.
    /// </summary>
    /// <param name="reason">播放不可用的原因.</param>
    /// <param name="code">服务端响应代码.</param>
    /// <param name="serverMessage">服务端响应消息.</param>
    public PgcPlaybackUnavailableException(PgcPlaybackUnavailableReason reason, int code, string? serverMessage)
        : base(BuildMessage(reason, code, serverMessage))
    {
        Reason = reason;
        Code = code;
        ServerMessage = serverMessage;
    }

    /// <summary>
    /// 播放不可用的原因.
    /// </summary>
    public PgcPlaybackUnavailableReason Reason { get; }

    /// <summary>
    /// 服务端响应代码.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 服务端响应消息原文.
    /// </summary>
    public string? ServerMessage { get; }

    private static string BuildMessage(PgcPlaybackUnavailableReason reason, int code, string? serverMessage)
    {
        var description = reason switch
        {
            PgcPlaybackUnavailableReason.NotEntitled => "当前账号没有观看权益",
            PgcPlaybackUnavailableReason.LoginRequired => "需要登录",
            PgcPlaybackUnavailableReason.RegionRestricted => "内容受地区限制",
            _ => "内容不可用",
        };

        return $"番剧播放不可用（{description}）：code={code}，message={serverMessage ?? "服务端未返回可播放信息"}";
    }
}

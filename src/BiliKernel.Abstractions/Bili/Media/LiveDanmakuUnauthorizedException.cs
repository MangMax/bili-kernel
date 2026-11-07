// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using RichasyKernel;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 直播弹幕认证被服务端拒绝；表示明确失败，不应进入重试循环.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only a classified authentication rejection may construct this exception.")]
public sealed class LiveDanmakuUnauthorizedException : KernelException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuUnauthorizedException"/> class.
    /// </summary>
    /// <param name="code">服务端回执代码.</param>
    /// <param name="serverMessage">服务端回执文本.</param>
    public LiveDanmakuUnauthorizedException(int code, string? serverMessage)
        : base($"直播弹幕认证失败：code={code}，message={serverMessage ?? "服务端未返回说明"}")
    {
        Code = code;
        ServerMessage = serverMessage;
    }

    /// <summary>
    /// 服务端回执代码.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 服务端回执文本.
    /// </summary>
    public string? ServerMessage { get; }
}

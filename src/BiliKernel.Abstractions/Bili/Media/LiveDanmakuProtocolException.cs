// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using RichasyKernel;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 直播弹幕协议错误：截断、越界、解压失败或超出解析预算.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only a classified protocol failure may construct this exception.")]
public sealed class LiveDanmakuProtocolException : KernelException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuProtocolException"/> class.
    /// </summary>
    /// <param name="reason">失败原因.</param>
    public LiveDanmakuProtocolException(string reason)
        : base($"直播弹幕协议错误：{reason}")
        => Reason = reason;

    /// <summary>
    /// 失败原因.
    /// </summary>
    public string Reason { get; }
}

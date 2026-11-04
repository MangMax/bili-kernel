// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using RichasyKernel;

namespace Richasy.BiliKernel.Bili.Search;

/// <summary>
/// 搜索响应要求完成服务端验证，不能作为正常搜索结果使用.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only a fixed safe message is allowed; accepting messages or inner exceptions could expose verification data.")]
public sealed class SearchVerificationRequiredException : KernelException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchVerificationRequiredException"/> class.
    /// </summary>
    public SearchVerificationRequiredException()
        : base("搜索需要完成验证，请稍后重试。")
    {
    }
}

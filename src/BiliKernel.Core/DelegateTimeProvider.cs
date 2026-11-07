// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel;

/// <summary>
/// 以委托提供当前 UTC 时间的 <see cref="TimeProvider"/>，用于兼容既有的 <see cref="Func{DateTimeOffset}"/> 重载。
/// </summary>
internal sealed class DelegateTimeProvider : TimeProvider
{
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTimeProvider"/> class.
    /// </summary>
    /// <param name="utcNow">返回当前 UTC 时间的委托。</param>
    public DelegateTimeProvider(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _utcNow();
}

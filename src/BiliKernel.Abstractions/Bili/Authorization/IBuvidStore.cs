// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Bili.Authorization;

/// <summary>
/// 设备标识存储。
/// </summary>
public interface IBuvidStore
{
    /// <summary>
    /// 获取已持久化的 BUVID3。
    /// </summary>
    string? Buvid3 { get; }

    /// <summary>
    /// 获取已持久化的 BUVID4。
    /// </summary>
    string? Buvid4 { get; }

    /// <summary>
    /// 持久化设备标识。
    /// </summary>
    /// <param name="buvid3">BUVID3。</param>
    /// <param name="buvid4">BUVID4。</param>
    void Save(string buvid3, string buvid4);
}
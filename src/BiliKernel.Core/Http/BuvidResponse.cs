// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Http;

/// <summary>
/// BUVID 响应。
/// </summary>
internal sealed class BuvidResponse
{
    /// <summary>
    /// BUVID3。
    /// </summary>
    [JsonPropertyName("b_3")]
    public string? B3 { get; set; }

    /// <summary>
    /// BUVID4。
    /// </summary>
    [JsonPropertyName("b_4")]
    public string? B4 { get; set; }
}
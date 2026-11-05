// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.User.Core;

internal sealed class BatchUserRelationsResponse
{
    [JsonPropertyName("code"), JsonRequired]
    public int Code { get; set; }

    [JsonPropertyName("data"), JsonRequired]
    public Dictionary<string, UserRelationResponse>? Data { get; set; }
}

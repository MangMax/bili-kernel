// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Richasy.BiliKernel.Services.User.Core;

internal static partial class EpisodeLink
{
    public static string GetEpisodeId(string url) => EpisodeIdRegex().Match(url).Groups[1].Value;

    [GeneratedRegex(@"ep(\d+)")]
    private static partial Regex EpisodeIdRegex();
}

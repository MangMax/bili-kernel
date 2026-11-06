// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Moment;
namespace Richasy.BiliKernel.Bili.Moment;
/// <summary>Cookie-authenticated web comprehensive dynamic feed, independent of the App feed.</summary>
public interface IWebMomentDiscoveryService
{
    /// <summary>Reads a page using an opaque offset. Web feeds do not expose an App baseline.</summary>
    Task<MomentView> GetComprehensiveMomentsAsync(string? offset = null, CancellationToken cancellationToken = default);
}

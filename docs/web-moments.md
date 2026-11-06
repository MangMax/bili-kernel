# Web comprehensive moments

Spec and acceptance context: [#46](https://github.com/MangMax/bili-kernel/issues/46).

Register `AddWebMomentDiscoveryService()` on the existing kernel builder and resolve
`IWebMomentDiscoveryService`. Configure the existing `BiliAuthenticator` with a Cookie
resolver. App token state may coexist; this service never refreshes it.

Call `GetComprehensiveMomentsAsync(cancellationToken: token)` for the first page.
Pass the returned `Offset` unchanged to the next call while `HasMoreMoments` is true.
`Users` and `UpdateBaseline` are null because this web response supplies neither.
The existing `IMomentDiscoveryService` continues to use App gRPC.

For video moments, cast `Data` to `VideoInformation` and read
`GetExtensionIfNotNull<string>(VideoExtensionDataId.Description)`. For forwards,
`Data` is the original `MomentInformation`; read its video in the same way.
The outer `Description` is dynamic text, including supplied emotes, not the video
summary. Empty video descriptions and `"-"` are preserved verbatim.

Opus maps to `ArticleInformation`: title and summary remain separate. Its supplied
pictures and rich summary are available via `ArticleExtensionDataId.Pictures` and
`SummaryEmoteText`. Plain text, images, articles, PGC episodes and forwards retain
their respective content shapes. Unknown types return `Unsupported`, rather than
dropping a page or pretending to be a video. Unsupported originals lacking required
identity/author data are rejected as malformed responses.

Use each moment's `CommentType`/`CommentId` for comments; these prefer explicit
`basic.comment_type`/`comment_id_str` fields. Dynamic IDs, archive aid/bvid and original
dynamic IDs are distinct. Video `WebArchiveType` and `WebBadgeText` extensions preserve
explicit response signals; collaboration/charging booleans are populated only when
the matching structured fields exist. Titles never create flags.

Tests: `src/Tests/BiliKernel.Tests/WebMomentTests.cs` exercises the public reader
with fixed synthetic HTTP responses and both Cookie and App token resolvers.
No live credentials or authenticated network validation are required.

Parsing uses `JsonDocument.ParseAsync`, which needs no reflection or generated DTO
metadata. The Moment project enables AOT compatibility analysis for net8.0; build it
with `EnableAotAnalyzer=true` and `EnableTrimAnalyzer=true`. Native executable publish
is a separate consumer-level check and has not been performed for this library.

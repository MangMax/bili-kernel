// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.Moment;

/// <summary>Reads the web comprehensive dynamic feed without App authentication.</summary>
public sealed class WebMomentDiscoveryService : IWebMomentDiscoveryService
{
    private readonly BiliHttpClient _http;
    private readonly BiliAuthenticator _auth;

    /// <summary>Creates a web reader. No App token refresh service is used.</summary>
    public WebMomentDiscoveryService(BiliHttpClient httpClient, BiliAuthenticator authenticator)
    {
        _http = httpClient;
        _auth = authenticator;
    }

    /// <inheritdoc/>
    public async Task<MomentView> GetComprehensiveMomentsAsync(string? offset = null, CancellationToken cancellationToken = default)
    {
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri("https://api.bilibili.com/x/polymer/web-dynamic/v1/feed/all"));
        var parameters = new Dictionary<string, string> { ["type"] = "all" };
        if (offset != null) parameters["offset"] = offset;
        _auth.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings
        {
            ApiType = BiliApiType.Web, RequireCookie = true, ForceNoToken = true,
        });
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        // JsonDocument is reflection-free on both target frameworks; no runtime serializer metadata is needed.
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (Number(document.RootElement, "code") != 0) throw new KernelException("Invalid web dynamic response code.");
        var data = Child(document.RootElement, "data");
        var items = Child(data, "items");
        var more = Child(data, "has_more");
        if (items.ValueKind != JsonValueKind.Array || more.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new KernelException("Invalid web dynamic page: items/has_more missing.");
        var next = Text(data, "offset");
        if (more.GetBoolean() && string.IsNullOrEmpty(next)) throw new KernelException("Missing web dynamic cursor.");
        var moments = new List<MomentInformation>();
        foreach (var item in items.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            moments.Add(Map(item));
        }
        return new MomentView(moments.AsReadOnly(), null, next, null, more.GetBoolean());
    }

    private static MomentInformation Map(JsonElement item)
    {
        var id = Required(item, "id_str");
        var modules = Child(item, "modules");
        var author = Child(modules, "module_author");
        var user = new UserProfile(Required(author, "mid"), Text(author, "name"), Image(Text(author, "face")));
        var dynamic = Child(modules, "module_dynamic");
        var major = Child(dynamic, "major");
        var stat = Child(modules, "module_stat");
        var type = MomentItemType.Unsupported;
        object? content = null;
        var commentType = CommentTargetType.None;
        var commentId = id;
        var description = RichText(Child(dynamic, "desc"));
        switch (Text(item, "type"))
        {
            case "DYNAMIC_TYPE_FORWARD":
                type = MomentItemType.Forward;
                commentType = CommentTargetType.Moment;
                var original = Child(item, "orig");
                if (original.ValueKind != JsonValueKind.Object) throw new KernelException("Missing web dynamic original.");
                if (Required(original, "type") == "DYNAMIC_TYPE_NONE")
                {
                    // Deleted originals have no author or identifiers to map.
                    type = MomentItemType.Unsupported;
                }
                else content = Map(original);
                break;
            case "DYNAMIC_TYPE_PGC":
                var pgc = Child(major, "pgc");
                type = MomentItemType.Pgc;
                commentType = CommentTargetType.Video;
                commentId = Required(pgc, "aid");
                var episode = new EpisodeInformation(new MediaIdentifier(Required(pgc, "epid"), Text(pgc, "title"), Image(Text(pgc, "cover"))),
                    Duration(Text(pgc, "duration")));
                episode.AddExtensionIfNotNull(EpisodeExtensionDataId.Aid, Number(pgc, "aid"));
                episode.AddExtensionIfNotNull(EpisodeExtensionDataId.SeasonId, Number(pgc, "season_id"));
                content = episode;
                break;
            case "DYNAMIC_TYPE_AV":
                var archive = Child(major, "archive");
                var aid = Required(archive, "aid");
                type = MomentItemType.Video;
                commentType = CommentTargetType.Video;
                commentId = aid;
                var video = new VideoInformation(new MediaIdentifier(aid, Text(archive, "title"), Image(Text(archive, "cover"))),
                    new PublisherProfile(user), Duration(Text(archive, "duration_text")), Text(archive, "bvid"),
                    Number(author, "pub_ts") is long ts ? DateTimeOffset.FromUnixTimeSeconds(ts) : null,
                    communityInformation: new VideoCommunityInformation(aid, Count(Child(archive, "stat"), "play"), Count(Child(archive, "stat"), "danmaku")));
                video.AddExtensionIfNotNull(VideoExtensionDataId.Description, Text(archive, "desc"));
                video.AddExtensionIfNotNull(VideoExtensionDataId.MediaType, MediaType.Video);
                video.AddExtensionIfNotNull(VideoExtensionDataId.WebArchiveType, Number(archive, "type"));
                video.AddExtensionIfNotNull(VideoExtensionDataId.WebBadgeText, Text(Child(archive, "badge"), "text"));
                video.AddExtensionIfNotNull(VideoExtensionDataId.IsCollaborativeSubmission, Number(Child(archive, "rights"), "is_cooperation") is long coop ? coop == 1 : (bool?)null);
                var charging = Child(archive, "is_upower_exclusive");
                video.AddExtensionIfNotNull(VideoExtensionDataId.IsChargingExclusive, charging.ValueKind is JsonValueKind.True or JsonValueKind.False ? charging.GetBoolean() : (bool?)null);
                content = video;
                break;
            case "DYNAMIC_TYPE_DRAW":
            case "DYNAMIC_TYPE_WORD":
            case "DYNAMIC_TYPE_ARTICLE":
                var opus = Child(major, "opus");
                var draw = Child(major, "draw");
                var article = Child(major, "article");
                if (opus.ValueKind == JsonValueKind.Object || article.ValueKind == JsonValueKind.Object)
                {
                    type = MomentItemType.Article;
                    var node = opus.ValueKind == JsonValueKind.Object ? opus : article;
                    var articleId = Text(node, "id") ?? Text(node, "opus_id") ?? id;
                    var pictures = opus.ValueKind == JsonValueKind.Object
                        ? Pictures(Child(opus, "pics"), "url")
                        : Pictures(Child(article, "covers"), null);
                    commentType = CommentTargetType.Moment;
                    var articleInfo = new ArticleInformation(new ArticleIdentifier(articleId, Text(node, "title"),
                        Text(Child(node, "summary"), "text") ?? Text(node, "desc"), pictures.FirstOrDefault()), user,
                        Number(author, "pub_ts") is long published ? DateTimeOffset.FromUnixTimeSeconds(published) : null);
                    articleInfo.AddExtensionIfNotNull(ArticleExtensionDataId.Pictures, pictures);
                    articleInfo.AddExtensionIfNotNull(ArticleExtensionDataId.SummaryEmoteText, RichText(Child(node, "summary")));
                    content = articleInfo;
                    if (article.ValueKind == JsonValueKind.Object) { commentType = CommentTargetType.Article; commentId = articleId; }
                }
                else if (draw.ValueKind == JsonValueKind.Object)
                {
                    type = MomentItemType.Image;
                    content = Pictures(Child(draw, "items"), "src");
                    commentType = CommentTargetType.Album;
                    commentId = Required(draw, "id");
                }
                else if (Text(item, "type") == "DYNAMIC_TYPE_WORD")
                {
                    type = MomentItemType.PlainText;
                    commentType = CommentTargetType.Moment;
                }
                else throw new KernelException("Missing web dynamic draw/article payload.");
                break;
        }
        var basic = Child(item, "basic");
        if (Text(basic, "comment_id_str") is string target && Number(basic, "comment_type") is long targetType)
        {
            commentId = target;
            commentType = targetType switch
            {
                1 => CommentTargetType.Video, 11 => CommentTargetType.Album, 12 => CommentTargetType.Article,
                14 => CommentTargetType.Music, 17 => CommentTargetType.Moment, 33 => CommentTargetType.Course,
                _ => CommentTargetType.None,
            };
        }
        return new MomentInformation(id, user, Text(author, "pub_time"),
            new MomentCommunityInformation(id, Number(Child(stat, "like"), "count"), Number(Child(stat, "comment"), "count"),
                Child(Child(stat, "like"), "status").ValueKind == JsonValueKind.True),
            commentType, commentId, type, content, description);
    }

    private static EmoteText? RichText(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object) return null;
        var emotes = new Dictionary<string, BiliImage>();
        var nodes = Child(node, "rich_text_nodes");
        if (nodes.ValueKind == JsonValueKind.Array)
            foreach (var n in nodes.EnumerateArray())
                if (Text(n, "type") == "RICH_TEXT_NODE_TYPE_EMOJI" && Image(Text(Child(n, "emoji"), "icon_url")) is BiliImage image)
                    emotes[Required(n, "text")] = image;
        return new EmoteText(Text(node, "text") ?? string.Empty, emotes.Count == 0 ? null : emotes);
    }

    private static List<BiliImage> Pictures(JsonElement array, string? key)
    {
        var result = new List<BiliImage>();
        if (array.ValueKind == JsonValueKind.Array)
            foreach (var node in array.EnumerateArray())
            {
                var url = key != null ? Text(node, key) : node.ValueKind == JsonValueKind.String ? node.GetString() : null;
                if (Image(url) is BiliImage image) result.Add(image);
            }
        return result;
    }

    private static JsonElement Child(JsonElement node, string key) => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) ? value : default;
    private static string? Text(JsonElement node, string key) => Child(node, key).ValueKind is JsonValueKind.String or JsonValueKind.Number ? Child(node, key).ToString() : null;
    private static string Required(JsonElement node, string key) => !string.IsNullOrEmpty(Text(node, key)) ? Text(node, key)! : throw new KernelException("Missing web dynamic field: " + key);
    private static long? Number(JsonElement node, string key) => long.TryParse(Text(node, key), out var value) ? value : null;
    private static BiliImage? Image(string? url) => string.IsNullOrEmpty(url) ? null : url.ToImage();
    private static double? Count(JsonElement node, string key)
    {
        var text = Text(node, key);
        if (text == null) return null;
        var multiplier = text.Length > 0 && text[text.Length - 1] == '万' ? 10000d : text.Length > 0 && text[text.Length - 1] == '亿' ? 100000000d : 1d;
        return double.TryParse(text.Replace("万", "").Replace("亿", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var count) ? count * multiplier : null;
    }
    private static long? Duration(string? text)
    {
        if (text == null) return null;
        long seconds = 0;
        foreach (var part in text.Split(':')) { if (!long.TryParse(part, out var value)) return null; seconds = (seconds * 60) + value; }
        return seconds;
    }
}

// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Live;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播消息到强类型事件的解析测试.
/// </summary>
public sealed class LiveMessageParserTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CommandSuffixIsToleratedForDanmaku()
    {
        const string Json = """{"cmd":"DANMU_MSG:4:0:2:2:2:0","info":[[0,1,25,16711680],"hello",[12345,"Alice",1,0,0,0,0,""],[5,"Medal","",0,""],[12,0,"#00AAFF"],["Title"]]}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        var danmaku = Assert.IsType<LiveDanmakuReceivedEvent>(liveEvent).Danmaku;
        Assert.Equal("hello", danmaku.Text);
        Assert.Equal("Alice", danmaku.UserName);
        Assert.Equal(12345, danmaku.UserId);
        Assert.True(danmaku.IsAdmin);
        Assert.Equal("#FF0000", danmaku.TextColor);
        Assert.Equal("UL12", danmaku.UserLevel);
        Assert.Equal("#00AAFF", danmaku.UserLevelColor);
        Assert.Equal("Medal", danmaku.MedalName);
        Assert.Equal("5", danmaku.MedalLevel);
        Assert.Equal("Title", danmaku.UserTitle);
        Assert.Equal(0, danmaku.GuardLevel);
    }

    [Fact]
    public void GuardLevelIsParsedFromSeventhInfoEntry()
    {
        const string Json = """{"cmd":"DANMU_MSG","info":[[0,1,25,16777215],"hello",[7,"Alice",0,0,0],[],[],["Title"],[],3]}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        var danmaku = Assert.IsType<LiveDanmakuReceivedEvent>(liveEvent).Danmaku;
        Assert.Equal(3, danmaku.GuardLevel);
    }

    [Theory]
    [InlineData("""{"cmd":"DANMU_MSG","info":[[0,1,25,16777215],"hello",[7,"Alice",0,0,0],[],[],["Title"]]}""")]
    [InlineData("""{"cmd":"DANMU_MSG","info":[[0,1,25,16777215],"hello",[7,"Alice",0,0,0],[],[],["Title"],[],"3"]}""")]
    [InlineData("""{"cmd":"DANMU_MSG","info":[[0,1,25,16777215],"hello",[7,"Alice",0,0,0],[],[],["Title"],[],[3]]}""")]
    public void GuardLevelIsZeroWhenMissingOrNotNumber(string json)
    {
        var kind = Parse(json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.Equal(0, Assert.IsType<LiveDanmakuReceivedEvent>(liveEvent).Danmaku.GuardLevel);
    }

    [Fact]
    public void SuperChatIsParsed()
    {
        const string Json = """{"cmd":"SUPER_CHAT_MESSAGE","data":{"message_id":"42","message":"great","price":30,"start_time":1700000000,"end_time":1700000060,"background_color":"#FF0000","user_info":{"uname":"Bob","uid":99}}}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        var superChat = Assert.IsType<LiveSuperChatEvent>(liveEvent).SuperChat;
        Assert.Equal("42", superChat.Id);
        Assert.Equal("great", superChat.Message);
        Assert.Equal(30, superChat.Price);
        Assert.Equal("Bob", superChat.UserName);
        Assert.Equal(99, superChat.UserId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), superChat.StartTime);
        Assert.Equal("#FF0000", superChat.BackgroundColor);
    }

    [Fact]
    public void SuperChatJapaneseDuplicateIsIgnored()
    {
        const string Json = """{"cmd":"SUPER_CHAT_MESSAGE_JPN","data":{"message_id":"42","message":"great"}}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Ignored, kind);
        Assert.Null(liveEvent);
    }

    [Fact]
    public void SuperChatDeleteIsParsed()
    {
        const string Json = """{"cmd":"SUPER_CHAT_MESSAGE_DELETE","data":{"message_ids":["1","2"]}}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.Equal(["1", "2"], Assert.IsType<LiveSuperChatDeletedEvent>(liveEvent).MessageIds);
    }

    [Fact]
    public void WatchedChangeProducesOnlineCount()
    {
        const string Json = """{"cmd":"WATCHED_CHANGE","data":{"num":321,"text_large":"321人看过"}}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        var online = Assert.IsType<LiveOnlineCountChangedEvent>(liveEvent);
        Assert.Equal(321, online.Count);
        Assert.Equal(LiveOnlineCountSource.Watched, online.Source);
        Assert.Equal("321人看过", online.Text);
    }

    [Fact]
    public void HeartbeatReplyProducesPopularity()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, 7890);

        Assert.True(LiveMessageParser.TryParseHeartbeatReply(payload, out var count));
        Assert.Equal(7890, count);
    }

    [Fact]
    public void HeartbeatReplyWithoutPopularityIsAckOnly()
    {
        Assert.True(LiveMessageParser.TryParseHeartbeatReply(Utf8("""{"code":0}"""), out var count));
        Assert.Null(count);
    }

    [Fact]
    public void RoomChangeChangesTitle()
    {
        const string Json = """{"cmd":"ROOM_CHANGE","data":{"title":"New title","area_name":"Games"}}""";

        var kind = Parse(Json, out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        var change = Assert.IsType<LiveRoomTitleChangedEvent>(liveEvent);
        Assert.Equal("New title", change.Title);
        Assert.Equal("Games", change.AreaName);
    }

    [Fact]
    public void PreparingIsNotStarted()
    {
        var kind = Parse("""{"cmd":"PREPARING"}""", out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.IsType<LiveRoomNotStartedEvent>(liveEvent);
    }

    [Fact]
    public void CutOffIsCutOff()
    {
        var kind = Parse("""{"cmd":"CUT_OFF","data":{"msg":"supervisor cut"}}""", out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.Equal("supervisor cut", Assert.IsType<LiveRoomCutOffEvent>(liveEvent).Message);
    }

    [Fact]
    public void PlayUrlReloadIsReload()
    {
        var kind = Parse("""{"cmd":"PLAYURL_RELOAD_MASTER"}""", out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.IsType<LivePlayUrlReloadEvent>(liveEvent);
    }

    [Fact]
    public void LiveIsStarted()
    {
        var kind = Parse("""{"cmd":"LIVE"}""", out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Event, kind);
        Assert.IsType<LiveStartedEvent>(liveEvent);
    }

    [Fact]
    public void UnknownCommandIsCountedWithoutThrowing()
    {
        var kind = Parse("""{"cmd":"SOME_NEW_COMMAND","data":{}}""", out var liveEvent);

        Assert.Equal(LiveMessageParseKind.Unknown, kind);
        Assert.Null(liveEvent);
    }

    [Fact]
    public void MalformedJsonDoesNotPreventFollowingPacket()
    {
        var buffer = Concat(
            Frame(Utf8("""{"cmd":"DANMU_MSG","info":""")),
            Frame(Utf8("""{"cmd":"PREPARING"}""")));
        var events = new List<LiveDanmakuEvent>();
        var kinds = new List<LiveMessageParseKind>();

        var result = LivePacketCodec.Decode(buffer, (_, _, _, payload) =>
        {
            var kind = LiveMessageParser.Parse(payload, ReceivedAt, out var liveEvent);
            kinds.Add(kind);
            if (liveEvent is not null)
            {
                events.Add(liveEvent);
            }
        });

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Equal([LiveMessageParseKind.Malformed, LiveMessageParseKind.Event], kinds);
        Assert.IsType<LiveRoomNotStartedEvent>(Assert.Single(events));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-101, false)]
    public void AuthReplyCodeIsClassified(int code, bool success)
    {
        var json = $"{{\"code\":{code},\"message\":\"auth\"}}";

        var parsed = LiveMessageParser.TryParseAuthReply(Utf8(json), out var actualCode, out var message);

        Assert.True(parsed);
        Assert.Equal(code, actualCode);
        Assert.Equal("auth", message);
        Assert.Equal(success, actualCode == 0);
    }

    private static LiveMessageParseKind Parse(string json, out LiveDanmakuEvent? liveEvent)
        => LiveMessageParser.Parse(Utf8(json), ReceivedAt, out liveEvent);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Frame(byte[] body)
    {
        var bytes = new byte[16 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 5);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(12, 4), 1);
        body.CopyTo(bytes.AsSpan(16));
        return bytes;
    }

    private static byte[] Concat(params byte[][] items)
    {
        var bytes = new byte[items.Sum(item => item.Length)];
        var offset = 0;
        foreach (var item in items)
        {
            item.CopyTo(bytes, offset);
            offset += item.Length;
        }

        return bytes;
    }
}

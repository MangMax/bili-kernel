// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using System.Net;
using System.Text;
using System.Text.Json;
using RichasyKernel;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User;

namespace Richasy.BiliKernel.Tests;

public sealed class BatchRelationshipTests
{
    [Fact]
    public async Task BatchResponsePreservesFollowingUnknownAndMissing()
    {
        using var handler = new Handler(_ => """{"code":0,"data":{"1":{"mid":1,"attribute":1},"2":{"mid":2,"attribute":2},"3":{"mid":3,"attribute":6},"4":{"mid":4,"attribute":128},"5":{"mid":5,"attribute":99},"6":{"mid":66,"attribute":2},"7":{"mid":7},"9":{"mid":9,"attribute":2,"special":1},"10":{"mid":10,"attribute":1,"special":1}}}""");
        using var client = new BiliHttpClient(handler);
        var service = new RelationshipService(client, null!, null!, new BiliAuthenticator(null, null));
        var result = await service.GetRelationshipsAsync(Enumerable.Range(1, 10).Select(id => id.ToString()));
        Assert.Equal(UserRelationStatus.Following, result["1"]);
        Assert.Equal(UserRelationStatus.Following, result["2"]);
        Assert.Equal(UserRelationStatus.Friends, result["3"]);
        Assert.Equal(UserRelationStatus.Blocked, result["4"]);
        Assert.Equal(UserRelationStatus.Unknown, result["5"]);
        Assert.Equal(UserRelationStatus.Unknown, result["6"]);
        Assert.Equal(UserRelationStatus.Unknown, result["7"]);
        Assert.Equal(UserRelationStatus.Unfollow, result["8"]);
        Assert.Equal(UserRelationStatus.SpeciallyFollowed, result["9"]);
        Assert.Equal(UserRelationStatus.SpeciallyFollowed, result["10"]);
        Assert.Single(handler.Queries);
        Assert.Contains("fids=1,2,3,4,5,6,7,8,9,10", handler.Queries[0]);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task SuccessfulEmptyBatchMeansUnfollowed(string data)
    {
        using var handler = new Handler(_ => "{\"code\":0,\"data\":" + data + "}");
        using var client = new BiliHttpClient(handler);
        var service = new RelationshipService(client, null!, null!, new BiliAuthenticator(null, null));
        var result = await service.GetRelationshipsAsync(["42"]);
        Assert.Equal(UserRelationStatus.Unfollow, result["42"]);
    }

    [Theory]
    [InlineData("{\"code\":0}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"code\":0,\"data\":[]}")]
    [InlineData("{\"code\":-352,\"message\":\"rejected\",\"data\":{}}")]
    public async Task InvalidOrRejectedBatchNeverBecomesUnfollowed(string json)
    {
        using var handler = new Handler(_ => json);
        using var client = new BiliHttpClient(handler);
        var service = new RelationshipService(client, null!, null!, new BiliAuthenticator(null, null));
        var error = await Record.ExceptionAsync(() => service.GetRelationshipsAsync(["42"]));
        if (json.Contains("-352", StringComparison.Ordinal)) Assert.IsType<KernelException>(error);
        else Assert.IsType<JsonException>(error);
    }

    [Fact]
    public async Task BatchRequestDeduplicatesChunksAndValidatesBeforeSending()
    {
        using var handler = new Handler(_ => "{\"code\":0,\"data\":{}}");
        using var client = new BiliHttpClient(handler);
        var service = new RelationshipService(client, null!, null!, new BiliAuthenticator(null, null));
        Assert.Empty(await service.GetRelationshipsAsync([]));
        Assert.Empty(handler.Queries);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetRelationshipsAsync(["42", "-1"]));
        Assert.Empty(handler.Queries);
        var ids = Enumerable.Range(1, 41).Select(id => id.ToString()).Append("01");
        Assert.Equal(41, (await service.GetRelationshipsAsync(ids)).Count);
        Assert.Equal(2, handler.Queries.Count);
        Assert.All(handler.Queries, query => Assert.DoesNotContain("%2C", query, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class Handler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        public List<string> Queries { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/x/relation/relations", request.RequestUri!.AbsolutePath);
            Queries.Add(request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json"),
            });
        }
    }
}

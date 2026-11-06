// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using System.Net;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User;

namespace Richasy.BiliKernel.Tests;

public sealed class UserCardRelationshipTests
{
    [Theory]
    [InlineData(",\"following\":true", UserRelationStatus.Following)]
    [InlineData(",\"following\":false", UserRelationStatus.Unfollow)]
    [InlineData("", UserRelationStatus.Unknown)]
    public async Task UserCardPreservesFollowingFalseAndUnknown(string following, UserRelationStatus expected)
    {
        using var handler = new UserCardHandler("{\"code\":0,\"data\":{\"card\":{\"mid\":\"42\"}" + following + "}}");
        using var client = new BiliHttpClient(handler);
        var service = new UserService(client, null!, null!, new BiliAuthenticator(null, null));

        var card = await service.GetUserInformationAsync("42");

        Assert.Equal("42", card.Profile!.User.Id);
        Assert.Equal(expected, card.Community!.Relation);
    }

    private sealed class UserCardHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/x/web-interface/card", request.RequestUri!.AbsolutePath);
            Assert.Contains("mid=42", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}

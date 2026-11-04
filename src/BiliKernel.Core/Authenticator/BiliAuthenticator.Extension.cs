// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Authenticator;

/// <summary>
/// 基础授权器.
/// </summary>
public sealed partial class BiliAuthenticator
{
    private const string AppKey = "27eb53fc9058f8c3";
    private const string AppSecret = "c2ed53a74eeefe3cf99fbd01d8c9c375";
    private const string WebKey = "aa1e74ee4874176e";
    private const string WebSecret = "54e6a9a31b911cd5fc0daa66ebf94bc4";
    private const string BuildNumber = BiliClientIdentity.BuildNumber;

    private readonly IBiliCookiesResolver? _cookieResolver;
    private readonly IBiliTokenResolver? _tokenResolver;
    private readonly BuvidManager? _buvidManager;
    private readonly BiliHttpClient? _httpClient;

#pragma warning disable IDE1006 // 命名样式
    private static readonly byte[] MIXIN_KEY_ENC_TAB =
#pragma warning restore IDE1006 // 命名样式
        [
            46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42,
            19, 29, 28, 14, 39, 12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60,
            51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11, 36, 20, 34, 44, 52,
        ];

    private volatile Tuple<string, DateTimeOffset>? _wbiCache;

    private static string? _buvid;

    private static string GetApiKey(BiliApiType deviceType)
    {
        return deviceType switch
        {
            BiliApiType.Web => WebKey,
            _ => AppKey,
        };
    }

    private static string GetApiSecret(BiliApiType deviceType)
    {
        return deviceType switch
        {
            BiliApiType.Web => WebSecret,
            _ => AppSecret,
        };
    }

    private static void InitializeDeviceParameters(Dictionary<string, string> parameters, BiliApiType apiType, bool onlyUseAppKey = false)
    {
        if (apiType != BiliApiType.None && !parameters.ContainsKey("appkey"))
        {
            var appKey = GetApiKey(apiType);
            parameters.Add("appkey", appKey);
        }

        if (onlyUseAppKey)
        {
            return;
        }

        switch (apiType)
        {
            case BiliApiType.App:
                {
                    parameters.Add("mobi_app", "iphone");
                    parameters.Add("platform", "ios");
                    parameters.Add("ts", NowWithSeconds().ToString());
                }
                break;
            case BiliApiType.Web:
                {
                    parameters.Add("platform", "web");
                    parameters.Add("ts", NowWithMilliSeconds().ToString());
                }
                break;
            default:
                break;
        }
    }

    private static string GenerateSign(Dictionary<string, string> parameters, BiliApiType device)
    {
        var secret = GetApiSecret(device);
        // App 端签名使用未转义的键值串；POST 由 FormUrlEncodedContent 负责编码.
        var query = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}").OrderBy(p => p, StringComparer.Ordinal));
        return (query + secret).Md5ComputeHash();
    }

    private static string GenerateQuery(Dictionary<string, string> parameters)
    {
        var queryParameters = parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}").ToList();
        queryParameters.Sort(StringComparer.Ordinal);
        return string.Join("&", queryParameters);
    }

    /// <summary>
    /// 剔除 WBI 签名不允许的字符，服务端校验时会做同样的过滤.
    /// </summary>
    /// <param name="parameters">请求参数.</param>
    private static void FilterWbiIllegalCharacters(Dictionary<string, string> parameters)
    {
        foreach (var key in parameters.Keys.ToList())
        {
            var value = parameters[key];
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            parameters[key] = value
                .Replace("!", string.Empty)
                .Replace("'", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("*", string.Empty);
        }
    }

    private static long NowWithSeconds() => DateTimeOffset.Now.ToLocalTime().ToUnixTimeSeconds();

    private static long NowWithMilliSeconds() => DateTimeOffset.Now.ToLocalTime().ToUnixTimeMilliseconds();

    private static string GetBuvid()
    {
        if (string.IsNullOrEmpty(_buvid))
        {
            var macAddress = NetworkInterface.GetAllNetworkInterfaces()
                 .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                 .Select(nic => nic.GetPhysicalAddress().ToString())
                 .FirstOrDefault();
            var buvidObj = new Buvid(macAddress);
            _buvid = buvidObj.Generate();
        }

        return _buvid;
    }

    private string GenerateRID(Dictionary<string, string> parameters)
    {
        var cache = _wbiCache;
        if (cache == null || _utcNow() >= cache.Item2)
        {
            throw new InvalidOperationException("WBI key is unavailable or expired. Call InitializeWbiAsync first.");
        }

        if (!parameters.ContainsKey("wts"))
        {
            parameters.Add("wts", NowWithSeconds().ToString());
        }

        var query = GenerateQuery(parameters);
        return (query + cache.Item1).Md5ComputeHash();
    }

    /// <summary>
    /// 网页导航响应.
    /// </summary>
    /// <remarks>
    /// https://github.com/SocialSisterYi/bilibili-API-collect/blob/master/docs/misc/sign/wbi.md.
    /// </remarks>
    internal sealed class WebNavResponse
    {
        /// <summary>
        /// 未知.
        /// </summary>
        [JsonPropertyName("wbi_img")]
        public WbiImage? Img { get; set; }

        /// <summary>
        /// Wbi image.
        /// </summary>
        internal sealed class WbiImage
        {
            /// <summary>
            /// 未知.
            /// </summary>
            [JsonPropertyName("img_url")]
            public string? ImgUrl { get; set; }

            /// <summary>
            /// 未知.
            /// </summary>
            [JsonPropertyName("sub_url")]
            public string? SubUrl { get; set; }
        }
    }
}

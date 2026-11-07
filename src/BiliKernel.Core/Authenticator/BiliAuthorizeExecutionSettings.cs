// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models;

namespace Richasy.BiliKernel.Authenticator;

/// <summary>
/// 哔哩授权执行设置.
/// </summary>
public class BiliAuthorizeExecutionSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BiliAuthorizeExecutionSettings"/> class.
    /// </summary>
    public BiliAuthorizeExecutionSettings(
        BiliApiType apiType = BiliApiType.App,
        bool useToken = false,
        bool forceNoToken = false,
        bool useCookie = false,
        bool onlyUseAppKey = false,
        bool needRID = false,
        bool needCSRF = false,
        string? additionalQuery = default)
    {
        ApiType = apiType;
        RequireToken = useToken;
        ForceNoToken = forceNoToken;
        RequireCookie = useCookie;
        OnlyUseAppKey = onlyUseAppKey;
        NeedRID = needRID;
        NeedCSRF = needCSRF;
        AdditionalQuery = additionalQuery;
    }

    /// <summary>
    /// 创建仅携带可用 Cookie 的凭据策略；不读取 App token，也不要求本地 Cookie 存在。
    /// ApiType、WBI、CSRF 和来源页仍由端点独立指定。
    /// </summary>
    public static BiliAuthorizeExecutionSettings CookieOnly(
        BiliApiType apiType = BiliApiType.Web,
        bool needRID = false,
        bool needCSRF = false)
        => new(apiType: apiType, forceNoToken: true, useCookie: true, needRID: needRID, needCSRF: needCSRF);

    /// <summary>
    /// 创建同时使用 Cookie 与 App 令牌的凭据策略：两者按可用性附加，缺失时不阻止匿名或试看请求。
    /// 仅用于明确声明需要账号权益的端点；其他网页端点继续使用 <see cref="CookieOnly"/>。
    /// </summary>
    public static BiliAuthorizeExecutionSettings CookieAndToken(
        BiliApiType apiType = BiliApiType.Web,
        bool needRID = false,
        bool needCSRF = false)
        => new(apiType: apiType, useCookie: true, needRID: needRID, needCSRF: needCSRF);

    /// <summary>
    /// 设备类型.
    /// </summary>
    public BiliApiType ApiType { get; set; }

    /// <summary>
    /// 是否需要Token.
    /// </summary>
    public bool RequireToken { get; set; }

    /// <summary>
    /// 是否强制不使用Token.
    /// </summary>
    public bool ForceNoToken { get; set; }

    /// <summary>
    /// 是否跳过自动附加 App build 参数，默认保留既有行为.
    /// </summary>
    public bool SkipBuild { get; set; }

    /// <summary>
    /// 是否需要Cookie.
    /// </summary>
    public bool RequireCookie { get; set; }

    /// <summary>
    /// 是否仅使用AppKey.
    /// </summary>
    public bool OnlyUseAppKey { get; set; }

    /// <summary>
    /// 是否需要RID.
    /// </summary>
    public bool NeedRID { get; set; }

    /// <summary>
    /// 是否需要CSRF.
    /// </summary>
    public bool NeedCSRF { get; set; }

    /// <summary>
    /// 附加查询参数.
    /// </summary>
    public string? AdditionalQuery { get; set; }
}

// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel;

/// <summary>
/// 客户端身份。HTTP 与 gRPC 共用同一套数值，避免版本号各自漂移。
/// </summary>
public static class BiliClientIdentity
{
    /// <summary>
    /// 应用版本号。
    /// </summary>
    public const string AppVersion = "8.43.0";

    /// <summary>
    /// 构建标识（字符串形态，用于查询参数与 User-Agent）。
    /// </summary>
    public const string BuildNumber = "8430300";

    /// <summary>
    /// 构建标识（整数形态，用于 gRPC 元数据）。
    /// </summary>
    public const int Build = 8430300;

    /// <summary>
    /// 设备型号。
    /// </summary>
    public const string DeviceModel = "iPhone 14 Pro";

    /// <summary>
    /// 系统版本。
    /// </summary>
    public const string OSVersion = "16.6";

    /// <summary>
    /// Web 端 User-Agent。
    /// </summary>
    public const string WebUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36 Edg/116.0.1938.69";

    /// <summary>
    /// gRPC 端 User-Agent。
    /// </summary>
    public static string GrpcUserAgent => $"bili-universal/{BuildNumber} "
        + $"os/ios model/{DeviceModel} mobi_app/iphone_i "
        + $"osVer/{OSVersion} "
        + $"network/{GrpcConfig.NetworkType} "
        + $"grpc-objc/1.47.0 grpc-c/25.0.0 (ios; cronet_http)";
}
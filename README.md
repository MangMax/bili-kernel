# 哔哩内核（独立维护版本）

本项目是 [Richasy/bili-kernel](https://github.com/Richasy/bili-kernel) 的独立维护版本。

- 与原项目相互独立：不向原项目提交改动，也不代表原项目
- 功能取舍与版本节奏由本仓库自行维护
- 原项目的设计与实现版权归原作者所有

## 概述

哔哩内核（Bili Kernel）是从 [哔哩助理](https://github.com/Richasy/Bili.Copilot) 中抽离出来的核心模块，是一层哔哩哔哩 API 的 .NET 包装器：

- 将常用 API 封装成 .NET 异步请求
- 将常见的数据结构抽象成统一模型
- 处理 HTTP 请求，并进行基础的错误处理
- 组件化按需取用，提供接口便于自定义实现

## 引入方式

尚未发布 NuGet 包，请以 git submodule 方式引入：

```shell
git submodule add https://github.com/MangMax/bili-kernel.git externals/bili-kernel
```

随后在项目中以 `ProjectReference` 引用所需的子模块，例如：

```xml
<ProjectReference Include="..\externals\bili-kernel\src\Services\Services.Media\Services.Media.csproj" />
```

代码调用示例可参考 [BiliService](https://github.com/Richasy/bili-kernel/blob/main/src/Samples/Bili.Console/BiliService.cs)。

## 许可

[GPL-3.0](LICENSE)。

## 显式 REST 凭据策略

网页端点可使用 `BiliAuthorizeExecutionSettings.CookieOnly(needRID: true)`：

```csharp
await authenticator.InitializeWbiAsync(cancellationToken);
authenticator.AuthorizeRestRequest(request, parameters,
    BiliAuthorizeExecutionSettings.CookieOnly(needRID: true));
```

该入口只携带已保存的 Cookie，不读取/附带 App `access_key`，也不触发 App token 刷新。
Cookie 不存在时允许请求发出（适用于支持匿名的公共读取）；过期 Cookie 或服务端拒绝仍通过正常 HTTP/API 错误传播，不转换为空数据。
它不验证 Cookie 有效性，也不负责调用 `EnsureTokenAsync`。调用方不得在 Cookie-only 路径上等待无关 token 刷新。
`apiType` 仅决定客户端参数/签名类型，默认 Web；`needRID`、`needCSRF` 分别选择 WBI/CSRF，WBI 须先初始化。
来源页 Referer/Origin、反爬参数、SkipBuild 等仍由各端点独立声明；不要把所有 Web 端点改成相同参数。
用户空间资料、投稿分页和专栏分页共用此入口，保留原来源页与指纹参数。
番剧播放地址是唯一显式例外，使用 `BiliAuthorizeExecutionSettings.CookieAndToken(needRID: true)`：按可用性同时附加 Cookie 与 App `access_key`，两者都缺失时仍按匿名请求发出。理由、证据与复核步骤见 [番剧播放凭据](docs/pgc-playback-credentials.md)；新增端点默认仍用 `CookieOnly`。

旧构造器和属性保持原语义：`RequireToken=false` 仍会附带已有 token；`ForceNoToken=true` 才排除自动添加。
需要 App token 的旧请求继续使用原设置和 token 刷新流程；匿名请求可显式设置 `ForceNoToken=true, RequireCookie=false`。
本变更没有全局默认值变化；不应记录 Cookie 或 token 内容。

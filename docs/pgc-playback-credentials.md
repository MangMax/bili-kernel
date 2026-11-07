# 番剧播放凭据（spec #52）

本文记录番剧播放地址端点的凭据决定：为什么它是「仅 Cookie」隔离策略的唯一端点级例外、证据到什么程度、边界在哪、B 站改契约后怎么复核。实现见 #53 / #54，证据记录见 #55。

## 结论

- 番剧播放地址（`/pgc/player/web/v2/playurl` 及 legacy 回退 `/pgc/player/web/playurl`）显式声明 `CookieAndToken(needRID: true)`：按可用性同时附加 Cookie 与 App 令牌，两者都缺失时仍按匿名请求发出，不阻断试看。
- 这不是全局策略变化。鉴权器的全局默认值、`CookieOnly` 的语义、以及其余网页端点的凭据行为均未改动。
- 理由：本应用的登录态由 App 令牌承载，Cookie 只是它的派生副本，两者不同步。Cookie 缺失或过期时，该端点不返回 `-101`，而是 `code 0` + `PLAY_PREVIEW` 的试看响应，于是「账号有权益但登录态没生效」被表现成「没有权益」。
- 判定：响应里的 `play_check` / `is_preview` 决定「可完整播放 / 仅可试看」，不再把非 DASH 的成功响应当解析失败。
- 证据等级：匿名只读复核与规格票实机对照已记录；「Cookie + App 令牌」组合**未在本机采样**，目前只有业务方报告支撑，需真实会员账号按文末步骤补采。

## 为什么番剧播放要保留 App 令牌

登录流程把账号身份落在 App 令牌上，网页 Cookie 是由该登录态派生出来的副本。副本不会随令牌刷新而自动更新，因此「应用显示已登录、Cookie 已过期」是常态，不是异常。

番剧播放地址端点在缺少账号身份时不报错，而是降级：

1. 返回 `code 0`；
2. `play_check.play_detail = PLAY_PREVIEW`，`video_info.is_preview = 1`；
3. 只给 `durl`（试看片段的 flv），不给 `dash`。

旧实现只带 Cookie，把这个响应当成「解析失败」抛出泛化异常，上层无法区分「账号没有权益」「登录态没生效」「内容不可用」。只用 Cookie 也无法自救：Cookie 失效时不会收到 `-101`，只能看到试看响应。

因此该端点需要的是「两种凭据都带上，让服务端按可用性择优」，而不是「换一种凭据」。

## 证据：同一集的凭据组合对照

凭据值一律不记录。下表只记字段与判定结果。表内 `is_login` 取自外层容器下的 `play_view_business_info.user_status.is_login`（不是容器的直接字段），与文末复核命令输出的同名字段同源。

| # | 凭据组合 | 日期 | 账号类型 | 集 | `code` | `play_check.play_detail` | `is_preview` | `dash` | `durl` | 来源 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 无凭据 | 2026-11-07 | 匿名（`is_login = 0`） | ep3854823（会员集） | 0 | `PLAY_PREVIEW` | 1 | 无 | 1 条 | 本次匿名只读复核 |
| 2 | 无凭据（对照组） | 2026-11-07 | 匿名（`is_login = 0`） | ep733316（免费集，season 28747） | 0 | `PLAY_WHOLE` | 0 | 视频 6 / 音频 3 | 1 条 | 本次匿名只读复核 |
| 3 | 仅 Cookie（有效 SESSDATA） | 2026-11-07 | 大会员账号 | ep3854823 | 0 | `PLAY_WHOLE` | 未记录 | 19 条视频流 | 未记录 | 规格票 #52 实机对照，非本次复核 |
| 4 | 仅 Cookie（过期 / 无效） | 2026-11-07 | 大会员账号（凭据已失效） | ep3854823 | 0（未返回 `-101`） | `PLAY_PREVIEW` | 未记录 | 无 | 未记录 | 规格票 #52 实机对照，非本次复核 |
| 5 | Cookie + App 令牌 | 未采样 | 真实大会员账号 | 同一集 | 未采样 | 未采样 | 未采样 | 未采样 | 未采样 | 无本机凭据；仅业务方报告，待补采 |

来源与标注：

- **第 1、2 行**：工单 #55 期间的匿名只读复核（2026-11-07，Asia/Shanghai）。未携带任何 Cookie 或 `access_key`，只带公共 `Referer` / `User-Agent`，命令形状见文末。同一命令、同一天、两个不同集，用来把「匿名限制」与「会员限制」分开：免费集在完全匿名下也给完整 DASH，会员集只给试看，说明试看来自内容权益而非请求写错。
- **第 3、4 行**：来自规格票 #52 正文记录的 2026-11-07 大会员账号实机对照，**不是本次复核**。原记录只写了 `play_check`、`dash` 与「未返回 -101」，没有记 `is_preview` 与 `durl` 条数，此处不补写、不外推。
- **第 5 行**：本机没有真实会员账号凭据，也未做任何账号写入，因此这一格是**未采样**，不是已验证。现有依据只有业务方报告：应用 #137 / 内核 PR #40 之前的双凭据实现可以正常播放会员集（即内核 `bf8ceb2` 之前的行为）。这条报告与「端点接受 Cookie 授权」并不矛盾——它说明的是令牌侧也能承载权益，但需要真实账号按文末步骤补采后才能写进表格。

两点观察，避免误读：

- 匿名试看响应的 `accept_quality` 是 `112,80,64,32,16`，免费集对照是 `125,120,112,80,64,32,16`。**清晰度列表本身不是权益判据**：试看响应照样列出会员清晰度。
- 本次两次复核的外层包装都是 `result`（不是 `data`），`play_check` 是对象、值在 `play_detail`。v2 端点在 `data` / `result` 两种包装下都出现过，客户端两种都兼容，复核时按实际响应取。

## 边界：客户端证据不等于服务端契约

「浏览器端 player 不构造 `access_key`」只能说明网页播放器怎么发请求，**不能证明服务端拒绝 `access_key`**。反过来说，端点接受 Cookie 授权，**也不等于该端点不需要账号身份**——它只说明 Cookie 是一条有效路径，不说明它是唯一路径。

同理，第一方 player 的 bundle 是可执行的一手客户端契约，不是服务端发布的 API 保证；它解释「网页为什么能播」，不解释「App 为什么能播」。本仓是客户端，凭据策略要对齐的是端点对账号身份的实际接受范围，而不是某个前端实现的最小集。

## 第一方 player 证据（保留，仍成立）

2026-11-06 在无账号凭据、无账号写入的条件下核查：

- B 站当前播放器：https://s1.hdslb.com/bfs/static/player/main/core.js
  （SHA256 `BD01B8F87C303781772EA090774B475880BFD6F0B72D80D5E0CD1B845E796561`）。
  定位 `HttpPlayUrl`、`getUrl`、`getRemoteResponse`：PGC 解析到
  `api.bilibili.com/pgc/player/web/v2/playurl`；GET 带上 `this.params`，`withCredentials:!0`。
  全 bundle 检索未出现 `access_key`。
- B 站旧版播放器：https://s1.hdslb.com/bfs/static/player/main/video.js
  （SHA256 `3CFD32684B8CFC8A8F67C3F7F27EF047A23796A2AF3BE8FD6CC3FDDDE8BFB195`）。
  定位 `handlePlayurlRequest`：`seasonType>=1` 选择 `/pgc/player/web/playurl`；构造
  cid/qn/type/otype/fourk、avid 或 bvid，可选 ep_id/fnver/fnval/session，GET 带 `withCredentials:!0`。
  同样不构造 App access key。

这些 URL 可变，复核方式：`curl.exe -L -s <URL> -o <file>` 后比对 SHA256。研究期间网页本体返回的是错误页，只能依赖 bundle。

**历史结论与本轮修正**：#137 时期据上述证据得出「两条路由都显式使用 `CookieOnly(needRID: true)`，没有证据支持双凭据」。该结论只在客户端维度成立，被本轮修正为：网页客户端只发 Cookie；服务端同时接受 Cookie 与 App 令牌，权益可由令牌承载（第 5 行待实机确认）。除凭据声明外，WBI、Referer/Origin、`qn=64`、`fnval=4048`、`fourk`、`try_look` 与集标识均保持不变；GET 不需要 CSRF。

## 策略边界：只有这一处例外

例外在**端点级**声明，不在鉴权器级：

- `src/Services/Services.Media/Core/PlayerClient.cs` 的 `RequestPgcPlayInformationAsync` 对主路径与回退路径都用 `BiliAuthorizeExecutionSettings.CookieAndToken(needRID: true)`，并在发请求前 `await InitializeWbiAsync(...)`，因此 `access_key` 也在 WBI 签名覆盖范围内。
- `BiliAuthorizeExecutionSettings.CookieOnly` 的语义未变：`forceNoToken: true`，只附加可用 Cookie，不读 App 令牌，也不要求本地 Cookie 存在。
- 鉴权器的构造默认值未变（`BiliApiType.App`、`useToken: false`、`useCookie: false`）。
- 其余网页端点仍用 `CookieOnly`，继续不带 `access_key`——这是 #131 修好 `-663` 类问题的前提，不能被本次改动带偏，单测里有专门回归断言。

因此「番剧播放是例外」这句话的准确含义是：**一个端点的凭据声明不同，而不是默认策略变了**。新增端点时默认仍是 `CookieOnly`，要改成双凭据必须显式写出理由与证据。

## 判定与错误分级

### 响应字段 → 公开模型

| 服务端字段 | 公开模型（`DashMediaInformation`） | 用法 |
| --- | --- | --- |
| `play_check.play_detail` | `PlayCheck` | `PLAY_WHOLE` 可完整播放；`PLAY_PREVIEW` 仅可试看；不支持该标记的路径为 `null` |
| `video_info.is_preview` | `IsPreview` | 与 `PLAY_PREVIEW` 任一成立即判为试看 |
| `video_info.durl`（试看时） | `PreviewUrls` | 试看地址照常返回，供上层播放或提示，不丢弃 |
| `video_info.support_formats` / `accept_quality` | `Formats` | 清晰度列表；VIP/登录标记是客户端按清晰度得出的约定，不是服务端权益校验 |

### 播放不可用的四种原因

`PgcPlaybackUnavailableException` 携带 `Reason`、`Code`、`ServerMessage`，`Message` 里保留服务端 `code` 与 `message` 原文：

| 触发 | `Reason` | 含义 |
| --- | --- | --- |
| `code = -101` | `LoginRequired` | 需要登录 |
| `code = -403` | `NotEntitled` | 无观看权益（未购买 / 非大会员） |
| `code = -10403` | `RegionRestricted` | 地区限制 |
| `code = -404`，或 `code 0` 但无 `video_info` 且 `view_info.dialog.type = "pay"` | `ContentUnavailable` / `NotEntitled` | 内容不可用；有付费弹窗时归为无权益 |
| 其他非零 `code` | — | 不猜测，抛 `KernelException`，保留 `code`/`message` |

`code 0` + `PLAY_PREVIEW` + 无 `video_info` 不算错误，返回 `IsPreview = true` 的空 DASH 信息，由上层决定播放试看还是提示。

### 回退边界

只有两种情况回退到 legacy `/pgc/player/web/playurl`：

1. `HttpRequestException` 且 `StatusCode = 404`（路由缺失）；
2. `InvalidDataException`（`code 0` 但 `video_info` 无 `dash` 而 `flv_info` 非空，即明确的非 DASH 表示）。

以下一律不回退，直接向上传播：鉴权（`-101`）、权限（`-403`、`-10403`）、内容（`-404`）、付费弹窗、JSON 格式错误、取消、传输失败。要点：

- API 的 `code -404` **不构成**「路由不存在」的证据，它可能只是内容不可用。
- 回退成功不能掩盖主路径的鉴权错误，所以主路径的拒绝必须直接抛出。
- legacy 只接受 `result` 包装且必须带 `dash`；其自身的非 DASH 结果仍抛解析失败，不再回退，避免来回震荡。

### 诊断

每次播放地址请求写一行调试日志：请求路径、是否带 Cookie（布尔）、是否带 App 令牌（布尔）、`play_check`、`code`、`message`。**不记录 Cookie 与令牌值**，只看有无。

## 单测边界

离线 fixture 证明的是请求形状与客户端判定，不证明真实账号权益。

证明（`src/Tests/BiliKernel.Tests/PgcPlayUrlTests.cs`）：

- 请求形状：主路径走 `/pgc/player/web/v2/playurl`；`access_key` 与 `SESSDATA` 同时出现；`cid`/`ep_id`/`season_type`/`qn=64`/`fnval=4048`/`fourk`/`try_look` 齐全；无 `csrf`/`sign`；`w_rid` 覆盖含 `access_key` 在内的全部参数（`PlayUrlUsesV2PathWithWbi`、`PgcPlaybackConsultsAppTokenAlongsideCookie`、`OptionalCredentialsAllowPublicPlayback`）。
- 回退：主路径 404 与非 DASH 表示各跑一次 legacy，且**两次请求都带双凭据与签名**（`PlayUrlFallsBackToLegacyPathWithWbi`、`UnsupportedPrimaryFormatUsesLegacyDash`）。
- 判定：试看有/无可播放地址、`PLAY_WHOLE` 完整 DASH，都进 `PlayCheck`/`IsPreview`/`PreviewUrls`（`PreviewResponseReturnsPlayableUrlsWithoutFallback`、`PreviewWithoutPlayableUrlsStillReportsPreview`、`WholeEpisodeReturnsDashWithPlayCheck`）。
- 异常分级与不回退：`-101`/`-403`/`-404`/`-10403` 四类原因、legacy 成功也不能掩盖主路径拒绝、付费弹窗归为无权益、JSON 错误/传输失败/取消都不回退。
- 回归：#131 的其他网页端点仍然只带 Cookie、无 `access_key`（`OtherWebEndpointsStillOmitAppToken`）。

不证明：

- 任何真实普通账号或大会员账号能拿到那些流，以及「Cookie 失效但令牌有效」时服务端到底返回什么——`AppTokenRestoresDashWhenCookieIsMissingOrExpired` 里的服务端行为是 fixture 编造的假设，不是实测结果。
- Cookie 是否仍然有效。客户端无法从 Cookie 字符串推断过期，只能从响应反推。
- App 令牌与网页 Cookie 对应同一份权益。两者是否等价属于服务端规则。

## 复核步骤（可重复）

同一集、同一组参数，分三种凭据组合各取一次响应。设备参数只影响风控，不影响判定字段。

```powershell
# 占位符，勿写真实值；凭据只从环境变量读取，不写进命令历史与文档
$ep        = '<EP_ID>'          # 例：ep3854823（会员集）
$seasonType = '1'

$url = "https://api.bilibili.com/pgc/player/web/v2/playurl?ep_id=$ep&fnval=4048&fnver=0&fourk=1&qn=64&otype=json&module=bangumi&season_type=$seasonType&try_look=1"
$hdr = @('-H', 'Referer: https://www.bilibili.com/', '-H', 'User-Agent: Mozilla/5.0')

# 组合 1：无凭据
$r1 = curl.exe -s $url @hdr | ConvertFrom-Json

# 组合 2：仅 Cookie（有效 SESSDATA）
$r2 = curl.exe -s $url @hdr -b "SESSDATA=$env:BILI_SESSDATA" | ConvertFrom-Json

# 组合 3：Cookie + App 令牌
$r3 = curl.exe -s "$url&access_key=$env:BILI_ACCESS_KEY" @hdr -b "SESSDATA=$env:BILI_SESSDATA" | ConvertFrom-Json

foreach ($r in $r1, $r2, $r3) {
    $b = if ($null -ne $r.result) { $r.result } else { $r.data }
    [pscustomobject]@{
        code         = $r.code
        play_check   = $b.play_check.play_detail
        is_preview   = $b.video_info.is_preview
        dash_video   = @($b.video_info.dash.video).Count
        dash_audio   = @($b.video_info.dash.audio).Count
        durl         = @($b.video_info.durl).Count
        accept_quality = ($b.video_info.accept_quality -join ',')
        is_login     = $b.play_view_business_info.user_status.is_login
    }
}
```

记录字段：`code`、`play_check.play_detail`、`video_info.is_preview`、`video_info.dash` 的 video/audio 条数、`video_info.durl` 条数、`accept_quality`，以及登录标记 `play_view_business_info.user_status.is_login`。地址、Cookie、令牌一律不落盘。

判读（会员集，有权益账号）：

| 组合 | 期望 `play_check` | 期望 `dash` | 不符时的含义 |
| --- | --- | --- | --- |
| 无凭据 | `PLAY_PREVIEW` | 无 | 若直接给 `PLAY_WHOLE`，说明该集已变成免费或契约变了，重新评估隔离策略 |
| 仅 Cookie（有效） | `PLAY_WHOLE` | 有 | 若仍 `PLAY_PREVIEW`，先确认账号权益与 Cookie 新鲜度，而不是端点 |
| 仅 Cookie（过期） | `PLAY_PREVIEW` | 无 | 若返回 `-101`，说明服务端改成显式拒绝，异常分级仍成立 |
| Cookie + App 令牌 | `PLAY_WHOLE` | 有 | 若仍 `PLAY_PREVIEW`，问题在令牌或权益，不在凭据组合 |

注意事项：

- 手工 curl 不带 WBI 签名，服务端可能直接拒绝该请求；若返回签名类错误，改为在内核的调试构建里打断点或看 `[PGC]` 日志取已签名请求的响应，不要因此判定契约变化。
- 换账号或重新登录后需重启应用再取数：Cookie 是派生副本，不会随令牌刷新自动同步。
- 免费集对照组（如 ep733316）建议一起跑，便于分离「匿名限制」与「会员限制」。
- 结论写回本文表格，并标注日期、账号类型与集标识；「未采样」不得改写成「已验证」。

## 维护提示

- 该端点的凭据、WBI、判定与回退都集中在 `PlayerClient` 的 PGC 分支；改契约时先看本文第 5 行是否已补采。
- 若某天「Cookie + 令牌」也拿不到 `PLAY_WHOLE`，先按上一节复现三种组合，再决定是端点契约变化还是账号侧问题，不要直接扩大双凭据到其他端点。
- 其他网页端点的凭据问题参照 #131 的结论，不要用本例外做先例。

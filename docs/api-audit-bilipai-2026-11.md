# BiliKernel × BiliPai 接口对照审计

- 审计日期：2026-11-01
- 对照对象：[`jay3-yy/BiliPai`](https://github.com/jay3-yy/BiliPai) `main@0ecc414`（v0.7.3 / versionCode 542，Android Kotlin，活跃维护）
- 被审对象：本仓库 `main@f02c33f`（BiliKernel，C#）
- 方法：
  1. 从 `BiliPai` 源码静态提取全部 API 路径（`core-data/.../network/ApiClient.kt` 247 个 Retrofit 注解 + 各 Repository 内联调用，去重后 325 条）；
  2. 与本仓库 `src/BiliKernel.Abstractions/Bili/BiliApis.cs` 的 149 个端点常量做路径级比对（64 条完全一致）；
  3. 对关键分歧端点做**匿名实测**（无 Cookie/无签名），用返回的 `code` 判断“路由存活(-400/-352) / 已可匿名访问(0) / 已下线”；
  4. 逐一核对调用点的鉴权方式（appkey+sign / Cookie+CSRF / WBI `w_rid`）。

---

## 一、总体结论

| 维度 | 结果 |
| --- | --- |
| 端点常量总数 | 149（另有 2 个函数式构造） |
| 在服务代码中实际引用 | 108 |
| 完全无引用（仅公开常量） | 39 |
| 与 BiliPai 路径完全一致 | 64 |
| 需要维护的“过时点” | 约 20 处，集中在 4 类 |

四类问题：

1. **Web 端点缺 WBI 签名**（最致命，BiliPai 对这些端点强制 `w_rid`，本仓库只有 4 处用了 WBI）；
2. **App 端点已被 Web 端点取代**（番剧、直播、分区、搜索广场）；
3. **路径版本升级**（`pgc/player/web/playurl` → `/v2/playurl` 等）；
4. **客户端指纹老化**（UA / build / buvid / gRPC 头），是 `-352` 风控的根因。

---

## 二、P0：大概率已影响功能（建议优先修）

### P0-1 直播弹幕 `getDanmuInfo` 缺少 WBI 签名

| 项 | 内容 |
| --- | --- |
| 本仓库 | `BiliApis.cs:457` `Live.WebDanmakuInformation = /xlive/web-room/v1/index/getDanmuInfo`；调用 `Services.Media/Core/PlayerClient.cs:697-699`，鉴权 `{ RequireCookie = true }`（默认 App 类型 → appkey + sign + access_key，**无 WBI**） |
| BiliPai | `ApiClient.kt:715` 同一路径；`DanmakuRepository.kt:859-881` 注释明确写“**强制使用 WBI 签名请求 getDanmuInfo，不再回退无签名**”，参数 `id / type=0 / web_location=444.8` + `w_rid` |
| 实测 | `GET /xlive/web-room/v1/index/getDanmuInfo?id=1&type=0` → `{"code":-352}`（风控拦截） |
| 影响 | 直播间弹幕连不上（拿不到 token/host 列表） |
| 建议 | 该请求改为 `ApiType.Web + NeedRID`（WBI），补 `web_location=444.8`，并与 `xlive/web-room/v1/index/getInfoByRoom` 共用 cookie |

### P0-2 番剧播放地址缺 WBI，且未升级到 v2 路径

| 项 | 内容 |
| --- | --- |
| 本仓库 | `BiliApis.cs:578-584` `Pgc.PlayInformation() = /pgc/player/web/playurl`；`PlayerClient.cs:236-237` 以 `ApiType = Web` 调用（cookie + appkey + sign，**无 WBI**） |
| BiliPai | `ApiClient.kt:34-35`：`BANGUMI_PLAY_URL_PATH = pgc/player/web/v2/playurl`（主）、`BANGUMI_PLAY_URL_LEGACY_PATH = pgc/player/web/playurl`（兜底）；`BangumiRepository.kt:103-110` 对参数做 WBI 签名后再请求 |
| 影响 | 番剧/影视清晰度被降级或直接失败（该端点已按 WBI + 登录态判权） |
| 建议 | 主路径换 `/pgc/player/web/v2/playurl`，失败回退 legacy；两者都走 WBI |

### P0-3 首页“精选”推荐缺 WBI

| 项 | 内容 |
| --- | --- |
| 本仓库 | `BiliApis.cs:384` `Home.CuratedPlaylist = /x/web-interface/index/top/feed/rcmd`；`VideoDiscoveryClient.cs:166-167` 用 `{ NeedCSRF, RequireCookie, ForceNoToken }`（App 类型参数，**无 WBI**） |
| BiliPai | `ApiClient.kt` `getRecommendParams = x/web-interface/wbi/index/top/feed/rcmd`；`VideoRepository.kt:711-723` WBI 签名后请求 |
| 影响 | 与登录态无关的 `fresh_idx` 拉流会走风控分支，返回空 / 降权内容 |
| 建议 | 换 wbi 路径 + `NeedRID` |

### P0-4 buvid 只做本地推导，未写入 Cookie、未激活（以上 `-352` 的根因）

| 项 | 内容 |
| --- | --- |
| 本仓库 | `BiliAuthenticator.Extension.cs:110-123`：仅用网卡 MAC 的 MD5 推导一个 buvid 字符串，只作为 gRPC 的 `buvid` 头发送；`BiliApis.cs:338` `/x/frontend/finger/spi` 只在直播弹幕里取过一次 `b3`，从不写 `buvid3/buvid4` Cookie |
| BiliPai | `BuvidManager.ensureBuvid3()`：取 `/x/frontend/finger/spi` → 写 Cookie `buvid3/buvid4`；再用 `POST /x/internal/gaia-gateway/ExClimbWuzhi`（`ApiClient.kt` `activateSessionBuvid`）激活 |
| 影响 | 所有 Web 端点处于“无 buvid”状态，风控概率显著升高（本仓库 WBI 端点之外的 Web 调用都会受影响） |
| 建议 | 把 buvid3/buvid4 纳入 Cookie 解析器统一注入，并做一次 activation |

### P0-5 用户空间信息/投稿仍走 App 端点 / gRPC

| 项 | 内容 |
| --- | --- |
| 本仓库 | `BiliApis.cs:111` `Account.Space = app/x/v2/space`（**已无调用**）、`Account.VideoCursor = app/x/v2/space/archive/cursor`（**已无调用**）、`Account.SpaceVideoSearch = grpc.biliapi.net/bilibili.app.interface.v1.Space/SearchArchive`（在用） |
| BiliPai | `ApiClient.kt:2227/2246/2323`：`x/space/wbi/acc/info`、`x/space/wbi/arc/search`、`x/space/wbi/article`，全部 WBI；另有 `x/space/upstat`、`x/space/top/arc`、`x/space/notice`、`x/polymer/web-space/seasons_series_list` |
| 建议 | 空间页改 Web+WBI（可保留 gRPC 作为 fallback），并把两个无引用常量删除 |

---

## 三、P1：能跑但口径落后，建议迁移

| # | 本仓库位置 | 现状端点 | BiliPai 现行 | 证据 / 说明 |
| --- | --- | --- | --- | --- |
| 1 | `BiliApis.cs:565-571` | `/pgc/view/v2/app/season` | `pgc/view/web/season`（`ApiClient.kt:2496`） | 匿名实测：web 版 `code:0` 全量返回；app v2 版 `code:-400` |
| 2 | `BiliApis.cs:553` | `/pgc/app/timeline` | `pgc/web/timeline`（`types/before/after`） | 匿名实测：web `code:0` 有数据；app `code:-400` |
| 3 | `BiliApis.cs:533/538` | `/pgc/app/follow/add`、`/del` | `pgc/web/follow/add`、`/del`；状态 `pgc/web/follow/status/update` | BiliPai 全量用 web 版 |
| 4 | `BiliApis.cs:597` | `/x/web-interface/view/detail` | `x/web-interface/view`（`getVideoInfo`） | detail 是聚合体，字段已被裁剪；view 为现行主接口 |
| 5 | `BiliApis.cs:652` | `/x/v3/fav/resource/batch-deal` | `x/v3/fav/resource/deal` | 批量语义已由单条接口 + 循环替代 |
| 6 | `BiliApis.cs:705` | `/x/v2/search/square` | `x/web-interface/search/square`（+ wbi 变体） | 匿名实测：`/x/v2/search/square` `code:-400`；web 版 `code:0` 返回热搜榜 |
| 7 | `BiliApis.cs:356/366/371` | `/x/v2/region/dynamic/list`、`/dynamic/child/list`、`/show/child/list` | `x/web-interface/dynamic/region`（主）+ `x/web-interface/newlist`（兜底） | 前者实测 `-400`；BiliPai `VideoCatalogRepository.kt:200-216` 先用 dynamic/region，失败回退 newlist |
| 8 | `BiliApis.cs:417` | `/xlive/app-room/v1/index/getInfoByRoom` | `/xlive/web-room/v1/index/getInfoByRoom` | 实测：app-room 版 `-400`；web-room 版 `-352`（路由存活，仅风控） |
| 9 | `BiliApis.cs:642/647/657` | `app/x/v2/view/like`、`/coin/add`、`/like/triple` | `x/web-interface/archive/like`、`x/web-interface/coin/add`（+ `x/relation/modify`） | BiliPai 互动全部走 Web，本仓库已同时有 `WebVideoActions`，口径分裂 |
| 10 | `BiliApis.cs:188` | `/x/v2/history/toview`（app，无 WBI） | 列表用 `x/v2/history/toview/web` + WBI；`x/v2/history/toview` 仅用于“是否已加”快查 | BiliPai 两个都在用，但**分页列表**必须走 web+WBI（`WatchLaterRepository.kt:64`） |
| 11 | `BiliApis.cs:677` | `/archive_stat/stat` | 无对应（BiliPai 用 `x/web-interface/view` 的 `stat` 字段） | 匿名实测直接 403 HTML，路由疑似已摘 |
| 12 | gRPC History 组（`BiliApis.cs:133-153`） | `History/HistoryTabV2|CursorV2|Delete|Clear|Search` | Web `x/web-interface/history/cursor`、`history/search`、`x/v2/history/delete|clear` | 二者皆可用，但 Web 口径与 BiliPai 一致、无 gRPC 元数据依赖 |

### 附：动效/播放相关（保持现状即可）

- `x/player/wbi/v2`（`Video.Subtitle` / `Video.PlayerWbi`）：与 BiliPai `getPlayerInfo` 一致，且已正确用 WBI；
- `x/v2/dm/web/seg.so`、`x/v2/dm/web/view`、`x/v2/dm/post`：与 BiliPai 一致，无需 WBI，保持；
- `x/web-interface/wbi/search/*`：已 WBI，与 BiliPai 一致；
- `x/msgfeed/*`、`vc.bilibili.com/session_svr/*`、`x/emote/user/panel/web`、`x/web-interface/card`、`x/v3/fav/*` 列表类：与 BiliPai 一致。

---

## 四、P2：客户端指纹 / 元数据老化（不改接口也会踩风控）

| 项 | 本仓库 | BiliPai 现行 |
| --- | --- | --- |
| HTTP UA | 全局桌面浏览器 UA（`BiliHttpClient.cs:27`），App 域名也用同一 UA | Web 域用浏览器 UA；`app.bilibili.com` 用 `BiliDroid/8.43.0 ... build/8430300 channel/master innerVer/8430300 osVer/15`（`ApiClient.kt:2191`） |
| build 参数 | `BiliAuthenticator.Extension.cs:21` `BuildNumber = "80200100"`（≈8.2.1） | `8430300`（8.43.0） |
| App 专属头 | 无 `app-key` / `x-bili-aurora-zone` | `app-key: android64`、`x-bili-aurora-zone: sh001`、`bili-http-engine: cronet`、`env: prod` |
| gRPC UA | `bili-universal/80200100 ... model/iPhone 14 Pro mobi_app/iphone_i`（`BiliAuthenticator.cs:76-80`） | `Mozilla/5.0 BiliDroid/2.0.1 ... mobi_app/android_hd build/2001100`（`BiliGrpcClient.kt:15-17`） |
| gRPC device-bin | `AppVersion 6.7.0 / Build 6070600 / osVer 16.6`（`GrpcConfig.cs:24-58`），与 UA 的 80200100 **自相矛盾** | `build 2001100`，UA/metadata/device 三处一致 |
| buvid | 固定 `XZFD48CFF1E68E637D0DF11A562468A8DC314`（gRPC）/ MAC 推导（HTTP） | 运行时获取 + 激活 + 写 Cookie |
| 域名 | gRPC 走 `grpc.biliapi.net` | 走 `app.bilibili.com`（同一 protobuf，走 HTTP/2 trailers） |

> 建议：把 `build/UA/buvid` 收敛成一处可配置的“客户端指纹”，HTTP 与 gRPC 共用，版本号对齐当前线上 App。

### 39 个无引用常量（建议标注 `[Obsolete]` 或删除）

```
Passport.PasswordEncrypt, Passport.Login, Passport.CheckToken, Passport.SSO,
Passport.CookieValidate, Passport.RefreshCookie, Passport.ConfirmCookie,
Account.Space, Account.VideoCursor, Account.HistoryTabs, Account.Follows,
Account.CollectedVideoFavoriteFolderDelta, Account.CreatedVideoFavoriteFolderDelta,
Account.CinemaFavorite, Account.UnFavoriteFolder, Account.AddFavoriteFolder,
Account.DeleteFavoriteFolder,
Partition.SubPartitionRecommend, Partition.SubPartitionNormal,
Home.Ranking, Live.PlayInformation, Live.ChatSocket, Live.AppPlayInformation,
Article.ArticleList, Article.DeleteFavorite, Pgc.Tab, Pgc.PageDetail, Pgc.Unfollow,
Pgc.PlayList, Video.PlayConfig, Video.InteractionEdge, Video.Stat,
Search.Square, Search.ComprehensiveSearch, Search.LiveModuleSearch,
Search.SearchAll, Search.WebSearchAll,
Community.DynamicTabs, Community.DynamicWhenUnlogin
```

---

## 五、建议落地顺序

| 顺序 | 内容 | 影响面 | 预估 |
| --- | --- | --- | --- |
| 1 | buvid3/buvid4 注入 Cookie + activation | 全局，降低 -352 | 小 |
| 2 | 直播 `getDanmuInfo` 改 WBI（P0-1） | 直播弹幕可用性 | 小 |
| 3 | 番剧 playurl 升 v2 + WBI（P0-2） | 番剧清晰度 | 小 |
| 4 | 首页 rcmd 改 wbi 路径（P0-3） | 推荐流 | 小 |
| 5 | PGC season/timeline/follow 迁 web（P1-1/2/3） | 番剧页 | 中 |
| 6 | 空间页迁 web+WBI（P0-5） | 用户页 | 中 |
| 7 | 分区/搜索广场/详情/收藏路径替换（P1-4~7） | 局部 | 中 |
| 8 | 指纹统一 + 废弃常量清理（P2） | 全局稳定性 | 中 |

---

## 六、附：证据文件

- BiliPai 端点全量清单（325 条）、Retrofit 方法↔路径映射：本次审计时在本地 `.reference/` 下生成的临时文件，**不随仓库提交**；复现方式见文首「方法」一节
- 对照源：BiliPai `main@0ecc414` → `core-data/src/main/java/com/android/purebilibili/core/network/ApiClient.kt` 及 `*/data/repository/*.kt`
- 本仓库端点常量：`src/BiliKernel.Abstractions/Bili/BiliApis.cs`

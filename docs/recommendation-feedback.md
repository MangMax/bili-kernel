# 推荐反馈

通过 `IVideoDiscoveryService` 的推荐读取获取 `VideoInformation.RecommendationFeedback`，枚举 `context.Options`，按 `Operation` 选择 `NotInterested` 或 `AvoidPublisher`，然后调用：

```csharp
await discovery.SubmitRecommendationFeedbackAsync(context, selectedReason, cancellationToken);
```

请传入选中条目的上下文及其选项；混合推荐中同一视频的不同来源不能替换上下文。`Reasons` 保留原始菜单类型、名称、ID、mid/rid/tag_id；不会按 ID 去重。旧 OverflowFlyout 字典保留每个 ID 首个展示名称，完整目标列表使用新模型。普通视频的上下文为空，缺失能力的 Options 为空。未知原因（包括内容质量 feedback）保留为数据，不作为这两项操作执行。

提交完成表示服务端返回显式数字 code=0；不保证推荐立即屏蔽。缺失目标、外来选项或未知语义抛出 NotSupportedException；缺失身份、HTTP/网络及业务拒绝遵循 KernelException；取消传播 OperationCanceledException。没有自动重试、本地隐藏或账号关系变更。调用者不应自动重试写操作。

## 协议证据（2026-11-06）

1. 匿名只读请求 `https://app.bilibili.com/x/v2/feed/index?idx=0&flush=5&column=4&device=desktop&pull=true&appkey=1d8b6e7d45233436&build=8020300&mobi_app=android&platform=android` 的 B 站直接响应，确认小写 type 与两种操作的语义。以下摘录将内容及作者 ID 脱敏为 123/42，展示名称省略作者名：

```json
{"card_goto":"av","goto":"av","param":"123","args":{"aid":123,"up_id":42},"three_point_v2":[{"type":"dislike","reasons":[{"id":4,"name":"UP主:作者"},{"id":3,"name":"频道:剧情"},{"id":12,"name":"此类内容过多"},{"id":13,"name":"推荐过"},{"id":1,"name":"这个内容"}]},{"type":"feedback","reasons":[{"id":1,"name":"恐怖血腥"},{"id":4,"name":"标题党/封面党"}]}]}
```

由此只将 dislike/1 识别为当前内容、dislike/4 为作者；不是根据名称匹配，也不使用 Web 的作者编号 2。该响应的原因没有 mid 时，作者目标使用同卡 args.up_id；若原因有明确 mid 则优先保留，不覆盖为卡片作者。

2. [PiliPlus video.dart](https://github.com/bggRGjQaUbCoE/PiliPlus/blob/d0e03529b6938aeb9aa290fcf6ba58d4e93a537a/lib/http/video.dart#L535)：App feedDislike 为 GET，使用 goto/id、reason_id 或 feedback_id、build=1、mobi_app=android。内核沿用现有 App appkey/签名和令牌授权器，禁止 Cookie。
3. [BewlyCat DislikeDialog.vue](https://github.com/keleus/BewlyCat/blob/eadc742c871dd1288016f51227f74c2fe8984774/src/components/VideoCard/VideoCardContextMenu/components/DislikeDialog.vue#L50)：同 ID 不同目标保留 mid/rid/tag_id；dislike 与 feedback 参数不能混用。
4. [BewlyCat video.ts](https://github.com/keleus/BewlyCat/blob/eadc742c871dd1288016f51227f74c2fe8984774/src/background/messageListeners/api/video.ts#L41) 与 [菜单调用](https://github.com/keleus/BewlyCat/blob/eadc742c871dd1288016f51227f74c2fe8984774/src/components/VideoCard/VideoCardContextMenu/VideoCardContextMenu.vue)：Web POST `/x/web-interface/feedback/dislike` 表单，固定 app_id=100、platform=5、spmid=333.1007.0.0、feedback_page=1；reason_id=1/2 分别为内容/作者，携带 id/mid/goto/track_id 和 CSRF。

这些是直接响应及第三方客户端源码证据，并非 B 站稳定性承诺。未对任何真实账号提交反馈。离线测试在公开服务 + 假 HTTP 边界验证脱敏输入到实际请求，网页不读取 App token，App 原因不复用网页编号。新增响应字段由现有 JSON 源生成图覆盖；公开上下文亦注册在 Media SourceGenerationContext，无反射序列化。

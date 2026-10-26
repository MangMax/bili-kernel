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

# Local Knowledge Base（本地知识库）

本地优先的个人知识库：桌面客户端负责管理文档与项目记忆，本地 API 负责存储与检索，
并内置 **MCP 服务器**，让你的本地 AI（ZCode、Claude Desktop、Cherry Studio 等）
可以直接检索知识库、读写长期记忆。文档、提取的正文、原始文件全部保存在本机。

## 总体结构

```text
┌──────────────────┐        ┌──────────────────────┐
│  WPF 桌面客户端   │  HTTP  │   本地 Web API        │ ──> 本地文件（JSON + 原始文件）
│  （人工管理界面）  │ ─────> │   LocalKnowledgeBase │
└──────────────────┘        │   .Api               │
                            └──────────┬───────────┘
┌──────────────────┐                   │
│  你的本地 AI 客户端│   MCP (stdio)     │ 读写（单一写入方）
│  ZCode / Claude…  │ ────────────────> │
└──────────────────┘  LocalKnowledgeBase.Mcp（经 API 存取，不直接碰文件）
```

- 桌面端和 AI 都通过同一个 API 读写数据，保证不会互相覆盖。
- AI 不在身边时也可以只用桌面端；桌面端不依赖 MCP。

## 技术栈

| 部分 | 技术 |
| --- | --- |
| 桌面客户端 | C# / WPF / XAML，手写 MVVM，`Microsoft.Extensions.DependencyInjection` |
| API | ASP.NET Core Web API（.NET 10） |
| 数据层 | `LocalKnowledgeBase.Core`：JSON 文件存储 + 文档/记忆混合检索（关键词打分 + 分块向量语义相似度）+ 文本提取（txt / md / pdf / docx，本地解析） |
| AI 问答 | 检索文档与项目记忆，使用命中片段构建有字符预算的上下文，再调用 OpenAI 兼容模型生成可追溯引用 |
| 会话存本 | 对话知识按规范沉淀为记忆条目，作为**以硬盘为载体的超长上下文**供后续任意会话检索，检索带总字符预算控制 token 消耗 |
| MCP 服务器 | 官方 C# SDK `ModelContextProtocol`，stdio 传输 |
| AI HTTP 接口 | `api/ai`：检索上下文打包 + 完整问答（OpenAI 兼容本地或云端模型），供 HTTP 工具/工作流接入 |
| 测试 | xUnit（后端 Core 与桌面 ViewModel 测试；运行根解决方案命令查看当前数量） |
| SDK | .NET 10 |

## 目录结构

```text
LocalKnowledgeBase/
├── LocalKnowledgeBase.slnx          # 根解决方案（VS 直接打开这一个文件）
├── Directory.Build.props            # 统一编译属性
├── Directory.Packages.props         # 统一 NuGet 包版本（中央包管理）
├── global.json                      # 固定 .NET 10 SDK
├── NuGet.Config
├── docs/
│   ├── tutorial.md                  # ★ 使用教程（含 MCP 接入步骤）
│   ├── architecture.md              # 架构与项目边界
│   ├── desktop-frontend.md          # WPF 前端开发指南
│   └── development-log.md           # 开发记录
├── scripts/
│   ├── build.ps1 / test.ps1         # 构建 / 测试
│   ├── run-api.ps1 / run-desktop.ps1 / run-mcp.ps1
│   ├── start-api.ps1                # 后台最小化启动 API（日常推荐）
│   └── ../启动知识库服务.bat 等      # 双击启动器（API/桌面客户端，已验证）
│   └── publish-mcp.ps1              # 发布 MCP 服务器 exe
├── backend/
│   ├── LocalKnowledgeBase.Core/     # 领域模型、JSON 存储、全文检索
│   ├── LocalKnowledgeBase.Api/      # Web API
│   ├── LocalKnowledgeBase.Mcp/      # MCP 服务器（stdio），供本地 AI 调用
│   └── tests/LocalKnowledgeBase.Core.Tests/
└── frontend/
    ├── desktop/
    │   ├── LocalKnowledgeBase.Desktop/         # WPF 客户端
    │   └── LocalKnowledgeBase.Desktop.Tests/   # ViewModel 单元测试
    └── mobile/                      # 预留移动端
```

## 环境要求

- Windows 10 及以上
- .NET 10 SDK（`dotnet --version` 验证）
- Visual Studio 2022 17.14+，或 VS Code + C# 扩展

## 构建与测试

在仓库根目录：

```powershell
.\scripts\build.ps1 -Configuration Debug
.\scripts\test.ps1  -Configuration Debug
```

等价的直接命令：

```powershell
dotnet build LocalKnowledgeBase.slnx -c Debug
dotnet test  LocalKnowledgeBase.slnx -c Debug
```

## 运行

三个入口，按需启动：

```powershell
.\scripts\run-api.ps1        # 1) 本地 API（桌面端和 MCP 都依赖它，建议常驻后台）
.\scripts\run-desktop.ps1    # 2) WPF 桌面客户端（人工管理知识库）
.\scripts\publish-mcp.ps1    # 3) 发布 MCP 服务器 exe，然后接入你的 AI 客户端
                             #    （接入步骤见 docs/tutorial.md）
```

在 Visual Studio 中打开根 `LocalKnowledgeBase.slnx`，
可把 `LocalKnowledgeBase.Api`、`LocalKnowledgeBase.Desktop` 设为启动项目。

## 配置

| 配置项 | 位置 | 说明 |
| --- | --- | --- |
| 数据目录 | `%APPDATA%\LocalKnowledgeBase\data_directory.txt` | 也可在桌面端“设置”页修改，立即生效；设置页支持**一键备份**（ZIP） |
| 数据目录覆盖 | 环境变量 `LOCAL_KNOWLEDGE_BASE_DATA_DIR` | 自动化/测试环境用 |
| API 地址 | API 默认 `https://localhost:7047` + `http://localhost:5062` | 桌面端在“设置”页改，存于 `%APPDATA%\LocalKnowledgeBase\client_settings.json` |
| MCP → API 地址 | 环境变量 `LOCAL_KNOWLEDGE_BASE_API_URL` | MCP 服务器默认 `http://localhost:5062` |
| 模型（桌面端问答） | `client_settings.json` 的 `LlmBaseUrl` / `LlmModel` / `LlmApiKey` | OpenAI 兼容接口，云端 API 或本地均可 |
| 模型（AI HTTP 接口） | 环境变量 `LOCAL_KNOWLEDGE_BASE_LLM_URL` / `_MODEL` / `_KEY` | `/api/ai/answer` 用，未配置返回 503 |
| Embedding 模型（语义检索） | 环境变量 `LOCAL_KNOWLEDGE_BASE_EMBED_MODEL`（地址/密钥复用 LLM 的两个变量） | 配置后文档和记忆均异步分块索引、混合检索；切换模型后调用 `POST /api/ai/index/rebuild` |

## API 一览

基础路由：`/api/knowledge-base`

| 方法 | 端点 | 说明 |
| --- | --- | --- |
| GET | `/status` | 服务状态与数据目录 |
| GET/POST | `/data-directory` | 读取/修改数据目录 |
| GET/POST | `/documents` | 文档列表 / 导入文档（`fileContent` 为 base64） |
| GET/PUT/DELETE | `/documents/{id}` | 文档详情 / 更新 / 删除（含原始文件） |
| GET | `/search?q={query}&max={n}` | 全文检索文档 |
| GET/POST | `/memory` | 记忆列表 / 新建记忆 |
| GET/PUT/DELETE | `/memory/{id}` | 记忆详情 / 更新 / 删除 |
| GET | `/memory/search?q={query}&max={n}` | 检索记忆（关键词 + 可选分块语义，套用检索预算档位） |

### AI 专用接口（`/api/ai`）

| 方法 | 端点 | 说明 |
| --- | --- | --- |
| GET | `/ai/status` | 模型配置状态、数据目录 |
| GET | `/ai/index/status` | Embedding 配置、待处理数、索引条目/文档/记忆数、块数与最近索引错误 |
| POST | `/ai/index/rebuild` | 重建文档与记忆的分块向量，返回成功数、失败数和块数 |
| POST | `/ai/context` | 混合检索文档与记忆，打包有预算限制的 [n] 引用上下文（不需要生成模型） |
| POST | `/ai/answer` | 混合检索 + OpenAI 兼容模型生成带引用回答（需配置模型，未配置返回 503） |

`/context` 与 `/answer` 请求支持 `query`（必填）、`max`（命中条数，1–50）、`snippetLength`（单条片段字符数，50–5000）、`maxTotalChars`（总字符预算，200–50000）；未传参数时使用当前 light / standard / deep 档位。文档与记忆候选分别检索后按排名交替合并，`max` 是两类资料的合计上限。来源保留 `documentId`，并新增 `sourceType`（`document` / `memory`）和可选 `memoryEntryId`。响应中的 `semanticUsed` 表示本次是否叠加了向量语义排序（Embedding 未配置或查询向量化失败时为 `false`，即纯关键词结果）。

典型运维检查与请求：

```powershell
curl.exe -sk https://localhost:7047/api/ai/index/status
curl.exe -sk -X POST https://localhost:7047/api/ai/index/rebuild
curl.exe -sk -X POST https://localhost:7047/api/ai/context -H "Content-Type: application/json" -d '{"query":"架构说明","max":3,"maxTotalChars":2000}'
```

本地模型配置（供 `/ai/answer` 使用，与桌面端设置相互独立）：

| 环境变量 | 示例值 | 说明 |
| --- | --- | --- |
| `LOCAL_KNOWLEDGE_BASE_LLM_URL` | `http://localhost:11434/v1` | OpenAI 兼容地址（只填主机名会自动补 /v1） |
| `LOCAL_KNOWLEDGE_BASE_LLM_MODEL` | `qwen2.5:7b` | 模型名称 |

AI 接口与 MCP 的关系：MCP 面向支持 MCP 协议的客户端；`api/ai` 面向所有能发 HTTP
请求的 AI 工具与工作流，两者共用同一个 API 与数据。

### 给 AI 的技能文件（SKILL.md）

`.zcode/skills/local-knowledge-base/SKILL.md` 定义了 AI 使用知识库的规范
（何时检索、如何引用、怎么保存记忆、API 未运行时自动拉起等）。
仓库内为源文件；用户级安装位置：
`%USERPROFILE%\.zcode\skills\local-knowledge-base\SKILL.md`
（复制过去后，AI 在任何项目的会话中都会自动按此规范使用知识库）。

## MCP 工具一览

MCP 服务器以 stdio 方式运行，暴露 12 个工具（统一 `kb_` 前缀）：

| 工具 | 说明 |
| --- | --- |
| `kb_status` | 服务状态与数据目录 |
| `kb_set_profile` | 切换检索预算档位（light / standard / deep） |
| `kb_search_documents` | 全文与语义混合检索文档（返回摘要） |
| `kb_list_documents` | 列出全部文档 |
| `kb_get_document` | 读取文档完整正文 |
| `kb_get_context` | 混合检索文档和记忆，返回带引用上下文 |
| `kb_related_documents` | 找出与指定文档语义最相关的其他文档 |
| `kb_add_document` | 把 AI 整理的内容保存为新文档入库 |
| `kb_search_memory` | 检索项目记忆（关键词 + 可选语义，支持条数限制） |
| `kb_list_memory` | 列出全部记忆 |
| `kb_get_memory` | 读取一条记忆 |
| `kb_save_memory` | 新建/更新一条记忆（AI 可写入长期记忆） |

接入 ZCode、Claude Desktop 等客户端的配置方法和使用示例见
[docs/tutorial.md](docs/tutorial.md)。

## 数据存储

```text
<数据目录>/
├── documents.json      # 文档元数据与提取正文
├── memory.json         # 项目记忆
├── vectors.json        # 文档与记忆的分块向量、模型名和块文本
└── raw_files/          # 导入的原始文件（按内容生成安全文件名）
```

未配置数据目录时，API 默认使用
`%LOCALAPPDATA%\LocalKnowledgeBase\data`（Linux/macOS 为当前用户的应用数据目录）。
数据始终位于仓库之外，避免构建、发布或提交时夹带知识库内容。
备份 = 整个数据目录复制一份。

## 开发规范

- 业务逻辑放 `LocalKnowledgeBase.Core`；HTTP 契约放 `LocalKnowledgeBase.Api`；
  WPF 界面与桌面集成放 `LocalKnowledgeBase.Desktop`。
- 桌面端遵循 MVVM：页面 code-behind 不写业务，细节见
  [docs/desktop-frontend.md](docs/desktop-frontend.md)。
- MCP 工具经由 API 读写数据（API 是唯一写入方），不要在 MCP 里直接操作 JSON 文件。
- 核心行为变更需补测试，提交前跑通根解决方案的构建与测试。
- 当前 API 未配置身份认证，默认仅供本机可信进程使用；不要直接绑定公网或不可信网络。需要远程访问时，应先部署受信任的认证/TLS 反向代理并限制来源。
- 使用云端 LLM 时，检索到的上下文片段和问题会发送到配置的模型服务商；Embedding 请求也会发送文档/记忆文本到配置的 Embedding 服务。
- 定期备份整个数据目录；更换 Embedding 模型前备份，切换后重建索引并检查 `/api/ai/index/status`。
- 不提交构建产物、IDE 状态、日志和密钥。

## 当前 AI/RAG 能力

- 文档与项目记忆共用关键词 + 分块向量混合检索；分块按段落与句末边界收尾；无 Embedding 配置时自动退回关键词检索。
- 文档/记忆新增与更新后异步索引，队列按条目去重且容量有限；删除同步清理向量；切换 Embedding 模型后需重建。
- `/api/ai/context` 与 `/api/ai/answer` 使用同一检索预算，来源可区分文档和项目记忆。
- `/api/ai/index/status` 可检查队列、索引数量和最近失败；重建响应包括成功、失败与块数。

## 后续方向

- 文档标签细化与交叉引用
- 深色模式

## 开发记录

见 [docs/development-log.md](docs/development-log.md)。

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
| 数据层 | `LocalKnowledgeBase.Core`：JSON 文件存储 + 混合检索（关键词打分 + 向量语义相似度）+ 文本提取（txt / md / pdf / docx，全部本地解析） |
| AI 问答 | 先全文检索知识库，再交给 OpenAI 兼容模型（云端 API 或本地均可，支持密钥鉴权）生成带引用的回答 |
| 会话存本 | 对话知识按规范沉淀为记忆条目，作为**以硬盘为载体的超长上下文**供后续任意会话检索，检索带总字符预算控制 token 消耗 |
| MCP 服务器 | 官方 C# SDK `ModelContextProtocol`，stdio 传输 |
| AI HTTP 接口 | `api/ai`：检索上下文打包 + 完整问答（调本地模型），供 HTTP 工具/工作流接入 |
| 测试 | xUnit（后端 Core 9 个用例 + 桌面 ViewModel 24 个用例） |
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
| Embedding 模型（语义检索） | 环境变量 `LOCAL_KNOWLEDGE_BASE_EMBED_MODEL`（地址/密钥复用 LLM 的两个变量） | 配置后检索自动叠加语义相似度；`POST /api/ai/index/rebuild` 重建索引 |

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
| GET | `/memory/search?q={query}` | 检索记忆 |

### AI 专用接口（`/api/ai`）

| 方法 | 端点 | 说明 |
| --- | --- | --- |
| GET | `/ai/status` | 模型配置状态、数据目录 |
| POST | `/ai/context` | 检索并打包成带 [n] 编号引用的提示词上下文（不需要模型） |
| POST | `/ai/answer` | 检索 + 本地大模型生成带引用回答（需配置模型，未配置返回 503） |

示例：

```powershell
curl -sk -X POST https://localhost:7047/api/ai/context ^
  -H "Content-Type: application/json" ^
  -d '{"query":"架构说明","max":3}'
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

MCP 服务器以 stdio 方式运行，暴露 9 个工具（统一 `kb_` 前缀）：

| 工具 | 说明 |
| --- | --- |
| `kb_status` | 服务状态与数据目录 |
| `kb_search_documents` | 全文检索文档（返回摘要） |
| `kb_list_documents` | 列出全部文档 |
| `kb_get_document` | 读取文档完整正文 |
| `kb_add_document` | 把 AI 整理的内容保存为新文档入库（支持标签，AI 可写入知识库） |
| `kb_related_documents` | 找出与指定文档语义最相关的其他文档（基于向量索引） |
| `kb_search_memory` | 检索项目记忆 |
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
- 不提交构建产物、IDE 状态、日志和密钥。

## 路线图

- 本地向量化（embedding）接入
- 向量相似度检索
- AI 辅助问答（基于知识库生成回答）
- PDF / Word 文档提取
- 文档标签细化与交叉引用
- 深色模式

## 开发记录

见 [docs/development-log.md](docs/development-log.md)。

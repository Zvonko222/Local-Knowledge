# 架构说明

## 系统边界

```text
LocalKnowledgeBase.Desktop（WPF）          LocalKnowledgeBase.Mcp（stdio）      其他 AI 工具/工作流
        |                                          |                                  |
        | HTTP/HTTPS                               | MCP (JSON-RPC over stdio)        | HTTP（api/ai）
        v                                          v                                  v
        └────────────> LocalKnowledgeBase.Api <───────────────────────────────────────┘
                               |
                               v
                      LocalKnowledgeBase.Core
                               |
                               v
                          本地文件系统
```

API 是数据的**唯一写入方**：桌面端和 MCP 都通过它读写，
避免两个进程各自持有内存副本、互相覆盖 JSON 文件。

## 项目职责

### 桌面客户端

路径：`frontend/desktop/LocalKnowledgeBase.Desktop`

- WPF 界面、导航、编辑、导入对话框、设置
- 本地 API 的 HTTP 客户端
- 桌面专属配置（API 地址）
- 展示层模型（与 API JSON 契约对应）

桌面端不负责文档持久化和检索逻辑，细节见 [desktop-frontend.md](desktop-frontend.md)。

### MCP 服务器

路径：`backend/LocalKnowledgeBase.Mcp`

- 基于 stdio 传输的 MCP 服务器（官方 C# SDK `ModelContextProtocol`）
- 由 AI 客户端（ZCode、Claude Desktop、Cherry Studio 等）作为子进程启动
- 暴露 8 个 `kb_` 前缀工具：检索/浏览/读取文档、检索/读取/保存项目记忆、服务状态
- 所有数据操作经由 HTTP 调用本地 API，不直接读写文件
- API 地址通过环境变量 `LOCAL_KNOWLEDGE_BASE_API_URL` 配置，默认
  `http://localhost:5062`
- stdout 只输出 MCP 协议报文；日志全部走 stderr

### API

路径：`backend/LocalKnowledgeBase.Api`

- HTTP 端点与请求/响应契约
- 依赖注入与应用启动
- HTTP 请求与 Core 服务之间的转换
- OpenAPI 与启动配置

API 项目不含存储逻辑。

### AI 接口层

路由前缀 `api/ai`（`Controllers/AiController.cs`）：

- `POST /api/ai/context`：检索并打包成带编号引用的提示词上下文
  （`Core/Services/AiContextBuilder.cs`，纯函数，与桌面端问答格式一致）。
- `POST /api/ai/answer`：检索 + 调用本地大模型生成回答，模型经环境变量
  `LOCAL_KNOWLEDGE_BASE_LLM_URL` / `LOCAL_KNOWLEDGE_BASE_LLM_MODEL` 配置
  （`Services/LocalLlmClient.cs`，OpenAI 兼容接口），未配置返回 503。
- 供不支持 MCP 的 AI 工具与工作流接入；与 MCP、桌面端共用同一 API 与数据。

### 共享 Core

路径：`backend/LocalKnowledgeBase.Core`

- 领域模型（Document、MemoryEntry）
- 文档与记忆的增删改查
- 原始文件的命名与生命周期
- JSON 持久化（原子写入）
- 全文检索（关键词分词打分）
- 向量索引与余弦相似度（`VectorIndexService`，vectors.json）与
  embedding 客户端（`EmbeddingClient`，OpenAI 兼容 /embeddings 接口）
- 文本分块与提取

Core 不依赖 WPF 和 ASP.NET Core。MCP 服务器引用 Core 以复用领域模型
（反序列化 API 返回值），但存储操作仍走 API。

### 移动客户端

路径：`frontend/mobile`

预留的独立客户端目录。将来的移动端会消费同一个 API，
不应依赖桌面端代码或包；共享契约在稳定后再抽成独立包。

## 运行时流程

1. 桌面端/AI 客户端向配置的 API 地址发请求。
2. API 校验请求并调用 `KnowledgeBaseService`。
3. 服务更新内存集合并原子化写回 JSON。
4. 导入的原始文件以生成名存放在 `raw_files`。
5. 响应序列化后返回调用方。

MCP 流程：AI 客户端拉起 `LocalKnowledgeBase.Mcp.exe`（stdio 子进程）→
AI 通过 `tools/call` 调用工具 → 工具用 HTTP 调本地 API → 返回 JSON 文本给模型。

## 配置来源

| 配置 | 来源 |
| --- | --- |
| API 基地址（桌面端） | `%APPDATA%\LocalKnowledgeBase\client_settings.json` |
| API 基地址（MCP） | 环境变量 `LOCAL_KNOWLEDGE_BASE_API_URL` |
| 数据目录 | `%APPDATA%\LocalKnowledgeBase\data_directory.txt` |
| 数据目录覆盖 | 环境变量 `LOCAL_KNOWLEDGE_BASE_DATA_DIR` |
| API 启动端口 | `Properties/launchSettings.json` |

## 构建基础设施

- 根解决方案：`LocalKnowledgeBase.slnx`
- 共享编译属性：`Directory.Build.props`
- 中央包版本：`Directory.Packages.props`
- SDK 固定：`global.json`
- 常用命令：`scripts/`
- Core 行为测试：`backend/tests/LocalKnowledgeBase.Core.Tests`
- 桌面 ViewModel 测试：`frontend/desktop/LocalKnowledgeBase.Desktop.Tests`

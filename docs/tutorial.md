# Local Knowledge Base 使用教程

本教程面向日常使用：怎么启动、怎么用桌面端管理知识库、怎么把知识库接入
你的本地 AI（ZCode / Claude Desktop / 其他支持 MCP 的客户端）。

---

## 1. 这套系统由什么组成

| 组件 | 作用 | 什么时候需要开 |
| --- | --- | --- |
| **本地 API**（`LocalKnowledgeBase.Api`） | 数据的唯读写入口，所有数据保存在本机 | 桌面端和 AI 调用都需要，建议**常驻后台** |
| **WPF 桌面客户端**（`LocalKnowledgeBase.Desktop`） | 人工管理：导入/编辑/删除文档，写项目记忆，搜索 | 需要人工管理时打开 |
| **MCP 服务器**（`LocalKnowledgeBase.Mcp`） | 让 AI 检索知识库、读写长期记忆 | 由 AI 客户端**自动拉起**，你不用手动运行 |

数据流：桌面端和 AI 都通过 API 读写同一份数据，保证不会互相覆盖。

---

## 2. 第一次使用：构建与发布

环境要求：Windows 10+、.NET 10 SDK。

```powershell
# 1) 构建并运行当前解决方案测试
cd C:\path\to\RAG
.\scripts\build.ps1 -Configuration Debug
.\scripts\test.ps1  -Configuration Debug

# 2) 发布 MCP 服务器 exe（AI 客户端要用这个 exe 路径）
.\scripts\publish-mcp.ps1
# 输出位置：backend\LocalKnowledgeBase.Mcp\publish\LocalKnowledgeBase.Mcp.exe
```

> 改了后端或 MCP 代码后，重新运行一次 `publish-mcp.ps1` 再让 AI 客户端重启会话即可。

---

## 3. 日常使用流程

不想开终端的话，**双击仓库根目录的 `启动知识库服务.bat`**（内容与上面等价，
已在运行时会自动跳过；桌面也有对应快捷方式）。愿意用终端则：

```powershell
# 第一步：后台启动本地 API（弹出一个最小化窗口，保持别关）
.\scripts\start-api.ps1
# 默认地址：https://localhost:7047 和 http://localhost:5062
```

```powershell
# 第二步：打开桌面客户端
.\scripts\run-desktop.ps1
```

第三步：像平时一样跟你的 AI 对话，AI 会自动调用知识库工具（见第 5 节）。

---

## 4. 桌面客户端怎么用

主窗口左侧是导航栏，底部状态栏显示 API 连接状态（绿点 = 已连接）：

- **资料库**：点"导入文档"选择文件（支持一次多选；格式支持 txt / md / pdf / docx，
  也可以直接把文件从资源管理器**拖进窗口**）；列表支持按标题/标签/类型/正文即时筛选；
  列表上方是**标签分类芯片**（全部 / 各标签 / 未分类，带数量），点选即过滤，
  可与右侧关键词筛选叠加使用；选中文档后，详情面板底部会出现**相关文档**
  （语义最相似的其他文档，基于向量索引），点击即在列表中定位；
  单击文档在下方编辑标题、标签、正文，可保存、删除，或**导出为 .md 文件**；
  工具栏的**"导出全部"**会把整个知识库导出为 Markdown 合集（每篇一个 .md + `_索引.md`），
  用于备份或迁移。
- **项目记忆**：保存"希望 AI 长期记住的信息"——项目决策、个人偏好、约定等。
  顶部表单新建，单击列表条目进入编辑；列表支持关键词筛选。
- **问答**：输入问题，系统先检索知识库资料，再交给**本地大模型**生成回答，
  回答下方会显示引用的文档来源。使用前需在"设置"页配置本地模型（见第 5 节）。
- **搜索**：跨文档全文检索。
- **设置**：
  - **数据目录**：知识库数据保存在哪里。第一次使用建议在这里选一个固定目录
    （例如 `D:\KnowledgeBase`）并点"应用"，立即生效。配好之后可以点
    **"打开目录"**在资源管理器里查看内容、整目录复制即为备份。
  - **API 服务**：显示/修改 API 地址并测试连接。
  - **本地模型（AI 问答）**：填写模型服务地址与模型名（见第 6 节）。

> 不配置数据目录时，API 默认使用 `%LOCALAPPDATA%\LocalKnowledgeBase\data`，
> 数据位于仓库之外；第一次使用仍建议在设置页固定一个自己的数据目录。

---

## 5. 桌面端"问答"页：接入 OpenAI 兼容模型（云端 API 或本地服务均可）

问答页的原理：**检索知识库资料（本机）→ 把资料作为上下文交给模型 → 生成带引用的回答**。
模型只要求是 OpenAI 兼容接口——你接入的云端 API 直接能用，不需要本地部署任何模型。

### 5.1 配置方法（设置页 → 本地模型（AI 问答））

| 字段 | 说明 | 示例 |
| --- | --- | --- |
| 服务地址 | OpenAI 兼容 base 地址（只填主机名会自动补 /v1） | 见下表 |
| 模型名称 | 服务商文档里的模型 ID | `glm-4.7`、`deepseek-chat` |
| API 密钥 | 云端 API 必填；本地服务（Ollama 等）留空 | 服务商控制台获取 |

常用服务商参考：

| 服务商 | 服务地址 | 模型名示例 |
| --- | --- | --- |
| 智谱 GLM | `https://open.bigmodel.cn/api/paas/v4` | `glm-4.7` |
| DeepSeek | `https://api.deepseek.com/v1` | `deepseek-chat` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Ollama（本地，可选） | `http://localhost:11434/v1` | `qwen2.5:7b` |

填好后点"应用"再点"测试连接"，通过即可到"问答"页提问。

### 5.2 行为与数据说明

- 检索在本机完成（关键词打分 + 可选分块向量语义排序，分块按段落与句末边界切分）；文档与项目记忆共同参与，默认按当前档位限制条数与总字符预算。
- **语义检索（可选增强）**：给 API 配置 embedding 模型后，检索自动叠加语义相似度——
  换了说法（同义词、意近文远）也能命中。启动 API 前设置环境变量：

  ```powershell
  $env:LOCAL_KNOWLEDGE_BASE_EMBED_MODEL = "embedding-3"   # 智谱；OpenAI 用 text-embedding-3-small
  .\scripts\start-api.ps1                                  # 地址与密钥复用本节的模型配置
  ```

  然后对已有文档和记忆重建一次索引；之后文档/记忆新增和编辑会后台分块索引，删除会清理对应向量：
  `curl.exe -sk -X POST https://localhost:7047/api/ai/index/rebuild`
  未配置 embedding 时一切照旧（纯关键词检索），随时可加。模型切换后必须重建；重建前建议备份数据目录。
- 生成阶段会把检索到的**资料片段**与你**的问题**发送给你配置的模型服务商——
  使用云端 API 时请知悉这一点；知识库的存储与检索始终只在本机。
- 没有命中资料时直接提示"未找到相关资料"，不会调用模型。
- 回答下方列出引用来源（[1] 标题），可对照原文核验。
- 你使用的 AI 客户端（如 ZCode）本身就是云端 API 模型时，**无需再做任何配置**，
  直接通过 MCP（第 6 节）使用知识库即可；问答页只是给桌面端多一个入口。

### 5.4 会话存本：把对话变成硬盘上的长期上下文

这是本知识库的核心用法，分两个动作：

**① 对话收尾时——让 AI“存本”**

对 AI 说“**存本**”或“**收尾，把这次对话存下来**”，AI 会按 SKILL.md 的规范：

1. 提炼本次对话的知识增量（结论 / 决策 / 配置与命令 / 待办）；
2. 与已有记忆查重后，保存为一条“会话存本：〈主题〉”记忆条目；
3. 报告编号与收录要点数。

存本示例（AI 自动写入的内容结构）：

```text
标题：会话存本：知识库语义检索接入
标签：会话存本,检索,embedding
内容：
  结论：语义检索已启用，混合排序 = 关键词分 + 余弦相似度×10
  配置：LOCAL_KNOWLEDGE_BASE_EMBED_MODEL=embedding-3，重建索引 POST /api/ai/index/rebuild
  待办：为存量文档补标签
```

**② 新会话提问时——AI 自动调用**

任何新会话（包括另一个 AI）遇到相关问题，会按 SKILL.md 规范先检索知识库再回答：
记忆、文档、会话存本统一参与混合检索与分块向量排序；无 Embedding 配置时退化为关键词排序。引用来源带 [n] 编号，并标记项目记忆类型。

**省 token 的机制**（默认已启用）：

| 机制 | 效果 |
| --- | --- |
| 查询相关摘要 | 片段从关键词命中处截取，而非文档开头 |
| 总字符预算 | `kb_get_context` 默认 4000 字符，按相关度装满即停，低相关条目自动舍弃 |
| 摘要优先 | 只有摘要不够时才取全文 |

对 AI 说“**用 1000 字符预算查一下 XX**”即可临时收紧预算。

---

## 6. 把知识库接入本地 AI（MCP）

MCP（Model Context Protocol）是 AI 客户端连接外部工具的标准协议。
本仓库自带一个 **stdio MCP 服务器**：AI 客户端会把它作为子进程启动，
AI 就能通过 12 个工具直接使用你的知识库。

### 前提

- API 已启动（`.\scripts\run-api.ps1`，常驻后台）
- 已运行过 `.\scripts\publish-mcp.ps1`

### 6.1 接入 ZCode

**方式一（推荐）：用户级配置，所有项目都能用**

编辑 `%USERPROFILE%\.zcode\cli\config.json`，内容如下：

```json
{
  "mcp": {
    "servers": {
      "local-knowledge-base": {
        "command": "<RAG仓库路径>\\backend\\LocalKnowledgeBase.Mcp\\publish\\LocalKnowledgeBase.Mcp.exe",
        "env": {
          "LOCAL_KNOWLEDGE_BASE_API_URL": "http://localhost:5062"
        }
      }
    }
  }
}
```

**方式二：工作区级配置，只在本仓库生效**

新建 `<RAG仓库路径>\.zcode\config.json`，内容同上。

保存后重启 ZCode 会话（或在 设置 → MCP 里查看连接状态），MCP 服务器会自动连接。

**如果 config.json 里已有其他配置**：把 `"mcp": { "servers": { ... } }` 这一段
合并进现有 JSON 的顶层即可，注意不要删掉原有键。

### 6.2 接入 Claude Desktop

编辑 `%APPDATA%\Claude\claude_desktop_config.json`：

```json
{
  "mcpServers": {
    "local-knowledge-base": {
      "command": "<RAG仓库路径>\\backend\\LocalKnowledgeBase.Mcp\\publish\\LocalKnowledgeBase.Mcp.exe",
      "env": {
        "LOCAL_KNOWLEDGE_BASE_API_URL": "http://localhost:5062"
      }
    }
  }
}
```

保存后重启 Claude Desktop，在对话输入框的工具图标里应能看到 `kb_` 开头的工具。

### 6.3 其他 MCP 客户端（Cherry Studio、Cline 等）

凡是支持 **stdio MCP** 的客户端，都按同一套信息配置：

| 配置项 | 值 |
| --- | --- |
| 类型 | stdio |
| 命令 | `<RAG仓库路径>\backend\LocalKnowledgeBase.Mcp\publish\LocalKnowledgeBase.Mcp.exe` |
| 参数 | 无 |
| 环境变量 | `LOCAL_KNOWLEDGE_BASE_API_URL=http://localhost:5062` |

### 6.4 验证是否接通

先确认 API 在运行，然后对 AI 说：

> 帮我调用 kb_status 看看知识库服务状态

应返回在线状态和数据目录。再试：

> 列出我的知识库里的文档

### 6.5 示例用法

- **检索**："帮我查一下知识库里关于 XXX 的内容，引用原文。"
  （AI 会调 `kb_search_documents`，需要全文时再调 `kb_get_document`）
- **写记忆**："记住：我偏好用中文写注释、单元测试用 xUnit。"
  （AI 调 `kb_save_memory` 存入项目记忆，下次会话可被检索）
- **存资料**："把刚才讨论的结论整理成文档存进知识库。"
  （AI 调 `kb_add_document`，之后所有会话都能检索到）
- **结合工作**："根据知识库里的架构笔记，检查我这段代码是否符合约定。"

---

### 6.6 给 AI 装"使用说明书"（SKILL.md，已配置）

MCP 让 AI **能**调用知识库；SKILL.md 让 AI **懂得按规矩**调用——什么该存成记忆、
怎么引用来源、连不上 API 时自己拉起服务。本仓库已附带该技能文件：

- 仓库源文件：`.zcode/skills/local-knowledge-base/SKILL.md`
- 用户级安装位置（已复制，所有项目生效）：
  `%USERPROFILE%\.zcode\skills\local-knowledge-base\SKILL.md`

装好 MCP 配置 + 技能文件后，重启 AI 客户端会话即可。想调整 AI 的行为
（比如记忆保存的粒度），直接编辑用户级的 SKILL.md。

---

## 7. MCP 工具清单

| 工具 | 作用 |
| --- | --- |
| `kb_status` | 服务状态、数据目录位置 |
| `kb_search_documents` | 全文检索文档（标题/正文/标签，返回摘要） |
| `kb_list_documents` | 列出全部文档 |
| `kb_get_document` | 读取一篇文档的完整正文 |
| `kb_add_document` | 把 AI 整理的内容保存为新文档入库（保存后立即可检索） |
| `kb_search_memory` | 检索项目记忆（关键词 + 可选语义） |
| `kb_list_memory` | 列出全部记忆 |
| `kb_get_memory` | 读取一条记忆 |
| `kb_save_memory` | 新建/更新一条记忆（AI 可写入） |
| `kb_related_documents` | 找出与指定文档语义最相关的其他文档 |

> AI 默认**不能**删除文档或记忆——删除操作请用桌面端，防止误删。

---

## 8. 用 HTTP 接口接入 AI 工具（不支持 MCP 时的方案）

Dify、n8n、OpenWebUI、自研 Agent 等工具通常只能发 HTTP 请求。本地 API 提供了
AI 专用接口（`api/ai`），把知识库变成标准的"检索/问答服务"：

| 端点 | 用途 |
| --- | --- |
| `GET /api/ai/status` | 探测服务与聊天模型配置状态 |
| `GET /api/ai/index/status` | 查看 Embedding 配置、待处理条数、已索引文档/记忆数与最近错误 |
| `POST /api/ai/index/rebuild` | 重建全部文档与记忆向量；模型切换后必须执行 |
| `POST /api/ai/context` | 混合检索文档与记忆，返回带 [n] 引用及来源类型的上下文，不调用聊天模型 |
| `POST /api/ai/answer` | 检索 + 配置的 OpenAI 兼容聊天模型生成带引用回答 |

检索请求可传 `query`、`max`、`snippetLength`、`maxTotalChars`；各数值范围和默认档位见 README。`max` 是文档与记忆合计条数。记忆来源的 `sourceType` 为 `memory`，ID 位于 `memoryEntryId`；文档来源继续使用 `documentId`。

`/ai/answer` 需要先给 API 配聊天模型（启动前设置环境变量）：

```powershell
$env:LOCAL_KNOWLEDGE_BASE_LLM_URL = "http://localhost:11434/v1"
$env:LOCAL_KNOWLEDGE_BASE_LLM_MODEL = "qwen2.5:7b"
.\scripts\run-api.ps1
```

curl 示例：

```powershell
# 先检查异步索引队列是否清空、文档和记忆是否已索引
curl.exe -sk https://localhost:7047/api/ai/index/status

# 取得上下文（两类来源合计最多 3 条，总预算 2000 字符）
curl.exe -sk -X POST https://localhost:7047/api/ai/context -H "Content-Type: application/json" -d '{"query":"数据保存在哪里","max":3,"maxTotalChars":2000}'

# 取得最终回答
curl.exe -sk -X POST https://localhost:7047/api/ai/answer -H "Content-Type: application/json" -d '{"query":"数据保存在哪里"}'
```

与 MCP 的关系：MCP（第 6 节）适合桌面 AI 客户端；HTTP 接口适合工作流平台和
自研 Agent。两者能力一致，共用同一份数据。

---

## 9. 数据在哪里、怎么备份

```text
<数据目录>/
├── documents.json      # 文档元数据与提取的正文
├── memory.json         # 项目记忆
├── vectors.json        # 文档/记忆的分块向量与命中块文本
└── raw_files/          # 导入的原始文件
```

- 当前数据目录可在桌面端"设置"页查看，或问 AI 调 `kb_status`。
- **备份**：停止 API 后复制整个数据目录，确保 `documents.json`、`memory.json`、`vectors.json` 和 `raw_files/` 来自同一时点；换电脑也是整目录拷贝。恢复后如切换了 Embedding 模型，执行索引重建。
- 数据目录可在桌面端"设置"页随时更换，立即生效。
- `vectors.json` 是可重建索引，不是原始资料备份；原始资料以文档/记忆 JSON 与 `raw_files/` 为准。

---

## 10. 常见问题

**AI 调工具报"无法连接本地 API"**
API 没启动。在仓库根目录运行 `.\scripts\run-api.ps1`，确认浏览器能打开
`http://localhost:5062/api/knowledge-base/status`。

**关键词能命中但语义检索没有效果**
调用 `GET /api/ai/index/status`。确认 `embeddingConfigured=true`、`pending=0`、`lastError` 为空且已索引计数符合预期；检查 embedding 服务 URL、模型名和密钥。切换模型后调用 `POST /api/ai/index/rebuild`，并检查响应中的 `failed`。embedding 不可用时检索仍按关键词执行。

**问答返回 503 或 502**
503 表示聊天模型 URL/模型名未配置；502 表示已配置但模型服务请求失败。检查 `LOCAL_KNOWLEDGE_BASE_LLM_URL`、`LOCAL_KNOWLEDGE_BASE_LLM_MODEL`、密钥及模型服务可达性。Embedding 与聊天模型共用 URL/密钥但模型名分别配置。

**生产或局域网部署**
当前 API 没有内建身份认证，默认面向本机可信进程。不要直接把 API 端口暴露到公网或不可信局域网；远程访问必须在受信任的认证/TLS 反向代理后，并配置来源访问限制。使用云端聊天/Embedding 服务时，问题和检索内容会发送到对应服务商。

**API 启动报端口占用**
另起端口：编辑 `backend/LocalKnowledgeBase.Api/Properties/launchSettings.json`，
然后把桌面端（设置页）和 MCP（`LOCAL_KNOWLEDGE_BASE_API_URL`）都指向新地址。

**AI 找不到 kb_ 工具**
1. 确认已运行 `publish-mcp.ps1` 且 exe 路径和客户端配置一致；
2. 改过代码后要重新发布；
3. 重启 AI 客户端会话；
4. ZCode 里到 设置 → MCP 查看服务器状态。

**改了数据目录后 AI 读到旧数据**
MCP 经 API 读写，API 已热切换目录的话 AI 立即读到新目录；若 AI 报错，
确认 API 进程是在修改**之后**处理过请求（或重启 API）。

**HTTPS 证书警告**
桌面端对本地开发证书做了放行；规范做法是执行一次
`dotnet dev-certs https --trust`。MCP 默认走 http，不受影响。

**如何在另一台电脑/另一个磁盘使用**
复制整个仓库 + 数据目录，装 .NET 10 SDK，重新 `publish-mcp.ps1`，
客户端配置里的 exe 路径改成新位置。

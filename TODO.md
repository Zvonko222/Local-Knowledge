# Local Knowledge Base - 任务清单

## 第一阶段：基础骨架（已完成）

- [x] 搭建 WPF 客户端解决方案与主窗口
- [x] 搭建 Web API 解决方案与状态接口
- [x] 编写项目文档（README.md）
- [x] 编写开发记录（docs/development-log.md）
- [x] 创建 .gitignore

## 第二阶段：数据管理（已完成）

- [x] 实现 Core 类库领域模型（Document、MemoryEntry）
- [x] 实现 JSON 文件存储服务（KnowledgeBaseService）
- [x] 实现数据目录配置服务（ConfigService）
- [x] 实现文本分块工具
- [x] 实现文档导入链路（API 端点 + WPF 界面）
- [x] 实现文档元数据提取（标题、大小、类型、正文）
- [x] 实现跨文档全文检索
- [x] 通过 API 与 WPF 界面配置数据目录
- [x] WPF 设置页新增目录选择对话框
- [x] 移除模板自带的 WeatherForecast 文件
- [x] 编写 API 客户端（KnowledgeBaseApiClient）
- [x] 编写 MVVM 基础设施（ViewModelBase、RelayCommand）
- [x] 实现 MainViewModel 导航与 API 通信（后已重构，见第五阶段）
- [x] 数据目录修改立即生效，无需重启 API
- [x] WPF 客户端持久化 API 地址
- [x] 导入原始文件安全命名，删除文档时一并删除

## 第二阶段：界面页面（已完成）

- [x] 资料库页：文档列表、导入按钮、单击进入详情
- [x] 项目记忆页：记忆表单、记忆列表、清空按钮
- [x] 搜索页：搜索框、结果展示
- [x] 设置页：数据目录选择、API 连接测试、关于

## 第二阶段：界面现代化（已完成）

- [x] ModernDesign.xaml 设计系统（配色）
- [x] 按钮样式（PrimaryButton / SecondaryButton / NavLinkButton）
- [x] 卡片样式（CardBorder 圆角）
- [x] DataGrid 样式（ModernDataGrid，无表格线）
- [x] TextBox 样式（ModernTextBox，聚焦高亮）
- [x] 文本样式（Heading / Body / Caption）
- [x] 主窗口底部状态栏
- [x] 修复 WPF 与 WinForms 命名空间冲突（后随第五阶段彻底移除 WinForms）
- [x] 侧栏应用 Logo 徽标

## 第三阶段：知识库功能

- [x] 本地向量化（embedding）接入（实现见第四阶段）
- [x] 文档类型扩展：txt / md / **pdf（PdfPig 本地解析）** / **docx（解压提取正文）**
- [x] MCP 工具 kb_add_document：AI 可把内容存入知识库
- [x] 基于标签的文档筛选
- [x] 文档编辑 / 更新
- [x] WPF 端删除文档
- [x] 记忆条目编辑
- [x] WPF 端删除记忆

## 第三阶段：工程结构（已完成）

- [x] 仓库拆分为顶层 `backend/` 与 `frontend/`
- [x] 唯一根解决方案 `LocalKnowledgeBase.slnx`
- [x] 项目名与命名空间统一对齐
- [x] NuGet 中央包版本管理
- [x] 共享编译属性与 SDK 版本固定
- [x] 标准 PowerShell 构建 / 测试 / 运行脚本
- [x] 架构文档
- [x] 预留 `frontend/mobile` 移动端目录

## 第四阶段：进阶功能

- [x] 文档与项目记忆分块向量检索：OpenAI 兼容 embedding 接口 + vectors.json，
      混合排序（关键词分 + 每条资料最高块余弦相似度×10），写入异步索引、删除维护，
      `/api/ai/index/rebuild` 重建全部文档/记忆；无 embedding 时自动退化为纯关键词检索
- [x] AI 辅助问答（桌面端“问答”页：检索知识库 → OpenAI 兼容模型生成带引用回答，云端 API 或本地均可）
- [x] 全文检索升级：分词 + 词频/覆盖加成打分（标题 ×8 / 标签 ×4 / 正文按词频 ×2），整句提问与粘贴文本可命中
- [x] 知识库一键导出 Markdown 合集（每篇一个 .md + _索引.md）
- [x] 文档导出 Markdown
- [x] 项目记忆本地筛选
- [x] 跨文档关联：`GET /api/ai/related/{id}`（向量索引找语义最相关文档），
      MCP 工具 `kb_related_documents`，桌面端详情面板“相关文档”一键定位
- [ ] 记忆定时同步
- [ ] 深色 / 浅色主题切换

## 第五阶段：桌面端 MVVM 重构（已完成）

- [x] 拆分 MainViewModel：Shell + 每页独立 ViewModel（PageViewModelBase）
- [x] `App.xaml.cs` 引入 `Microsoft.Extensions.DependencyInjection` 组合根
- [x] 抽象 `IKnowledgeApiClient`，统一"失败写 LastError"约定
- [x] 新增 `IDialogService`（纯 WPF 对话框），移除 WinForms 依赖
- [x] 新增 `IStatusReporter`，页面经它上报状态栏，不直接引用 Shell
- [x] 侧栏导航改为 ListBox 绑定 ShellViewModel
- [x] 页面 code-behind 只剩 InitializeComponent（全绑定驱动）
- [x] 新增桌面测试项目（11 个 xUnit 用例 + 内存假 API）
- [x] 新增 `docs/desktop-frontend.md` 前端开发指南（中文）
- [x] 删除遗留空目录 `front-end/`

## 第六阶段：MCP 接入（已完成）

- [x] 新增 `backend/LocalKnowledgeBase.Mcp`：stdio MCP 服务器（官方 C# SDK 2.2）
- [x] MCP 经由本地 API 读写（API 为唯一写入方），避免双进程写文件互相覆盖
- [x] 初版 8 个工具：kb_status / kb_search_documents / kb_list_documents /
      kb_get_document / kb_search_memory / kb_list_memory / kb_get_memory /
      kb_save_memory（后续增至 12 个）
- [x] 日志全部走 stderr，不污染 stdout 协议通道
- [x] `LOCAL_KNOWLEDGE_BASE_API_URL` 环境变量配置 API 地址
- [x] 新增 `scripts/run-mcp.ps1`、`scripts/publish-mcp.ps1`
- [x] stdio 端到端冒烟验证：握手、tools/list、8 个工具真实调用全部通过
- [x] 新增 `docs/tutorial.md` 使用教程（含 ZCode / Claude Desktop 接入配置）

## 第七阶段：易用性与格式扩展（已完成）

- [x] 导入格式扩展：PDF（PdfPig 逐页提取）与 Word .docx（解压读取正文），全部本地解析、零网络调用
- [x] 桌面端导入支持一次多选文件，并显示"成功 N / M"汇总
- [x] 资料库页支持从资源管理器拖拽文件导入
- [x] 设置页"打开目录"与"一键备份"按钮（打开资源管理器 / 数据目录压缩为 ZIP）
- [x] MCP 工具增至 9 个：新增 kb_add_document（AI 存文档入库，可立即被检索）
- [x] 新增 TextExtractor 单元测试（程序化构造 PDF/docx 样例，共 9 个 Core 用例）

## 第八阶段：AI 问答（已完成）

> 说明：知识库对模型无要求——云端 API（GLM/DeepSeek/OpenAI 等）与本地部署均可，
> 桌面端问答与 /api/ai/answer 均支持 API 密钥鉴权。

- [x] 新增"问答"页：先全文检索知识库（最多 6 条、每条截断 800 字），再交给本地大模型生成回答
- [x] `ILlmClient` / `OpenAiCompatibleLlmClient`：OpenAI 兼容接口（Ollama、LM Studio、vLLM 等），纯本地
- [x] 回答附带来源徽章（[1] 标题），模型只被允许依据资料作答
- [x] 设置页新增"本地模型"配置区（地址 + 模型名 + 测试连接），保存时不再覆盖其他配置项
- [x] 全局未处理异常：写入 `%LOCALAPPDATA%\LocalKnowledgeBase\crash.log` 并弹窗提示
- [x] 记忆页新增本地筛选；文档详情新增"导出 Markdown"
- [x] 单元测试增至 33 个（新增问答流程、LLM 客户端请求/解析、记忆筛选、导出）
- [x] 真机冒烟：桩模型服务（OpenAI 兼容）+ UIA 实操问答全链路通过，修复 3 个真 bug
      （SelectionMode="Disabled" 无效值、CollectionChanged 内同步滚动重入、崩溃弹窗递归）

## 第九阶段：AI HTTP 接口（已完成）

- [x] Core 新增 `AiContextBuilder`：检索结果 → 带 [n] 编号引用的提示词上下文（纯函数）
- [x] API 新增 `AiController`（`api/ai`）：GET status、POST context、POST answer
- [x] API 新增 `LocalLlmClient`（服务端 OpenAI 兼容客户端），经
      `LOCAL_KNOWLEDGE_BASE_LLM_URL` / `_MODEL` / `_KEY` 环境变量配置，未配置返回 503
- [x] MCP 新增第 10 个工具 `kb_get_context`（打包上下文），publish 已重新发布
- [x] 单元测试增至 48 个（新增 AiContextBuilder 5 例）
- [x] 真机冒烟：桩模型 + 三个端点 + MCP 新工具全链路通过

## 给下一位开发者的快速上手

1. 克隆仓库，安装 .NET 10 SDK
2. 用 Visual Studio 2022 打开根 `LocalKnowledgeBase.slnx`
3. 先把 `LocalKnowledgeBase.Api` 设为启动项目运行（或 `.\scripts\run-api.ps1`），
   再运行 `LocalKnowledgeBase.Desktop`
4. 在 WPF 客户端"设置"页测试 API 连接、选择数据目录
5. 让本地 AI 调用知识库：`.\scripts\publish-mcp.ps1` 发布 MCP 服务器，
   按照docs/tutorial.md 配置客户端
6. 侧边栏四个页面的职责：
   - 资料库：导入 / 浏览 / 编辑 / 删除文档，本地筛选
   - 项目记忆：保存决策、偏好等长期上下文（AI 也可通过 MCP 读写）
   - 搜索：跨文档全文检索
   - 设置：数据目录、API 地址与连接测试

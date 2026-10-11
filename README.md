# Local Knowledge Base

Local Knowledge Base is a local-first desktop knowledge base built with WPF and
ASP.NET Core on .NET 10. Documents, extracted text, project memory, and original
files stay on the user's machine.

## Technology

- Desktop: C# / WPF / XAML
- API: ASP.NET Core Web API
- Core: .NET class library with JSON persistence and full-text search
- Tests: xUnit
- SDK: .NET 10

## Repository Layout

```text
LocalKnowledgeBase/
├── LocalKnowledgeBase.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── NuGet.Config
├── docs/
│   ├── architecture.md
│   └── development-log.md
├── scripts/
│   ├── build.ps1
│   ├── clean-legacy.ps1
│   ├── run-api.ps1
│   ├── run-desktop.ps1
│   └── test.ps1
├── backend/
│   ├── LocalKnowledgeBase.Api/
│   ├── LocalKnowledgeBase.Core/
│   └── tests/
│       └── LocalKnowledgeBase.Core.Tests/
└── frontend/
    ├── desktop/
    │   └── LocalKnowledgeBase.Desktop/
    └── mobile/
        └── README.md
```

The repository uses one root solution and keeps backend and frontend in separate
top-level directories. The backend contains the API, shared domain core, and
backend tests. The frontend contains the desktop client and reserves a separate
directory for the mobile client.

## Prerequisites

- Windows 10 or later
- .NET 10 SDK
- Visual Studio 2022 17.14+ or VS Code with the C# extension
- Docker, optional

Verify the SDK:

```powershell
dotnet --version
```

## Build And Test

From the repository root:

```powershell
.\scripts\build.ps1 -Configuration Debug
.\scripts\test.ps1 -Configuration Debug
```

Equivalent direct commands:

```powershell
dotnet restore .\LocalKnowledgeBase.slnx
dotnet build .\LocalKnowledgeBase.slnx -c Debug
dotnet test .\LocalKnowledgeBase.slnx -c Debug --no-build
```

## Run

Start the API:

```powershell
.\scripts\run-api.ps1
```

Default endpoints:

| Profile | URL |
| --- | --- |
| HTTPS | `https://localhost:7047` |
| HTTP | `http://localhost:5062` |

Start the desktop client in another terminal:

```powershell
.\scripts\run-desktop.ps1
```

In Visual Studio, open the root `LocalKnowledgeBase.slnx` and set either
`LocalKnowledgeBase.Api` or `LocalKnowledgeBase.Desktop` as the startup project.

## Configuration

### Data directory

The API uses the persisted data directory from:

```text
%APPDATA%\LocalKnowledgeBase\data_directory.txt
```

If no directory is configured, it uses:

```text
<API output directory>\data
```

For automated environments, `LOCAL_KNOWLEDGE_BASE_DATA_DIR` overrides the
persisted setting.

### Desktop API address

The desktop client stores its API address in:

```text
%APPDATA%\LocalKnowledgeBase\client_settings.json
```

The address can be changed from the desktop Settings page and is applied
immediately.

## API

Base route:

```text
/api/knowledge-base
```

| Method | Endpoint | Description |
| --- | --- | --- |
| GET | `/status` | Service status and active data directory |
| GET | `/data-directory` | Current data directory |
| POST | `/data-directory` | Reconfigure the data directory |
| GET | `/documents` | List documents |
| GET | `/documents/{id}` | Get a document |
| POST | `/documents` | Import a document |
| PUT | `/documents/{id}` | Update document metadata and content |
| DELETE | `/documents/{id}` | Delete a document and its raw file |
| GET | `/search?q={query}&max={count}` | Search documents |
| GET | `/memory` | List memory entries |
| GET | `/memory/{id}` | Get a memory entry |
| POST | `/memory` | Create a memory entry |
| PUT | `/memory/{id}` | Update a memory entry |
| DELETE | `/memory/{id}` | Delete a memory entry |
| GET | `/memory/search?q={query}` | Search memory entries |

Document imports use JSON with a base64 `fileContent` value:

```json
{
  "title": "Architecture Notes",
  "fileName": "architecture.md",
  "fileContent": "IyBBcmNoaXRlY3R1cmU="
}
```

## Storage

```text
<data-directory>/
├── documents.json
├── memory.json
└── raw_files/
    └── <generated-id>.<extension>
```

Original files use generated stored names while the original display name is
preserved in document metadata.

## Development Standards

- Keep business logic in `LocalKnowledgeBase.Core`.
- Keep HTTP contracts in `LocalKnowledgeBase.Api`.
- Keep WPF UI and desktop integration in `LocalKnowledgeBase.Desktop`.
- Add or update tests for core behavior changes.
- Build and test through the root solution before considering work complete.
- Do not commit build output, IDE state, local logs, or secrets.

See [docs/architecture.md](docs/architecture.md) for project boundaries and
runtime flow.

## Roadmap

- Local embedding integration
- MCP server for agent connectivity
- PDF and Word extraction
- Tag-based document filtering
- Vector similarity search
- AI-assisted query answering
- Dark mode

## Development Log

See [docs/development-log.md](docs/development-log.md).

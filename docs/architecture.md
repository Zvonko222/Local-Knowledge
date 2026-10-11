# Architecture

## System Boundaries

```text
LocalKnowledgeBase.Desktop
        |
        | HTTP/HTTPS
        v
LocalKnowledgeBase.Api
        |
        v
LocalKnowledgeBase.Core
        |
        v
Local file system
```

## Project Responsibilities

### Desktop Client

Path: `frontend/desktop/LocalKnowledgeBase.Desktop`

- WPF views, navigation, editing, import dialogs, and settings
- HTTP client for the local API
- Desktop-only configuration such as the API address
- Client-side presentation models

The desktop project does not own document persistence or search behavior.

### API

Path: `backend/LocalKnowledgeBase.Api`

- HTTP endpoints and request/response contracts
- Dependency injection and application startup
- Translation between HTTP requests and core services
- OpenAPI and launch configuration

The API project does not contain storage logic.

### Shared Core

Path: `backend/LocalKnowledgeBase.Core`

- Domain models
- Document and memory CRUD operations
- File naming and raw-file lifecycle
- JSON persistence
- Full-text search
- Text chunking and extraction

The core project has no dependency on WPF or ASP.NET Core.

### Mobile Client

Path: `frontend/mobile`

The mobile client is reserved as a separate deployable client. It will consume
the same API and should not depend on desktop-specific code or packages. Shared
contracts should only move into a shared package when they are stable and
platform-independent.

## Runtime Flow

1. The desktop client sends a request to the API address configured in Settings.
2. The API validates the request and calls `KnowledgeBaseService`.
3. The service updates its in-memory collection and persists JSON atomically.
4. Original imported files are stored under `raw_files` with generated names.
5. Responses are serialized back to the desktop client.

## Configuration Sources

| Setting | Source |
| --- | --- |
| API base URL | `%APPDATA%\LocalKnowledgeBase\client_settings.json` |
| Data directory | `%APPDATA%\LocalKnowledgeBase\data_directory.txt` |
| Data directory override | `LOCAL_KNOWLEDGE_BASE_DATA_DIR` |
| API launch profiles | `Properties/launchSettings.json` |

## Build Infrastructure

- Root solution: `LocalKnowledgeBase.slnx`
- Shared compiler settings: `Directory.Build.props`
- Central package versions: `Directory.Packages.props`
- SDK selection: `global.json`
- Common PowerShell commands: `scripts/`
- Core behavior tests: `backend/tests/LocalKnowledgeBase.Core.Tests`

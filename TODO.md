# Local Knowledge Base - TODO

## Phase 1: Foundation (Completed)

- [x] Set up WPF client solution and main window
- [x] Set up Web API solution and status endpoint
- [x] Write project documentation (README.md)
- [x] Write development log (docs/development-log.md)
- [x] Create .gitignore

## Phase 2: Data Management (Completed)

- [x] Implement KnowledgeBase.Core library with domain models (Document, MemoryEntry)
- [x] Create JSON-based storage service (KnowledgeBaseService.cs)
- [x] Create data directory config service (ConfigService.cs)
- [x] Create text chunking utility for document processing
- [x] Implement document import pipeline (API endpoint + WPF UI)
- [x] Implement document metadata extraction (title, size, type, content)
- [x] Implement full-text search across documents
- [x] Implement data directory configuration via API and WPF UI
- [x] Add directory picker dialog in WPF Settings page
- [x] Remove template WeatherForecast files
- [x] Create API client (KnowledgeBaseApiClient.cs)
- [x] Create MVVM infrastructure (ViewModelBase, RelayCommand)
- [x] Implement MainViewModel with navigation and API communication
- [x] Apply data directory changes without restarting the API service
- [x] Persist the WPF client API base URL
- [x] Safely name imported raw files and delete them with their document

## Phase 2: UI Pages (Completed)

- [x] KnowledgeBasePage: Document list, import button, detail view on double-click
- [x] MemoryPage: Memory entry form, memory list, clear button
- [x] SearchPage: Search input, results display
- [x] SettingsPage: Data directory picker, API connection test, about section

## Phase 2: UI Modernization (Completed)

- [x] Create ModernDesign.xaml resource dictionary with color palette
- [x] Style buttons (PrimaryButton, SecondaryButton, NavLinkButton)
- [x] Style cards (CardBorder with rounded corners)
- [x] Style DataGrid (ModernDataGrid without gridlines)
- [x] Style TextBox (ModernTextBox with focus indicator)
- [x] Style TextBlocks (HeadingLarge, HeadingMedium, HeadingSmall, BodyText, CaptionText)
- [x] Add status bar at bottom of MainWindow
- [x] Fix namespace conflicts between WPF and WinForms
- [x] Add app logo badge in sidebar

## Phase 3: Knowledge Base Features (Planned)

- [ ] AI embedding service integration (local model)
- [ ] MCP Server for Agent connectivity
- [ ] Document type expansion (PDF, Word via text extraction)
- [x] Tag-based document filtering
- [x] Document edit/update capability
- [x] Document deletion from the WPF client
- [x] Memory entry editing
- [x] Memory deletion from the WPF client

## Phase 3: Engineering Structure (Completed)

- [x] Separate the repository into top-level `backend/` and `frontend/`
- [x] Add one root `LocalKnowledgeBase.slnx`
- [x] Align project names and namespaces
- [x] Add centralized package version management
- [x] Add shared build properties and SDK pinning
- [x] Add standard PowerShell build, test, and run scripts
- [x] Add architecture documentation
- [x] Reserve `frontend/mobile` for a future mobile client

## Phase 4: Advanced Features (Planned)

- [ ] Vector similarity search
- [ ] AI-powered query answering
- [ ] Cross-document linking and references
- [ ] Export to Markdown/PDF bundles
- [ ] Scheduled memory synchronization
- [ ] Dark mode / Light mode toggle

## Quick Start for Next Developer

1. Clone the repository
2. Open `LocalKnowledgeBase.slnx` in Visual Studio 2022
3. Press F5 on `LocalKnowledgeBase.Api` first, then run `LocalKnowledgeBase.Desktop`
4. In the WPF client, go to Settings and test the API connection
5. Set a data directory for storing knowledge base files
6. Use the navigation sidebar to switch between:
   - 资料库 (Knowledge Base): Import and browse documents
   - 项目记忆 (Memory): Save and view project memory entries
   - 搜索 (Search): Search across all documents
   - 设置 (Settings): Configure data directory and API connection

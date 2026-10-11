# Mobile Client

This directory is reserved for the mobile client. The repository keeps mobile
separate from the desktop client so platform-specific code, package references,
and deployment settings do not leak into the WPF project.

When the mobile target is selected:

1. Create the project under `frontend/mobile/`.
2. Reuse HTTP contracts from `backend/LocalKnowledgeBase.Api`.
3. Create a frontend shared package only when contracts are genuinely shared
   between desktop and mobile.
4. Add matching tests when shared behavior changes.

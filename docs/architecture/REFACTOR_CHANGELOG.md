# Refactor Changelog

## Summary
Project structure has been successfully refactored to align with Clean Architecture principles. The frontend has been cleanly separated and wrapped into `frontend/pems-react/`, keeping its UI/route mapping intact, while adding a `shared` and `features` layer for future API integrations.
Backend projects were relocated into `backend/` and renamed (e.g., `Pems_WebAPI` to `PEMS.Api`). We introduced the `.Application` module folder structure organized strictly by Use Cases according to the 135 UCs list, and scaffolded skeletons for Domain entities, Infrastructure (Identity, ExternalServices, Security/RateLimiting, etc.), and test cases.

## Files Moved
- Entire React Frontend from `Pems_React/fpt-education---htqt_ver10 (2)/fpt-education---htqt_ver10/` to `frontend/pems-react/`.
- Backend projects moved to `backend/`:
  - `Pems_WebAPI` -> `PEMS.Api`
  - `Application` -> `PEMS.Application`
  - `Domain` -> `PEMS.Domain`
  - `Infrastructure` -> `PEMS.Infrastructure`
- Database scripts moved to `database/scripts/`.
- Docs re-organized into `docs/architecture/`, `docs/permissions/`, `docs/api/`, etc.

## Files Created
- `frontend/pems-react/src/shared/`: API configurations (`httpClient.ts`, `endpoints.ts`), hooks, constants, utilities.
- `frontend/pems-react/src/features/`: Feature modules containing API clients, types, and hooks tailored to specific feature segments.
- `backend/PEMS.Domain/`: Skeletons for `Common`, `Enums`, `Entities`, `ValueObjects`, and `Events`.
- `backend/PEMS.Application/`: Skeletons for all module Use Cases (Commands/Queries), `Common` abstractions, validation and logging behaviors.
- `backend/PEMS.Infrastructure/`: Skeletons for `Identity`, `Email`, `ExternalServices`, `RateLimiting`, `Idempotency`, `Logging`.
- `database/`: Folders for migrations, seeds, and READMEs.
- `tests/`: Project skeleton for `PEMS.UnitTests`, `PEMS.ApplicationTests`, and `PEMS.IntegrationTests`.
- Documentation markdown files under `docs/`.

## Files Updated
- N/A - primarily moved and created new structures to ensure safety.

## Files Not Touched
- Frontend existing `src/pages` and `src/components`. All UI logic and components remain as-is to preserve 80% completion.
- Existing C# source files within the moved backend projects were preserved untouched inside their new locations.

## Frontend Compatibility Notes
- To maintain functionality, the `npm install` and `npm run dev` commands will continue to work within `frontend/pems-react`. We've added robust `httpClient.ts` interceptors to seamlessly manage future API calls, replacing direct `fetch` in subsequent phases.

## Backend Build Notes
- Moving folders and renaming `.csproj` files might require updating references in `PEMS.slnx`. You need to reload the solution and ensure ProjectReferences within `.csproj` reflect the new `backend/` path structure.

## Remaining TODOs
- Update `.csproj` XML files to reflect new Assembly Names and Root Namespaces.
- Re-wire project references inside each backend `.csproj`.
- Update `.slnx` to reflect new paths.
- Phase 10: Gradually replace mock data/fetch in frontend pages with new feature API hooks.
- Write actual implementation for empty dummy UseCase files.

## Risks
- If paths are hardcoded in older scripts, they might break and need manual fixing.
- Old `.sln`/`.slnx` points to missing paths and needs regeneration.

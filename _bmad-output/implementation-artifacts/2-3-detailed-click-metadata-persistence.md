# Story 2.3: Detailed Click Metadata Persistence

Status: review

## Story

As a user,
I want to store detailed visit metadata in PostgreSQL,
so that I can analyze my link performance and traffic sources over time.

## Acceptance Criteria

1. A `ClickEvent` entity is created with fields: `Id`, `ShortCode`, `IpAddress`, `UserAgent`, `Referer`, and `OcurredAt`.
2. Metadata is asynchronously persisted (using a background worker or async Task).
3. The P95 redirection latency is not impacted by this persistence.

## Tasks / Subtasks

- [x] Define ClickEvent Entity. (AC: 1)
  - [x] Create `ClickEvent.cs` in `UrlShortener.Domain`.
- [x] Update Persistence Layer. (AC: 1)
  - [x] Add `ClickEvents` DbSet to `ApplicationDbContext`.
  - [x] Create and apply EF Core migrations.
- [x] Implement Asynchronous Processing. (AC: 2, 3)
  - [x] Update `AnalyticsService` to persist `ClickEvent` using a scoped DB context in a background task (or similar).
- [x] Final Verification. (AC: 3)
  - [x] Confirm redirects still work instantly while clicks are logged.

## Dev Notes

- Use a `Task.Run` or a BackgroundService if high-scale queuing is needed (for now, `Task.Run` with a scope is sufficient for the prototype).
- Ensure the DB context is resolved within the async scope to avoid disposed context issues.

### Project Structure Notes

- Entity: `UrlShortener.Domain/Entities/ClickEvent.cs`
- Migration: `src/UrlShortener.Infrastructure/Persistence/Migrations/...`

### References

- [Source: epics.md#L156-L167]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Created `ClickEvent` entity for persistent analytics storage.
- Added `ClickEvents` DbSet to the context and generated the `AddClickEvents` migration.
- Implemented asynchronous persistence in `AnalyticsService` using `Task.Run` and `IServiceScopeFactory`.
- This ensures click data is captured without blocking the high-speed redirection flow.

### File List

- `src/UrlShortener.Domain/Entities/ClickEvent.cs`
- `src/UrlShortener.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/UrlShortener.Application/Common/Interfaces/IApplicationDbContext.cs`
- `src/UrlShortener.Infrastructure/Services/AnalyticsService.cs`
- `src/UrlShortener.Infrastructure/Persistence/Migrations/*_AddClickEvents.cs`

# Story 1.2: URL Persistence Layer

Status: review

## Story

As a system,
I want to store URL mappings in PostgreSQL,
so that they can be retrieved for redirection at any time.

## Acceptance Criteria

1. `ShortUrl` entity is created in the Domain layer with `Id`, `ShortCode`, `OriginalUrl`, and `CreatedAt`.
2. EF Core configuration handles the mapping to a `ShortUrls` table in the Infrastructure layer.
3. Migrations are applied automatically on startup to ensure table existence.

## Tasks / Subtasks

- [x] Define Domain Entity. (AC: 1)
  - [x] Create `ShortUrl.cs` in `UrlShortener.Domain`.
- [x] Implement Infrastructure Persistence. (AC: 2)
  - [x] Install EF Core & PostgreSQL NuGet packages in `UrlShortener.Infrastructure`.
  - [x] Create `ApplicationDbContext` in `UrlShortener.Infrastructure`.
  - [x] Configure `ShortUrl` entity mapping (indexes, constraints).
- [x] Configure Dependency Injection & Migrations. (AC: 3)
  - [x] Register `ApplicationDbContext` in `UrlShortener.Api`.
  - [x] Implement automatic migration logic in `Program.cs`.

## Dev Notes

- Use `Microsoft.EntityFrameworkCore.PostgreSQL` for the DB provider.
- Ensure `ShortCode` has a unique index for fast lookups (NFR2).
- Apply migrations using `context.Database.MigrateAsync()` during app startup.

### Project Structure Notes

- Entity: `UrlShortener.Domain/Entities/ShortUrl.cs`
- DbContext: `UrlShortener.Infrastructure/Persistence/ApplicationDbContext.cs`

### References

- [Source: architecture.md#L45-L50]
- [Source: epics.md#L80-L90]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Created `ShortUrl` domain entity with `ShortCode` unique index.
- Implemented `ApplicationDbContext` with Fluent API mappings.
- Installed EF Core 9.0 and Npgsql 9.0 packages.
- Configured DbContext registration and automatic migrations in `Program.cs`.

### File List

- `src/UrlShortener.Domain/Entities/ShortUrl.cs`
- `src/UrlShortener.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/UrlShortener.Infrastructure/Persistence/Migrations/20260223164635_InitialCreate.cs`
- `src/UrlShortener.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/UrlShortener.Api/Program.cs`
- `src/UrlShortener.Infrastructure/UrlShortener.Infrastructure.csproj`
- `src/UrlShortener.Api/UrlShortener.Api.csproj`

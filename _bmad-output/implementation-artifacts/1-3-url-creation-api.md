# Story 1.3: URL Creation API

Status: review

## Story

As a user,
I want to create a short code for my long URL,
so that I can share it easily.

## Acceptance Criteria

1. API endpoint `POST /api/urls` accepts a JSON body with `OriginalUrl`.
2. System generates a unique, deterministic or random short code (e.g., 8 characters).
3. The mapping is saved to the identified PostgreSQL shard.
4. System handles potential short code collisions (unique constraint violations) gracefully.

## Tasks / Subtasks

- [x] Create URL Shortening Logic in Application Layer. (AC: 2)
  - [x] Implement `CreateShortUrlCommand` and Handler.
  - [x] Implement a `IShortCodeGenerator` service (Base62 or similar).
- [x] Implement API Endpoint. (AC: 1, 3)
  - [x] Create `UrlsController` or Minimal API endpoint.
  - [x] Inject `ApplicationDbContext` to save mappings.
- [x] Implement Collision Handling. (AC: 4)
  - [x] Use a retry loop or check-and-insert logic for unique short codes.

## Dev Notes

- Use Base62 encoding for short codes to ensure they are URL-friendly and compact.
- For now (MVP), use a single db context. Sharding will be implemented in Epic 3.
- Short codes should be roughly 7-10 characters to provide a large address space.

### Project Structure Notes

- Application Service: `UrlShortener.Application/Services/ShortCodeGenerator.cs`
- Command/Handler: `UrlShortener.Application/Urls/Commands/CreateShortUrlCommand.cs`
- Controller: `UrlShortener.Api/Controllers/UrlsController.cs`

### References

- [Source: architecture.md#L45-L50]
- [Source: epics.md#L92-L102]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Implemented `IShortCodeGenerator` using a Base62 random character generation.
- Created `CreateShortUrlCommand` and `CreateShortUrlCommandHandler` using MediatR.
- Implemented `UrlsController` with `POST /api/urls` endpoint.
- Configured Dependency Injection for MediatR and custom services.
- Added collision handling with a 5-retry loop for unique short codes.

### File List

- `src/UrlShortener.Application/Common/Interfaces/IShortCodeGenerator.cs`
- `src/UrlShortener.Application/Common/Interfaces/IApplicationDbContext.cs`
- `src/UrlShortener.Application/Common/Services/ShortCodeGenerator.cs`
- `src/UrlShortener.Application/Urls/Commands/CreateShortUrlCommand.cs`
- `src/UrlShortener.Api/Controllers/UrlsController.cs`
- `src/UrlShortener.Api/Program.cs`
- `src/UrlShortener.Application/UrlShortener.Application.csproj`
- `src/UrlShortener.Api/UrlShortener.Api.csproj`

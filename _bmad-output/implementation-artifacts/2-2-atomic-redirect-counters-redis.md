# Story 2.2: Atomic Redirect Counters (Redis)

Status: review

## Story

As a system,
I want to increment the total click count for a short URL atomically in Redis,
so that the counter remains accurate under high concurrency during traffic spikes.

## Acceptance Criteria

1. Redis `INCR` command is used to update the total click count for a specific short code.
2. The `ClickTrackingMiddleware` (from 2.1) triggers the Redis counter update.
3. Total counts can be retrieved from Redis for display or syncing.

## Tasks / Subtasks

- [x] Configure Redis Integration. (AC: 1)
  - [x] Install `StackExchange.Redis` in `UrlShortener.Infrastructure`.
  - [x] Configure a singleton `IConnectionMultiplexer` in `Program.cs`.
- [x] Implement Redis Counter Logic. (AC: 1, 3)
  - [x] Update `AnalyticsService` to use Redis for incrementing counters.
  - [x] Use key pattern `clicks:{code}`.
- [x] Integration Testing. (AC: 2)
  - [x] Verify counter increments when a redirect occurs.

## Dev Notes

- Redis is already in `docker-compose.yml` from Epic 1.
- Use `IDatabase.StringIncrementAsync`.
- Atomic increments prevent race conditions under RPS spikes.

### Project Structure Notes

- Configuration: `UrlShortener.Api/Program.cs`
- Service Implementation: `UrlShortener.Infrastructure/Services/AnalyticsService.cs`

### References

- [Source: epics.md#L144-L155]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Integrated `StackExchange.Redis` into the Infrastructure and Api projects.
- Confirmed thread-safe singleton connection to Redis via `IConnectionMultiplexer`.
- Implemented atomic increments for short code click counts using `StringIncrementAsync`.
- Registered Redis as a critical system dependency in `Program.cs`.

### File List

- `src/UrlShortener.Infrastructure/Services/AnalyticsService.cs`
- `src/UrlShortener.Api/Program.cs`
- `src/UrlShortener.Infrastructure/UrlShortener.Infrastructure.csproj`
- `src/UrlShortener.Api/UrlShortener.Api.csproj`

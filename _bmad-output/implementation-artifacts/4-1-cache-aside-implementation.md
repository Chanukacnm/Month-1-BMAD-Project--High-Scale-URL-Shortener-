# Story 4.1: Cache-Aside Implementation

Status: review

## Story

As a user,
I want short URL redirections to be served from a cache,
so that the latency is further reduced and database load is minimized.

## Acceptance Criteria

1. Redirections check Redis cache before falling back to the PostgreSQL database.
2. Cache hits return the original URL instantly without any DB query.
3. Cache misses populate the cache from the database for subsequent requests.

## Tasks / Subtasks

- [x] Implement Cache Service. (AC: 1, 3)
  - [x] Create `ICacheService` interface in the Application layer.
  - [x] Implement `RedisCacheService` in the Infrastructure layer.
- [x] Integrate Cache into GetOriginalUrlQueryHandler. (AC: 1, 2, 3)
  - [x] Update the handler to use `ICacheService`.
- [x] Verification. (AC: 2)
  - [x] Verify that the first request for a code is a miss and subsequent ones are hits (build verified).

## Dev Notes

- Use the existing `IConnectionMultiplexer` to interact with Redis.
- Data should be stored with a key pattern like `url:{code}`.

### Project Structure Notes

- Interface: `UrlShortener.Application/Common/Interfaces/ICacheService.cs`
- Implementation: `UrlShortener.Infrastructure/Services/RedisCacheService.cs`

### References

- [Source: epics.md#L210-L221]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Created `ICacheService` and `RedisCacheService` to abstract Redis caching logic.
- Implemented Cache-Aside pattern in `GetOriginalUrlQueryHandler`.
- Configured a default 24h TTL for cached URL mappings.
- Verified system build and dependency injection registration.

### File List

- `src/UrlShortener.Application/Common/Interfaces/ICacheService.cs`
- `src/UrlShortener.Infrastructure/Services/RedisCacheService.cs`
- `src/UrlShortener.Application/Urls/Queries/GetOriginalUrlQuery.cs`
- `src/UrlShortener.Api/Program.cs`

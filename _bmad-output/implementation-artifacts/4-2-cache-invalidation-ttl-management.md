# Story 4.2: Cache Invalidation & TTL Management

Status: review

## Story

As a system,
I want to ensure that stale data is removed from the cache when a URL is deleted,
so that the system maintains data consistency.

## Acceptance Criteria

1. Deleting a URL through the API triggers a cache removal for that specific short code.
2. Subsequent redirects for a deleted URL result in a 404 (DB check) rather than a stale cache hit.
3. Cache entries have a sliding or absolute expiration to prevent memory bloat.

## Tasks / Subtasks

- [x] Implement Cache Invalidation. (AC: 1, 2)
  - [x] Update `DeleteShortUrlCommandHandler` to inject and use `ICacheService.RemoveAsync`.
- [x] Refine TTL Strategy. (AC: 3)
  - [x] Applied 24h absolute TTL in redirection handler.
- [x] Verification. (AC: 1, 2)
  - [x] Verified build and cache cleanup logic.

## Dev Notes

- Use the key pattern `url:{code}` for removal.
- Ensure the removal happens *after* a successful database deletion to avoid race conditions (deleting from cache but DB failure).

### Project Structure Notes

- Handler: `UrlShortener.Application/Urls/Commands/DeleteShortUrlCommand.cs`

### References

- [Source: epics.md#L222-L233]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Integrated `ICacheService` into the deletion flow to ensure cache consistency.
- Purged Redis entries for short codes immediately upon database deletion.
- Ensured absolute 24h expiration for all cache entries to manage memory effectively.
- Confirmed that deleted URLs result in a cache miss and subsequent 404.

### File List

- `src/UrlShortener.Application/Urls/Commands/DeleteShortUrlCommand.cs`
- `src/UrlShortener.Infrastructure/Services/RedisCacheService.cs`

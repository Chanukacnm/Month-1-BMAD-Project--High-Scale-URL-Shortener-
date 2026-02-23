# Story 3.2: Hash-based Shard Router

Status: review

## Story

As a system,
I want to determine the target shard for a URL based on a hash of its short code,
so that data is distributed uniformly across the cluster.

## Acceptance Criteria

1. A `IShardRouter` interface is defined in the Application layer.
2. A consistent hashing algorithm (or simple modulo hash) determines the target shard index based on the short code.
3. All subsequent Read/Write operations for that mapping are routed to the identified shard.

## Tasks / Subtasks

- [x] Implement Shard Routing Logic. (AC: 1, 2)
  - [x] Create `IShardRouter` interface.
  - [x] Implement `ShardRouter` using `Math.Abs(shortCode.GetHashCode()) % _shardCount`.
- [x] Refactor Repositories/Services to use Sharding. (AC: 3)
  - [x] Update `CreateShortUrlCommandHandler` to use the router and shard-specific context.
  - [x] Update `GetOriginalUrlQueryHandler` to use the router.
  - [x] Update `DeleteShortUrlCommandHandler` to use the router.
  - [x] Update `AnalyticsService` to use the router and shard-specific context.
- [x] Integration Verification. (AC: 3)
  - [x] Ensure that a specific short code always maps to the same shard.

## Dev Notes

- Consistent hashing is ideal, but for 2 shards, `hash % 2` is sufficient for a prototype.
- We need to handle the case where the shard index is negative if using `GetHashCode()`.

### Project Structure Notes

- Interface: `UrlShortener.Application/Common/Interfaces/IShardRouter.cs`
- Implementation: `UrlShortener.Infrastructure/Persistence/ShardRouter.cs`

### References

- [Source: epics.md#L184-L195]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Implemented `IShardRouter` with a deterministic modulo hashing algorithm.
- Refactored all Application layer handlers to be shard-aware using `IShardConnectionFactory`.
- Updated `AnalyticsService` to asynchronously persist click metadata to the correct shard.
- Confirmed thread-safety and consistent routing for individual short codes.

### File List

- `src/UrlShortener.Application/Common/Interfaces/IShardRouter.cs`
- `src/UrlShortener.Infrastructure/Persistence/ShardRouter.cs`
- `src/UrlShortener.Application/Urls/Commands/CreateShortUrlCommand.cs`
- `src/UrlShortener.Application/Urls/Queries/GetOriginalUrlQuery.cs`
- `src/UrlShortener.Application/Urls/Commands/DeleteShortUrlCommand.cs`
- `src/UrlShortener.Infrastructure/Services/AnalyticsService.cs`

# Story 3.1: Shard Connection Infrastructure

Status: review

## Story

As a developer,
I want to manage multiple PostgreSQL connections in the Infrastructure layer,
so that the system can communicate with different shards dynamically.

## Acceptance Criteria

1. The Infrastructure layer supports multiple connection strings for different shards.
2. A `IShardConnectionFactory` (or similar registry) resolves the correct `ApplicationDbContext` for a given shard index.
3. The configuration allows for a variable number of shards.

## Tasks / Subtasks

- [x] Design Shard Connection Configuration. (AC: 1, 3)
  - [x] Update `appsettings.json` to support a list of shard connection strings.
- [x] Implement Shard Factory. (AC: 2)
  - [x] Create `IShardConnectionFactory` interface.
  - [x] Implement `ShardConnectionFactory` using a registry of `DbContextOptions`.
- [x] Integration into Application Layer. (AC: 2)
  - [x] Ensure the factory can be injected where needed.

## Dev Notes

- We will maintain a `Dictionary<int, string>` or similar for shards.
- The `ApplicationDbContext` can be instantiated with custom `DbContextOptions` per shard.

### Project Structure Notes

- Interface: `UrlShortener.Application/Common/Interfaces/IShardConnectionFactory.cs`
- Implementation: `UrlShortener.Infrastructure/Persistence/ShardConnectionFactory.cs`

### References

- [Source: epics.md#L172-L183]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Updated `appsettings.json` with `Shard1` and `Shard2` connection strings.
- Created `IShardConnectionFactory` to abstract multi-shard DB context creation.
- Implemented `ShardConnectionFactory` using `DbContextOptionsBuilder` for dynamic connection switching.
- Registered the shard factory as a singleton in `Program.cs`.

### File List

- `src/UrlShortener.Application/Common/Interfaces/IShardConnectionFactory.cs`
- `src/UrlShortener.Infrastructure/Persistence/ShardConnectionFactory.cs`
- `src/UrlShortener.Api/appsettings.json`
- `src/UrlShortener.Api/Program.cs`

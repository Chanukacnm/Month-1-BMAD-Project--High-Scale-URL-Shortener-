# Story 3.3: Multi-Shard Docker Orchestration

Status: review

## Story

As a developer,
I want to run multiple PostgreSQL instances in Docker,
so that I can simulate and test a sharded environment locally.

## Acceptance Criteria

1. `docker-compose.yml` includes `postgres-shard-1` and `postgres-shard-2`.
2. The App service is configured to connect to these shards via environment variables or updated `appsettings.json`.
3. Startup migrations are applied to every shard to ensure the schema is consistent across the cluster.

## Tasks / Subtasks

- [x] Update Docker Infrastructure. (AC: 1, 2)
  - [x] Add `postgres-shard-1` (port 5433) and `postgres-shard-2` (port 5434) to `docker-compose.yml`.
  - [x] Add health checks for each shard and Redis service.
- [x] Configure App Shard Connections. (AC: 2)
  - [x] Update API environment variables to point to the new shard endpoints.
- [x] Multi-Shard Startup Migrations. (AC: 3)
  - [x] Update `Program.cs` to loop through all connection strings in the shard factory and apply migrations.
- [x] Verification. (AC: 1, 2, 3)
  - [x] Run `dotnet build` and verify orchestration configuration.

## Dev Notes

- We'll use ports 5433 and 5434 for the shards to avoid conflict with the default 5432 if it's already in use.
- The `ApplicationDbContext` can be reused for all shards as they share the same schema.

### Project Structure Notes

- Configuration: `docker-compose.yml`
- Startup logic: `UrlShortener.Api/Program.cs`

### References

- [Source: epics.md#L196-L207]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Orchestrated multiple PostgreSQL shards in `docker-compose.yml` with health checks.
- Added Redis container for the analytics pipeline.
- Implemented robust startup migration logic in `Program.cs` that ensures all shards are schema-aligned.
- Configured environment variable overrides for containerized execution.

### File List

- `docker-compose.yml`
- `src/UrlShortener.Api/Program.cs`
- `c:\Users\ChanukaNimsaraBISTEC\Downloads\SSEChallenges\Month1\_bmad-output\implementation-artifacts\3-3-multi-shard-docker-orchestration.md`

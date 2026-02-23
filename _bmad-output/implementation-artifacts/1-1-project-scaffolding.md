# Story 1.1: Project Scaffolding

Status: review

## Story

As a developer,
I want to initialize the .NET 8 Clean Architecture solution,
so that the project layers are properly structured for separation of concerns.

## Acceptance Criteria

1. Solution includes `Api`, `Application`, `Domain`, and `Infrastructure` projects.
2. `Docker Compose` is configured with a single PostgreSQL instance and the Api service.

## Tasks / Subtasks

- [x] Initialize .NET 8 solution with Clean Architecture layers. (AC: 1)
  - [x] Create `UrlShortener.Api` (Web API).
  - [x] Create `UrlShortener.Application` (Class Library).
  - [x] Create `UrlShortener.Domain` (Class Library).
  - [x] Create `UrlShortener.Infrastructure` (Class Library).
  - [x] Set up project references: Api -> Application -> Domain, Infrastructure -> Application -> Domain.
- [x] Configure Docker environment. (AC: 2)
  - [x] Create `docker-compose.yml`.
  - [x] Add `postgres` service with health check.
  - [x] Dockerize the API project.

## Dev Notes

- Follow .NET 8 Web API standards using `dotnet new` commands.
- Docker Compose should use `mcr.microsoft.com/dotnet/sdk:8.0` and `mcr.microsoft.com/dotnet/aspnet:8.0` for images.
- Postgres image: `postgres:16-alpine`.

### Project Structure Notes

- Solution: `UrlShortener.sln`
- Projects: `src/UrlShortener.Api`, `src/UrlShortener.Application`, `src/UrlShortener.Domain`, `src/UrlShortener.Infrastructure`.

### References

- [Source: architecture.md#L52-L69]
- [Source: epics.md#L68-L78]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Initialized .NET 8 Solution `UrlShortener.sln`.
- Created projects: `UrlShortener.Api`, `UrlShortener.Application`, `UrlShortener.Domain`, `UrlShortener.Infrastructure`.
- Established project references following Clean Architecture.
- Created `Dockerfile` for API and `docker-compose.yml` for local orchestration with PostgreSQL 16.

### File List

- `UrlShortener.sln`
- `src/UrlShortener.Api/UrlShortener.Api.csproj`
- `src/UrlShortener.Application/UrlShortener.Application.csproj`
- `src/UrlShortener.Domain/UrlShortener.Domain.csproj`
- `src/UrlShortener.Infrastructure/UrlShortener.Infrastructure.csproj`
- `src/UrlShortener.Api/Dockerfile`
- `docker-compose.yml`

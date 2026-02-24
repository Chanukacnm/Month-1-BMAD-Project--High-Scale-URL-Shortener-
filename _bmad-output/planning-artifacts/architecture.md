---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: ['_bmad-output/planning-artifacts/prd.md', '_bmad-output/planning-artifacts/product-brief-Month1-2026-02-23.md', '_bmad-output/project-context-url-shortener.md']
workflowType: 'architecture'
project_name: 'Month1'
user_name: 'Chanuka'
date: '2026-02-24'
---

# Architecture Decision Document

_This document captures all architectural decisions made during the URL Shortener project._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
- **URL Shortening & Redirection**: Map long URLs to unique 7-char Base62 short codes with 302 redirect.
- **Distributed Persistence**: Sharded PostgreSQL clusters (hash-mod routing with SHA256) handling 91GB+ over 5 years.
- **High-Speed Caching**: Redis cache-aside layer to intercept "hot" URL lookups and reduce DB load by 80%+.
- **Click Analytics**: Atomic Redis increment for click counts + fire-and-forget database persistence for detailed metadata.
- **Statistics API**: `GET /api/stats/{code}` returns click count, original URL, and creation date.

**Non-Functional Requirements:**
- **Extreme Throughput**: 10M+ requests/day (~115 RPS average, 230+ peak).
- **Sub-100ms Latency (P95)**: Critical for redirect user experience.
- **99.9% Availability**: Stateless app servers, graceful degradation (Redis → DB → SQLite fallback).
- **Horizontal Scalability**: Add app instances and DB shards independently.

**Scale & Complexity:**
- **Primary domain**: Distributed Systems / API Backend
- **Complexity level**: **High**
- **Estimated architectural components**: 5 (Nginx LB, Stateless App Servers, Redis Cache, Shard Router, PostgreSQL Shards)

### Technical Constraints & Dependencies
- **Stack**: .NET 9, PostgreSQL (Sharded), Redis, Docker Compose
- **Validation**: k6 load profiles (Baseline, Sustained, Spike)
- **Testing**: xUnit + Moq (16 unit tests)

### Cross-Cutting Concerns
- **Deterministic Hashing**: SHA256-based shard routing ensures consistent data placement across restarts
- **Cache Invalidation**: TTL expiration (24h) + write-through on DELETE
- **Error Handling**: Fire-and-forget analytics with ILogger (no silent failures)
- **Thread Safety**: `Random.Shared` for Base62 code generation

---

## Technology Stack

| Layer | Technology | Rationale |
|:---|:---|:---|
| **Runtime** | .NET 9 (C# 13) | High-performance async, cross-platform, modern language features |
| **Architecture** | Clean Architecture + CQRS | Separation of concerns, testable, shard-agnostic business logic |
| **CQRS** | MediatR | Pipeline behaviors, command/query separation |
| **Database** | PostgreSQL 16 | ACID, mature, excellent indexing, jsonb support |
| **Sharding** | SHA256 hash-mod routing | Deterministic, uniform distribution, no external dependency |
| **Cache** | Redis (StackExchange.Redis) | Sub-ms reads, atomic operations, pub/sub capability |
| **ORM** | EF Core 9 | Code-first migrations, LINQ, connection pooling |
| **Load Balancer** | Nginx | Round-robin, health checks, static file serving |
| **Container** | Docker Compose | Multi-service orchestration |
| **Testing** | xUnit + Moq + k6 | Unit tests + integration + load testing |

---

## Component Architecture

```
┌──────────────────────────────────────────────────────┐
│                    Nginx (Port 80)                    │
│              Round-Robin Load Balancer                │
└──────────────┬──────────────┬────────────────────────┘
               │              │
    ┌──────────▼──┐  ┌────────▼────┐
    │  API Inst 1  │  │  API Inst 2  │    ← Stateless .NET 9
    │  (Port 5023) │  │  (Port 5024) │
    └──────┬───────┘  └──────┬───────┘
           │                 │
    ┌──────▼─────────────────▼──────┐
    │        MediatR Pipeline       │    ← CQRS Commands/Queries
    │   Commands → Handlers → DB    │
    │   Queries  → Cache → DB       │
    └──────┬──────────────┬─────────┘
           │              │
    ┌──────▼──────┐ ┌─────▼───────┐
    │  Redis Cache │ │ Shard Router │   ← SHA256(code) % N
    │  (Port 6379) │ │             │
    └─────────────┘ └──┬──────┬───┘
                       │      │
              ┌────────▼┐ ┌───▼───────┐
              │ Shard 1  │ │  Shard 2   │  ← PostgreSQL
              │ (PG:5432)│ │ (PG:5433)  │
              └──────────┘ └───────────┘
```

### Project Structure

```
url-shortener/
├── src/
│   ├── UrlShortener.Api/           # Controllers, Middleware, Program.cs
│   ├── UrlShortener.Application/   # Commands, Queries, Interfaces, Services
│   ├── UrlShortener.Domain/        # Entities (ShortUrl, ClickEvent)
│   └── UrlShortener.Infrastructure/# EF Core, ShardRouter, Redis, Analytics
├── tests/
│   └── UrlShortener.Tests/         # xUnit + Moq (16 tests)
├── docker-compose.yml              # Multi-container orchestration
├── load-test.js                    # k6 load test script
├── chanuka-nimsara-month1-system-design.md
├── chanuka-nimsara-month1-sharding.md
└── chanuka-nimsara-month1-loadtest.md
```

---

## Key Architectural Decisions

### ADR-1: SHA256 over GetHashCode() for Shard Routing
- **Context**: .NET Core randomizes `string.GetHashCode()` per process (security feature)
- **Decision**: Use `SHA256.HashData()` for deterministic hash routing
- **Consequence**: Same shortCode always routes to same shard, even across restarts. Prevents data loss.

### ADR-2: Cache-Aside over Write-Through
- **Context**: URL shorteners are read-heavy (100:1 ratio)
- **Decision**: Check cache first, populate on miss (cache-aside pattern)
- **Consequence**: Simple implementation, graceful degradation if Redis fails, 80%+ hit rate

### ADR-3: Fire-and-Forget Analytics
- **Context**: Click tracking should not slow down redirects
- **Decision**: `Task.Run` with try-catch and ILogger
- **Consequence**: Non-blocking redirects, small risk of lost click events (logged, not silently dropped)

### ADR-4: Random.Shared for Code Generation
- **Context**: `new Random()` is not thread-safe in concurrent scenarios
- **Decision**: Use `Random.Shared` (thread-safe, lock-free)
- **Consequence**: No duplicate codes under concurrent URL creation

### ADR-5: Clean Architecture + CQRS
- **Context**: Need separation between business logic and infrastructure (sharding, caching)
- **Decision**: MediatR-based CQRS with Clean Architecture layers
- **Consequence**: Shard routing is transparent to business logic, easy to test with mocks

---

## Verification Results

### Unit Tests: 16/16 passed
- ShardRouter determinism (5 tests)
- ShortCodeGenerator thread-safety (5 tests)
- CreateShortUrlCommandHandler logic (2 tests)
- GetUrlStatsQueryHandler logic (2 tests)
- Thread safety + collision handling (2 tests)

### k6 Load Test: All 3 scenarios passed
- Baseline (10 VUs, 1m): ✓
- Sustained (50 VUs, 3m): ✓
- Spike (200 VUs, 50s): ✓
- **Throughput**: 201.4 RPS | **Success Rate**: 100% | **P50**: 9.52ms

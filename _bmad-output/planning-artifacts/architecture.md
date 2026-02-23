---
stepsCompleted: [1, 2, 3]
inputDocuments: ['_bmad-output/planning-artifacts/prd.md', '_bmad-output/planning-artifacts/product-brief-Month1-2026-02-23.md', '_bmad-output/project-context-url-shortener.md']
workflowType: 'architecture'
project_name: 'Month1'
user_name: 'Chanuka'
date: '2026-02-23'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
- **URL Shortening & Redirection**: Core service to map long URLs to unique short codes with high performance.
- **Distributed Persistence**: Use of sharded PostgreSQL clusters to handle 91GB+ of data over 5 years.
- **High-Speed Caching**: Redis-based cache-aside layer to intercept "hot" URL lookups and reduce DB load.
- **Analytics Tracking**: Atomic increments for redirect counts and metadata storage for click tracking.

**Non-Functional Requirements:**
- **Extreme Throughput**: Target of 10M+ requests/day (~115 RPS average, 500+ peak).
- **Sub-100ms Latency (P95)**: Critical for user experience during redirects.
- **99.9% Availability**: Requires stateless app servers and redundant DB shards.
- **Scalability**: Must demonstrate a clear path for horizontal growth through database sharding.

**Scale & Complexity:**
- **Primary domain**: Distributed Systems / API Backend
- **Complexity level**: **High**
- **Estimated architectural components**: 5 (LB, Stateless Apps, Redis, Shard Manager, PostgreSQL Shards)

### Technical Constraints & Dependencies
- **Stack**: .NET 8, PostgreSQL (Sharded), Redis, Docker Compose.
- **Validation**: Must be verified using **k6** load profiles (Sustained, Spike, and Ramp-up).

### Cross-Cutting Concerns Identified
- **Consistent Hashing**: Ensuring uniform data distribution across PostgreSQL shards.
- **Cache Invalidation**: Managing the lifecycle of short-to-long URL mappings in Redis.
- **Shard Management**: Handling failover and potential rebalancing of shards.

---

## Starter Template Evaluation

### Primary Technology Domain

**API / Backend Service** based on project requirements analysis (.NET 8 core).

### Selected Starter: .NET 8 Clean Architecture Foundation

**Rationale for Selection:**
The **Clean Architecture** approach provides the necessary decoupling between business logic and the distributed infrastructure (PostgreSQL sharding, Redis caching). It enables independent development of the sharding logic while maintaining a robust Web API surface.

**Initialization Command:**

```bash
dotnet new webapi -n UrlShortener.Api
# Structure: Api -> Application -> Domain -> Infrastructure
```

### Architectural Decisions Provided by Foundation:

- **Language & Runtime**: C# 12 / .NET 8 (Modern productivity features).
- **Code Organization**: Core logic in `Domain`/`Application` with sharding implementations in `Infrastructure`.
- **Testing Framework**: xUnit for unit tests; k6 for external load/stress testing.
- **Containerization**: Multi-service Docker Compose for orchestrating redundant shards and caching nodes.

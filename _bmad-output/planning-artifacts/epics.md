---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: ['_bmad-output/planning-artifacts/prd.md', '_bmad-output/planning-artifacts/architecture.md', '_bmad-output/planning-artifacts/product-brief-Month1-2026-02-23.md', '_bmad-output/project-context-url-shortener.md']
---

# Month1 - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Month1, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: URL Shortening - Generate unique short codes for long URLs.
FR2: Redirection - Map short codes back to long URLs with high performance.
FR3: Distributed Persistence - Store data across multiple PostgreSQL shards.
FR4: Analytics Tracking - Capture redirect counts and basic metadata.
FR5: Management API - Create, read, and delete short URL mappings.

### NonFunctional Requirements

NFR1: High Throughput - 10M+ requests per day (~115 RPS average).
NFR2: Low Latency - P95 response time under 100ms for redirects.
NFR3: High Availability - 99.9% uptime with redundant nodes.
NFR4: Scalability - Horizontal scaling via database sharding and stateless app nodes.

### Additional Requirements

- **Starter Template**: .NET 8 Clean Architecture Foundation.
- **Infrastructure**: Multi-service Docker Compose (LB, 2x App nodes, 1x Redis, 2x PostgreSQL Shards).
- **Load Testing**: k6 validation scripts for sustained, spike, and ramp-up profiles.

### FR Coverage Map

FR1: Epic 1 - Story 1.3 (Create)
FR2: Epic 1 - Story 1.4 (Redirect)
FR3: Epic 3 - Story 3.1, 3.2 (Sharding)
FR4: Epic 2 - Story 2.1, 2.2, 2.3 (Analytics)
FR5: Epic 1 - Story 1.3, 1.4, 1.5 (CRUD)

## Epic List

### Epic 1: URL Redirection Foundation (MVP)
Establish the core URL shortening and redirection service using the .NET 8 Clean Architecture foundation with single-shard persistence.
**FRs covered:** FR1, FR2, FR5

### Epic 2: Real-time Analytics & Click Tracking
Implement atomic click tracking and metadata capture to provide usage insights without impacting redirect performance.
**FRs covered:** FR4

### Epic 3: Distributed Scalability with Database Sharding
Implement the logic and infrastructure to partition data across multiple PostgreSQL shards for target scale.
**FRs covered:** FR3

### Epic 4: Performance Caching & Latency Optimization
Integrate Redis as a cache-aside layer to ensure sub-100ms latency for "hot" redirects under heavy load.
**FRs covered:** NFR1, NFR2

### Epic 5: Production Readiness & System Verification
Finalize Docker Compose orchestration and execute k6 load tests to verify the 10M+ requests/day target.

## Epic 1: URL Redirection Foundation (MVP)

Establish the core URL shortening and redirection service using the .NET 8 Clean Architecture foundation with single-shard persistence.

### Story 1.1: Project Scaffolding
As a developer,
I want to initialize the .NET 8 Clean Architecture solution,
So that the project layers are properly structured for separation of concerns.

**Acceptance Criteria:**

**Given** a fresh development environment
**When** the project is initialized
**Then** the solution includes `Api`, `Application`, `Domain`, and `Infrastructure` projects
**And** `Docker Compose` is configured with a single PostgreSQL instance and the Api service.

### Story 1.2: URL Persistence Layer
As a system,
I want to store URL mappings in PostgreSQL,
So that they can be retrieved for redirection at any time.

**Acceptance Criteria:**

**Given** the database schema is being designed
**When** the `ShortUrl` entity is created in the Domain layer
**Then** EF Core configuration handles the mapping to a `ShortUrls` table
**And** migrations are applied automatically on startup to ensure table existence.

### Story 1.3: URL Creation API
As a user,
I want to create a short code for my long URL,
So that I can share it easily.

**Acceptance Criteria:**

**Given** a long destination URL
**When** I POST to `/api/urls`
**Then** the system returns a unique short code
**And** the system prevents duplicate short codes via collision handling or unique constraints.

### Story 1.4: Redirection Logic
As a user,
I want to be redirected to the original destination when I use a short link,
So that I can reach the intended content quickly.

**Acceptance Criteria:**

**Given** a valid short code
**When** I GET `/{code}`
**Then** the system performs a 302 redirect to the destination URL
**And** a 404 is returned if the code does not exist in the system.

### Story 1.5: URL Management API (Delete)
As a user,
I want to delete a short URL mapping,
So that I can stop redirection for a link that is no longer needed.

**Acceptance Criteria:**

**Given** an existing short code
**When** I DELETE to `/api/urls/{code}`
**Then** the record is removed from the database
**And** subsequent requests for that code return a 404.

## Epic 2: Real-time Analytics & Click Tracking

Implement atomic click tracking and metadata capture to provide usage insights without impacting redirect performance.

### Story 2.1: Click Tracking Decorator
As a developer,
I want to capture click events automatically when a redirect occurs,
So that I can maintain a centralized tracking logic across all redirection endpoints.

**Acceptance Criteria:**

**Given** a redirection request
**When** the redirect is successfully processed
**Then** an `AnalyticsFilter` or Middleware intercepts the call
**And** basic request metadata (IP, User Agent, Referer) is extracted for processing.

### Story 2.2: Atomic Redirect Counters (Redis)
As a system,
I want to increment the total click count for a short URL atomically in Redis,
So that the counter remains accurate under high concurrency during traffic spikes.

**Acceptance Criteria:**

**Given** extracted click metadata
**When** the click event is processed
**Then** the Redis `INCR` command is used to update the total click count for the specific short code
**And** counters are periodically synced back to PostgreSQL via a background worker.

### Story 2.3: Detailed Click Metadata Persistence
As a user,
I want to store detailed visit metadata in PostgreSQL,
So that I can analyze my link performance and traffic sources over time.

**Acceptance Criteria:**

**Given** extracted request metadata
**When** the click event is recorded
**Then** a new record is created in the `ClickEvent` table in PostgreSQL
**And** metadata is asynchronously persisted to ensure it doesn't block the P95 redirect latency.

## Epic 3: Distributed Scalability with Database Sharding

Implement the logic and infrastructure to partition data across multiple PostgreSQL shards for target scale.

### Story 3.1: Shard Connection Infrastructure
As a developer,
I want to manage multiple PostgreSQL connections in the Infrastructure layer,
So that the system can communicate with different shards dynamically.

**Acceptance Criteria:**

**Given** multiple PostgreSQL shard instances
**When** the infrastructure layer is configured
**Then** the system supports an array of connection strings
**And** a `ShardConnectionFactory` is implemented to resolve DB connections based on a provided shard index.

### Story 3.2: Hash-based Shard Router
As a system,
I want to determine the target shard for a URL based on a hash of its short code,
So that data is distributed uniformly across the cluster.

**Acceptance Criteria:**

**Given** a short code (for Create or Redirect)
**When** the shard routing logic is invoked
**Then** a consistent hashing algorithm determines the target shard index
**And** all subsequent Read/Write operations for that mapping are routed to the identified shard.

### Story 3.3: Multi-Shard Docker Orchestration
As a developer,
I want to run multiple PostgreSQL instances in Docker,
So that I can simulate and test a sharded environment locally.

**Acceptance Criteria:**

**Given** a local development environment
**When** `docker-compose up` is executed
**Then** the environment includes `postgres-shard-1` and `postgres-shard-2` services
**And** health checks ensure all shards are ready before the App service begins accepting traffic.

## Epic 4: Performance Caching & Latency Optimization

Integrate Redis as a cache-aside layer to ensure sub-100ms latency for "hot" redirects under heavy load.

### Story 4.1: Cache-Aside Implementation
As a system,
I want to check Redis before querying PostgreSQL during a redirect,
So that I can reduce database load and latency for popular links.

**Acceptance Criteria:**

**Given** a redirection request
**When** the redirection service processes the request
**Then** it first attempts to fetch the destination URL from Redis based on the short code
**And** on a cache miss, it fetches data from the appropriate PostgreSQL shard and then stores it in Redis for future requests.

### Story 4.2: Cache Invalidation & TTL Management
As a system,
I want to manage the lifetime of cached URLs,
So that the cache remains relevant and doesn't consume excessive memory over time.

**Acceptance Criteria:**

**Given** a URL mapping is cached in Redis
**When** the entry is created or a URL is deleted
**Then** a default TTL (e.g., 24 hours) is applied to the Redis entry
**And** if a URL is deleted via the API (FR5), the corresponding Redis entry is immediately invalidated.

### Story 4.3: Redis Performance Configuration
As a developer,
I want to optimize the Redis client configuration,
So that the cache layer can handle 500+ peak requests per second efficiently.

**Acceptance Criteria:**

**Given** the application's Redis integration
**When** the system is under load
**Then** `StackExchange.Redis` is used with a singleton `ConnectionMultiplexer` for efficiency
**And** timeouts and retry policies are configured to prevent cascading failures if Redis experiences temporary latency.

## Epic 5: Production Readiness & System Verification

Finalize Docker Compose orchestration and execute k6 load tests to verify the 10M+ requests/day target.

### Story 5.1: Load Balancer Configuration (Nginx)
As a developer,
I want to route traffic across multiple app instances using Nginx,
So that the system remains available if a single instance fails.

**Acceptance Criteria:**

**Given** multiple running app containers
**When** Nginx is started via Docker Compose
**Then** it acts as the primary entry point for all API traffic
**And** it load-balances requests across `app-instance-1` and `app-instance-2` using a round-robin or least-conn policy.

### Story 5.2: k6 Load Profile: Sustained & Spike
As a tester,
I want to execute a k6 script that simulates 10M+ requests/day,
So that I can verify the system meets the scalability target.

**Acceptance Criteria:**

**Given** the full sharded and cached environment is running
**When** the k6 script is executed
**Then** the system maintains a sustained load of ~115 RPS without errors
**And** it successfully handles a spike profile reaching 500+ RPS while maintaining sub-100ms P95 latency.

### Story 5.3: Health Monitoring & Logging
As an operator,
I want to monitor the health of all system components,
So that I can quickly identify and resolve performance bottlenecks.

**Acceptance Criteria:**

**Given** the running system
**When** the `/health` endpoint is called
**Then** it returns the status of PostgreSQL shards, Redis, and internal services
**And** container logs are aggregated or indexed to show request distribution and latency across the cluster.

---
stepsCompleted: ['step-01-init', 'step-02-discovery', 'step-02b-vision', 'step-03-requirements', 'step-04-ux', 'step-05-constraints', 'step-06-final']
inputDocuments: ['_bmad-output/planning-artifacts/product-brief-Month1-2026-02-23.md', '_bmad-output/project-context-url-shortener.md']
workflowType: 'prd'
classification:
  projectType: api_backend
  domain: distributed_systems
  complexity: high
  projectContext: greenfield
documentCounts:
  briefCount: 1
  researchCount: 0
  brainstormingCount: 0
  projectDocsCount: 1
---

# Product Requirements Document - Month1

**Author:** Chanuka  
**Date:** 2026-02-24  
**Version:** 2.0

---

## 1. Vision & Goals

### Product Vision
Build a production-grade URL shortening service that demonstrates horizontal scalability, database sharding, and multi-layer caching — handling 10M+ redirects per day with sub-100ms P95 latency.

### Success Metrics

| Metric | Target |
|:---|:---|
| Redirect throughput | 200+ RPS sustained |
| P95 latency (redirects) | < 100ms with Redis |
| Availability | 99.9% |
| Error rate under load | < 0.1% |
| Data retention | 5 years |

---

## 2. Functional Requirements

### FR-1: URL Shortening
- **Input**: Original URL (validated)
- **Output**: 7-character Base62 short code
- **Endpoint**: `POST /api/shorten`
- **Collision handling**: Retry up to 5 times with new random code

### FR-2: URL Redirection
- **Input**: Short code
- **Output**: HTTP 302 redirect to original URL
- **Endpoint**: `GET /{shortCode}`
- **Performance**: Cache-aside pattern (check Redis → DB on miss → populate cache)

### FR-3: Click Analytics
- **Tracking**: IP address, user-agent, referer, timestamp per click
- **Counters**: Atomic Redis increment for real-time counts
- **Persistence**: Fire-and-forget to database (non-blocking)

### FR-4: Statistics API
- **Input**: Short code
- **Output**: Click count, original URL, creation date
- **Endpoint**: `GET /api/stats/{shortCode}`

### FR-5: URL Deletion
- **Input**: Short code
- **Output**: 204 No Content / 404 Not Found
- **Endpoint**: `DELETE /api/urls/{shortCode}`
- **Side effect**: Invalidate Redis cache entry

### FR-6: Health Check
- **Endpoint**: `GET /health`
- **Response**: System health status

---

## 3. Non-Functional Requirements

### NFR-1: Performance
- 10M+ requests/day (~115 RPS average, 230 peak)
- P95 latency < 100ms for redirects (with Redis)
- P50 latency < 10ms (cache hit path)

### NFR-2: Scalability
- Stateless app servers (horizontal scaling via Nginx)
- Hash-based PostgreSQL sharding (add shards for data growth)
- Redis cache-aside (80%+ hit rate for hot URLs)

### NFR-3: Reliability
- Graceful degradation: Redis → DB → SQLite fallback
- Fire-and-forget analytics with error logging (no silent failures)
- Shard isolation (one shard down ≠ full outage)

### NFR-4: Security
- Input validation on all URLs
- No PII stored beyond IP addresses (analytics)
- Rate limiting configurable via Nginx

---

## 4. Technical Constraints

| Constraint | Value |
|:---|:---|
| Runtime | .NET 9 |
| Primary database | PostgreSQL (sharded) |
| Cache | Redis |
| Architecture | Clean Architecture + CQRS (MediatR) |
| Containerization | Docker Compose |
| Load testing | Grafana k6 |
| Testing framework | xUnit + Moq |

---

## 5. API Specification

| Method | Endpoint | Description | Response |
|:---|:---|:---|:---|
| POST | `/api/shorten` | Create short URL | `{ shortCode }` |
| GET | `/{shortCode}` | Redirect | 302 |
| GET | `/api/stats/{shortCode}` | Click stats | `{ shortCode, originalUrl, totalClicks, createdAt }` |
| DELETE | `/api/urls/{shortCode}` | Delete URL | 204 / 404 |
| GET | `/health` | Health check | 200 |

---

## 6. Acceptance Criteria

- [ ] All 4 API endpoints return correct responses
- [ ] Short codes are unique 7-char Base62 strings
- [ ] Redirect correctly tracks click analytics
- [ ] Cache-aside pattern reduces DB load (measurable via k6)
- [ ] Sharding correctly routes shortCode to deterministic shard
- [ ] k6 load test passes: 100% success rate at 200 VUs
- [ ] 16+ unit tests pass covering core logic
- [ ] System runs in standalone mode without PostgreSQL/Redis

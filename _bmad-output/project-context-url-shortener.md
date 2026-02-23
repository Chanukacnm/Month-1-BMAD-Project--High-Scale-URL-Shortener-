# URL Shortener Challenge - Project Context

## Learning Objectives
Participants will learn to:

- Design systems handling massive scale (10M+ requests/day)
- Implement horizontal scaling
- Apply sharding patterns
- Make trade‑off decisions
- Create & present load test results

## Session Agenda (Hour-by-hour)

1. Design principles → scalability, CAP, case study
2. Sharding deep dive → strategies, trade‑offs, edge cases
3. Implementation → build URL shortener prototype
4. Testing & presentations → load tests, benchmarks, demos

## System Design Process

1. Clarify requirements
2. Estimate scale
3. Design high-level architecture
4. Deep dive on components
5. Address bottlenecks

## Capacity Estimation
For 10M DAU:

- Reads/day: 10M
- Writes/day: 100K (100:1 read/write)
- Peak QPS: ~230
- Storage (5 years): ~91GB
- Cache size: ~1GB Redis for hot URLs
- Short code space: 62⁷ ≈ 3.5 trillion combinations

### Detailed Calculations

- Storage (5 years): 100K x 500B x 365 x 5 = 91 GB
- Bandwidth: 230 x 500B = 115 KB/s
- Cache size (20% hot): 10M x 0.2 x 500B = 1 GB Redis
- URL length: ~7 chars
- 62^7 = 3.5 trillion

## High‑Level Architecture

- Stateless app servers behind load balancer
- Redis cache (cache‑aside)
- Sharded PostgreSQL clusters using hash‑based sharding
- Read path: Client → LB → App → Cache → DB
- Write path: Client → LB → App → DB → Cache invalidate

## Scalability Patterns

- Horizontal scaling: add more machines (preferred)
- Vertical scaling: limited improvement
- Sharding: essential for DB scaling
- Caching: high impact for reducing load

## CAP Theorem Applied

Defines trade‑offs between Consistency, Availability, Partition tolerance. Real systems choose 2. URL shortener chooses AP (availability prioritized; eventual consistency accepted).

## Database Sharding Strategies

Covers:
- Hash-based
- Range
- Geo
- Directory

Chosen approach: Hash-based + MD5 hash mod N (uniform distribution). Consistent hashing explained for easier shard changes.

## BMAD Method & Workflow

BMAD = AI-driven development workflow using slash commands such as:
- /product-brief
- /create-prd
- /create-architecture
- /dev-story
- /code-review

Produces:
- PRD
- Architecture doc
- Epics & stories
- Implementation code

## BMAD Roadmap

5-step AI-directed workflow:
1. Product brief
2. PRD
3. Architecture
4. Stories
5. Build & review

**Challenge Statement:** "Build a high-scale URL shortener: 10M+ req/day, .NET 8, PostgreSQL sharding, Redis caching, k6 load tests"

---

# Challenge Overview

Design and implement a URL shortening service capable of handling 10M+ requests per day with database sharding and horizontal scaling.

**Time Allocation:** 3 hours (during session)  
**Difficulty:** Advanced

## Business Requirements

### Functional Requirements

- Shorten long URLs to unique short codes
- Redirect short URLs to original destinations
- Track click analytics (count, timestamp, referrer)
- Support custom aliases (optional premium feature)
- URL expiration support

### Non-Functional Requirements

- 10M+ redirects per day (~115 requests/second average, 500+ peak)
- 99.9% availability
- < 100ms redirect latency (P95)
- Data retention: 5 years
- Global distribution ready

## Technical Constraints

- Use PostgreSQL as primary datastore (sharded)
- Implement Redis caching layer
- Provide load balancing strategy
- Document all architectural decisions

## Deliverables

### 1. System Design Document (25 points)

**File:** `{your-name}-month1-system-design.md`

**Required Sections:**

1. **Requirements Analysis**
   - Capacity estimation
   - Traffic patterns
   - Storage calculations

2. **High-Level Architecture**
   - Component diagram
   - Data flow description
   - Technology choices with rationale

3. **Database Design**
   - Schema design
   - Sharding strategy
   - Index strategy

4. **Caching Strategy**
   - What to cache
   - Cache invalidation approach
   - Cache sizing

5. **Trade-off Analysis**
   - CAP theorem decisions
   - Consistency vs availability choices
   - Cost vs performance trade-offs

6. **Scaling Strategy**
   - Horizontal scaling approach
   - Bottleneck identification
   - Future growth plan

**Evaluation Criteria:**
- Capacity estimation accuracy (5 pts)
- Architecture completeness (5 pts)
- Trade-off documentation (5 pts)
- Sharding strategy clarity (5 pts)
- Scaling plan viability (5 pts)

### 2. Working Prototype (25 points)

**Repository Structure:**

```
url-shortener/
├── src/
│   ├── api/           # REST endpoints
│   ├── services/      # Business logic
│   ├── db/            # Database layer with sharding
│   └── cache/         # Redis integration
├── docker-compose.yml # Multi-container setup
├── README.md          # Setup instructions
└── tests/             # Unit tests
```

**Required API Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/shorten | Create short URL |
| GET | /:shortCode | Redirect to original |
| GET | /api/stats/:shortCode | Get click statistics |
| DELETE | /api/urls/:shortCode | Delete URL |

**Minimum Functionality:**
- [ ] URL shortening with unique code generation
- [ ] Redirect with click tracking
- [ ] Basic analytics endpoint
- [ ] Cache integration for hot URLs
- [ ] Error handling

**Evaluation Criteria:**
- Code quality and organization (5 pts)
- Sharding implementation (5 pts)
- Cache integration (5 pts)
- API completeness (5 pts)
- Error handling (5 pts)

### 3. Database Sharding Implementation (25 points)

**File:** `{your-name}-month1-sharding.md`

**Required Content:**

1. **Sharding Strategy**
   - Algorithm chosen (hash/range/directory)
   - Shard key selection rationale
   - Number of shards and sizing

2. **Implementation Details**
   - Connection routing logic
   - Cross-shard query handling
   - Transaction considerations

3. **Schema Per Shard**
   - [Include DDL statements]

4. **Shard Management**
   - Adding new shards
   - Rebalancing procedure
   - Failure handling

**Code Requirements:**
- Shard routing logic
- Connection pool per shard
- Query router implementation

**Evaluation Criteria:**
- Strategy appropriateness (5 pts)
- Implementation correctness (5 pts)
- Rebalancing plan (5 pts)
- Failure handling (5 pts)
- Documentation quality (5 pts)

### 4. Load Test Results (25 points)

**File:** `{your-name}-month1-loadtest.md`

**Required Tests:**

| Test | Duration | Load | Target |
|------|----------|------|--------|
| Baseline | 1 min | 10 rps | Establish baseline |
| Ramp-up | 5 min | 10→200 rps | Find breaking point |
| Sustained | 5 min | 150 rps | Stability test |
| Spike | 2 min | 500 rps spike | Recovery test |

**Report Format:**

1. **Test Environment**
   - Hardware specs
   - Database configuration
   - Cache configuration

2. **Results Summary**

| Metric | Baseline | Target | Achieved |
|--------|----------|--------|----------|
| Throughput | - | 150 rps | ? |
| P50 Latency | - | <50ms | ? |
| P95 Latency | - | <100ms | ? |
| P99 Latency | - | <200ms | ? |
| Error Rate | - | <0.1% | ? |

3. **Detailed Results**
   - [Include graphs/charts]

4. **Bottleneck Analysis**
   - Identified bottlenecks
   - Resource utilization
   - Recommendations

5. **Optimization Applied**
   - Changes made during testing
   - Before/after comparison

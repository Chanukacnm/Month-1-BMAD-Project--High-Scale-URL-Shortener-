# Database Sharding Implementation

**Name:** Chanuka Nimsara  
**Date:** 2026-02-24

---

## Sharding Strategy

### Algorithm Chosen: Hash-Based

```
hash(short_code) % N → shard index
```

We use **hash-based sharding** (same approach as the session material). Data is distributed by computing a deterministic hash of the shard key and taking modulo N (number of shards).

**Why hash-based over alternatives?**

| Strategy | Pros | Cons | Verdict |
|:---|:---|:---|:---|
| **Hash-based** ✅ | Even distribution, O(1) routing, no lookup table | Harder to add shards (requires redistribution) | **Best fit** — uniform, key-based access patterns |
| **Range-based** | Easy to add shards (split ranges) | Hot spots if keys are random (ours are) | ❌ Random Base62 codes defeat range locality |
| **Geographic** | Regional data locality | Complex cross-region queries | ❌ Not needed for URL shortener |
| **Directory-based** | Maximum flexibility | SPOF on lookup table, extra network hop | ❌ Over-engineered for our use case |

### Shard Key Selection: `ShortCode`

| Factor | Rationale |
|:---|:---|
| **Present in all operations** | Every redirect, create, delete, and stats lookup includes ShortCode |
| **Uniform distribution** | Randomly generated Base62 codes produce even hash distribution |
| **No cross-shard queries** | All data for a URL (mapping + click events) lives on the same shard |
| **Immutable** | ShortCodes never change — no re-sharding of individual records needed |

### Number of Shards and Sizing

| Config | Value | Rationale |
|:---|:---|:---|
| **Initial shards** | 2 | Sufficient for 91 GB over 5 years (~45.5 GB per shard) |
| **Expandable to** | N | Hash-mod routing supports any shard count |
| **Per-shard capacity** | ~50 GB data + indexes | Well within PostgreSQL's comfortable range |

---

## Implementation Details

### Connection Routing Logic

```csharp
public class ShardRouter : IShardRouter
{
    private readonly int _shardCount;

    public int GetShardIndex(string shortCode)
    {
        if (string.IsNullOrEmpty(shortCode))
            return 0;

        // SHA256 hash for deterministic, uniform distribution
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(shortCode));
        var hash = Math.Abs(BitConverter.ToInt32(bytes, 0));
        return hash % _shardCount;
    }
}
```

> **Note:** The session slides show MD5 as the hash function. We chose SHA256 for stronger collision resistance, though both produce uniform distribution. The critical requirement is **determinism** — the same ShortCode must always route to the same shard, even across application restarts. (.NET's `string.GetHashCode()` is randomized per process and would cause data loss.)

### Connection Pool Per Shard

```csharp
public class ShardConnectionFactory : IShardConnectionFactory
{
    private readonly DbContextOptions[] _shardOptions;

    public int ShardCount => _shardOptions.Length;

    public ApplicationDbContext CreateDbContext(int shardIndex)
    {
        return new ApplicationDbContext(_shardOptions[shardIndex]);
    }
}
```

Configuration:
```json
{
  "ConnectionStrings": {
    "Shard1": "Host=postgres-shard-1;Database=UrlShortenerDb;...",
    "Shard2": "Host=postgres-shard-2;Database=UrlShortenerDb;..."
  }
}
```

Each shard has its own connection string and connection pool. The factory dynamically discovers shards from configuration at startup (`Shard1`, `Shard2`, ..., `ShardN`).

### Query Router Implementation

All queries are routed through MediatR handlers that use the shard router:

```csharp
// Redirect: GET /{shortCode}
public async Task<string?> Handle(GetOriginalUrlQuery query, CancellationToken ct)
{
    var shardIndex = _router.GetShardIndex(query.ShortCode);
    using var context = _factory.CreateDbContext(shardIndex);
    
    var url = await context.ShortUrls
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.ShortCode == query.ShortCode, ct);
    
    return url?.OriginalUrl;
}
```

### Cross-Shard Query Handling

Most operations are **single-shard** (routed by ShortCode). The only cross-shard operation is global statistics:

| Query Type | Strategy |
|:---|:---|
| Single URL lookup/stats | Route to correct shard via `GetShardIndex` |
| Global URL count | Fan-out: query all shards in parallel, sum results |
| Search by OriginalUrl | Fan-out: query all shards (rare operation) |

### Transaction Considerations

- **No cross-shard transactions** — each URL and its click events live on the same shard
- **Per-shard ACID** — PostgreSQL provides full ACID guarantees within each shard
- **Collision handling** — if `INSERT` fails due to duplicate ShortCode, retry with a new code (up to 5 times)

---

## Schema Per Shard

Each PostgreSQL shard receives identical DDL via EF Core migrations:

```sql
-- Table 1: URL mappings
CREATE TABLE "ShortUrls" (
    "Id"          UUID NOT NULL,
    "ShortCode"   VARCHAR(10) NOT NULL,
    "OriginalUrl" TEXT NOT NULL,
    "CreatedAt"   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_ShortUrls" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_ShortUrls_ShortCode"
    ON "ShortUrls" ("ShortCode");

-- Table 2: Click analytics (same shard as parent URL)
CREATE TABLE "ClickEvents" (
    "Id"         UUID NOT NULL,
    "ShortCode"  VARCHAR(10) NOT NULL,
    "IpAddress"  VARCHAR(45),
    "UserAgent"  TEXT,
    "Referer"    TEXT,
    "OccurredAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_ClickEvents" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_ClickEvents_ShortCode"
    ON "ClickEvents" ("ShortCode");

CREATE INDEX "IX_ClickEvents_OccurredAt"
    ON "ClickEvents" ("OccurredAt");
```

**Design decisions:**
- No foreign key from ClickEvents → ShortUrls to avoid cross-entity locking during high write throughput
- UUID primary keys avoid auto-increment coordination across shards
- `OccurredAt` index enables time-range analytics queries

---

## Shard Management

### Adding New Shards

When scaling from N to N+1 shards, `hash % N` → `hash % (N+1)` changes the mapping for approximately `1/(N+1)` of all records.

**Procedure:**

1. **Provision**: Deploy new PostgreSQL instance, apply EF Core migrations
2. **Update config**: Add `Shard3` connection string to `appsettings.json`
3. **Dual-write mode**: Deploy code that writes to both old and new shard locations
4. **Migrate data**: For each record on existing shards, recalculate `SHA256(ShortCode) % 3`. If the new shard index differs from the current shard, copy the record to the correct shard
5. **Verify**: Integrity check — each ShortCode exists on exactly one shard
6. **Switch**: Update shard count, remove dual-write mode
7. **Clean up**: Delete migrated records from source shards

### Rebalancing Procedure

| Scaling | Records to Move | Percentage |
|:---|:---|:---|
| 2 → 3 shards | ~33% of all records | ~30M at 5-year scale |
| 3 → 4 shards | ~25% of all records | ~22.5M |
| 4 → 5 shards | ~20% of all records | ~18M |

**Consistent hashing** (as mentioned in the session) would minimize data movement to ~`K/N` (K = total keys) when adding a node with virtual nodes on a hash ring. This is used by DynamoDB and Cassandra. For our planned (not auto-scaling) shard counts, hash-mod is acceptable.

### Failure Handling

| Scenario | Behavior | Mitigation |
|:---|:---|:---|
| **Single shard down** | URLs on that shard return 500; other shards unaffected | Health checks + alerting; cached redirects still work for hot URLs |
| **Redis down** | All requests fall through to DB | Graceful degradation: cache returns null, app queries shard directly |
| **All shards down** | SQLite standalone fallback (dev/test only) | Production: return 503 with Retry-After header |

**Recommended enhancements:**
- Circuit breaker (Polly): After 5 consecutive shard failures, open circuit for 30s
- Read replicas per shard: Redirect queries use replica, writes go to primary
- Connection pooling: pgBouncer in front of each shard for connection multiplexing

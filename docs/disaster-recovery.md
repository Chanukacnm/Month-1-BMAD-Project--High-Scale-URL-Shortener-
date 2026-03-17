# Disaster Recovery Procedure

## URL Shortener — Disaster Recovery Runbook

**Last Updated:** 2026-03-17  
**Author:** Chanuka Nimsara  
**System:** High-Scale URL Shortener (.NET 9 + PostgreSQL + Redis)

---

## 1. Recovery Targets

| Component | RTO (Recovery Time Objective) | RPO (Recovery Point Objective) |
|:---|:---|:---|
| Redis Cache | 5 minutes | 0 (cache is ephemeral, can be rebuilt) |
| PostgreSQL Shard (single) | 15 minutes | 1 hour (with hourly backups) |
| Full Cluster | 30 minutes | 1 hour |
| Nginx Load Balancer | 2 minutes | N/A (stateless) |
| API Instances | 2 minutes | N/A (stateless) |

---

## 2. Backup Procedures

### 2.1 PostgreSQL Shard Backup

Hourly automated backups using `pg_dump` per shard:

```bash
# Per-shard backup (run on each shard host)
pg_dump -U postgres -h localhost -p 5432 UrlShortenerDb \
  --format=custom \
  --file=/backups/shard-{N}-$(date +%Y%m%d-%H%M%S).dump

# Verify backup integrity
pg_restore --list /backups/shard-{N}-*.dump > /dev/null && echo "OK"
```

**Retention Policy:**
- Hourly backups: keep 24 hours
- Daily backups: keep 30 days
- Weekly backups: keep 12 weeks

### 2.2 Redis Backup

Redis uses RDB snapshots (configured in `redis.conf`):

```bash
# Trigger manual snapshot
redis-cli BGSAVE

# Snapshot location
/data/dump.rdb
```

> **Note:** Redis is used as a cache. Complete data loss is acceptable — the cache rebuilds automatically from the database on cache misses. No backup is strictly required, but snapshots reduce warm-up time.

### 2.3 Backup Automation Script

```bash
#!/bin/bash
# backup-all-shards.sh — Run via cron every hour

BACKUP_DIR="/backups/$(date +%Y%m%d)"
mkdir -p "$BACKUP_DIR"

for SHARD in 1 2; do
  CONTAINER="postgres-shard-${SHARD}"
  BACKUP_FILE="${BACKUP_DIR}/shard-${SHARD}-$(date +%H%M%S).dump"
  
  docker exec "$CONTAINER" pg_dump -U postgres UrlShortenerDb \
    --format=custom > "$BACKUP_FILE"
  
  echo "[$(date)] Shard ${SHARD} backed up to ${BACKUP_FILE}"
done

# Clean up backups older than 7 days
find /backups -name "*.dump" -mtime +7 -delete
```

---

## 3. Restore Procedures

### 3.1 Single Shard Restore

When one PostgreSQL shard fails and must be rebuilt:

```bash
# 1. Stop the failed shard
docker compose stop postgres-shard-{N}

# 2. Remove the old data volume
docker compose rm -v postgres-shard-{N}

# 3. Start a fresh shard container
docker compose up -d postgres-shard-{N}

# 4. Wait for it to be healthy
docker compose exec postgres-shard-{N} pg_isready -U postgres

# 5. Restore from latest backup
docker exec -i postgres-shard-{N} pg_restore \
  -U postgres -d UrlShortenerDb --clean --if-exists \
  < /backups/latest/shard-{N}.dump

# 6. Verify data
docker exec postgres-shard-{N} psql -U postgres -d UrlShortenerDb \
  -c "SELECT COUNT(*) FROM \"ShortUrls\";"
```

### 3.2 Full Cluster Restore

When the entire cluster must be rebuilt from scratch:

```bash
# 1. Bring down everything
docker compose down -v

# 2. Start infrastructure only
docker compose up -d postgres-shard-1 postgres-shard-2 redis

# 3. Wait for health checks
sleep 15

# 4. Restore each shard
for SHARD in 1 2; do
  docker exec -i postgres-shard-${SHARD} pg_restore \
    -U postgres -d UrlShortenerDb --clean --if-exists \
    < /backups/latest/shard-${SHARD}.dump
done

# 5. Start API instances
docker compose up -d url-shortener-api nginx

# 6. Verify health
curl http://localhost/health
```

### 3.3 Redis Restore (Optional)

```bash
# 1. Stop Redis
docker compose stop redis

# 2. Copy RDB snapshot
docker cp /backups/redis/dump.rdb redis:/data/dump.rdb

# 3. Start Redis
docker compose start redis
```

> Redis will auto-populate from DB on cache misses, so this step is optional and mainly speeds up warm-up.

---

## 4. Shard Migration / Rebalancing

When adding a new shard or rebalancing data across shards:

### 4.1 Adding a New Shard

```
⚠️ WARNING: Adding a shard changes the modulo divisor, which will cause
existing short codes to route to different shards. You must migrate data.
```

**Step-by-step:**

1. **Add the new shard** to `docker-compose.yml` and `appsettings.json`
2. **Put the system in read-only mode** (disable POST /api/shorten temporarily)
3. **Run the migration script:**

```bash
# Pseudocode: Re-route all existing URLs to their new shard
for each URL in all_shards:
    new_shard = SHA256(URL.ShortCode) & 0x7FFFFFFF % NEW_SHARD_COUNT
    if new_shard != current_shard:
        INSERT URL into new_shard
        DELETE URL from current_shard
```

4. **Update `ShardCount` configuration** and restart API instances
5. **Re-enable write operations**
6. **Invalidate Redis cache:** `redis-cli FLUSHDB`

### 4.2 Geographic Shard Migration

When moving URLs between geographic regions:

1. Export URLs from source region shard
2. Import to destination region shard
3. Update any region metadata
4. Flush Redis cache for affected short codes

---

## 5. Redis Failover and Reconnection

### Scenario: Redis Goes Down

**Impact:** Cache misses increase, all reads fall through to PostgreSQL.

**Automatic Recovery:** The application is designed to gracefully degrade:
- `RedisCacheService.GetAsync()` returns `null` when Redis is unavailable
- `RedisRateLimitService` falls back to in-memory `ConcurrentDictionary`
- No 5xx errors — just higher latency

**Manual Steps (if Redis doesn't auto-recover):**

```bash
# 1. Check Redis status
docker compose logs redis

# 2. Restart Redis
docker compose restart redis

# 3. Verify connectivity
docker exec redis redis-cli ping
# Expected: PONG
```

### Scenario: Redis Data Corruption

```bash
# 1. Stop Redis
docker compose stop redis

# 2. Remove corrupted data
docker compose rm -v redis

# 3. Start fresh (cache rebuilds automatically)
docker compose up -d redis
```

---

## 6. Common Failure Decision Trees

### 6.1 API Returns 5xx Errors

```
Is it affecting all requests?
├── YES → Check Nginx and API container health
│   ├── Nginx down → docker compose restart nginx
│   └── API containers crashing → docker compose logs url-shortener-api
│       ├── OOM → Increase memory limits / add replicas
│       └── Connection pool exhausted → Restart API + check DB connections
└── NO → Check if it affects specific short codes
    ├── YES → Likely a shard issue → Check route: SHA256(code) % 2
    │   └── Is that shard healthy?
    │       ├── NO → Restore shard (see §3.1)
    │       └── YES → Check application logs for that shard
    └── NO → Intermittent → Monitor, likely transient
```

### 6.2 High Latency (P95 > 500ms)

```
Is Redis available?
├── NO → All reads hitting DB → Restart Redis (see §5)
└── YES → Check cache hit ratio in Grafana
    ├── Low hit ratio → Cache may have been flushed → Wait for warm-up
    └── High hit ratio → Check DB shard performance
        ├── High connection count → Connection pool exhaustion → Restart APIs
        └── Slow queries → Check indexes on ShortUrls.ShortCode
```

### 6.3 Rate Limiting Not Working

```
Are X-RateLimit-* headers present in responses?
├── NO → RateLimitingMiddleware not registered → Check Program.cs
└── YES → Check values:
    ├── X-RateLimit-Remaining always high → Policy limits too generous
    └── Everyone being limited → Redis rate limit keys corrupted
        └── Fix: redis-cli KEYS "ratelimit:*" | xargs redis-cli DEL
```

---

## 7. Escalation Contacts

| Severity | Action | Contact |
|:---|:---|:---|
| P1 — Full outage | Immediate restore from backup | On-call engineer + team lead |
| P2 — Single shard down | Restore shard within RTO | On-call engineer |
| P3 — High latency | Monitor, restart Redis if needed | On-call engineer |
| P4 — Rate limiting issues | Adjust policy in appsettings.json | Any team member |

---

## 8. Post-Incident Checklist

After recovering from any incident:

- [ ] Verify all shards are serving data correctly
- [ ] Verify Redis cache is rebuilding (monitor cache hit ratio in Grafana)
- [ ] Verify rate limiting is functioning (check X-RateLimit headers)
- [ ] Run health checks on all endpoints
- [ ] Review logs for any remaining errors
- [ ] Update this document if new failure modes were discovered
- [ ] Schedule a post-mortem within 48 hours

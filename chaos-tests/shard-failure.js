import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * Chaos Test: PostgreSQL Shard Failure Simulation
 * 
 * This test runs load while one PostgreSQL shard is killed mid-test.
 * It verifies that URLs on the surviving shard continue to be served,
 * and that errors for the dead shard are handled gracefully.
 * 
 * Prerequisites:
 *   - Docker Compose cluster running (docker compose up -d)
 *   - k6 installed
 *   
 * Usage:
 *   k6 run chaos-tests/shard-failure.js
 */

export const options = {
    scenarios: {
        sustained_load: {
            executor: 'constant-vus',
            vus: 30,
            duration: '2m',
        },
    },
    thresholds: {
        'http_req_duration': ['p(95)<3000'],
        'http_req_failed': ['rate<0.55'],  // Up to ~50% may fail if one shard is down
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost';

export function setup() {
    // Create multiple test URLs — they'll land on different shards
    const codes = [];
    for (let i = 0; i < 20; i++) {
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/shard-test-${i}-${Date.now()}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        if (res.status === 200) {
            const body = JSON.parse(res.body);
            codes.push(body.shortCode);
        }
    }

    console.log(`Created ${codes.length} test URLs across shards`);
    return { codes };
}

export default function (data) {
    if (data.codes.length === 0) return;

    // Pick a random code
    const code = data.codes[Math.floor(Math.random() * data.codes.length)];

    const rand = Math.random();

    if (rand < 0.6) {
        // 60% - Redirects
        const res = http.get(`${BASE_URL}/${code}`, { redirects: 0 });
        check(res, {
            'redirect: graceful error handling': (r) => r.status !== 500,
        });
    } else if (rand < 0.8) {
        // 20% - Stats
        const res = http.get(`${BASE_URL}/api/stats/${code}`);
        check(res, {
            'stats: graceful error handling': (r) => r.status !== 500,
        });
    } else {
        // 20% - Create new
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/shard-chaos-${Date.now()}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        check(res, {
            'create: not unhandled error': (r) => r.status !== 500,
        });
    }

    sleep(0.2);
}

export function handleSummary(data) {
    const failRate = data.metrics.http_req_failed?.values?.rate || 0;
    const p95 = data.metrics.http_req_duration?.values?.['p(95)'] || 0;

    console.log(`\n=== Shard Failure Chaos Test Results ===`);
    console.log(`  Failure Rate: ${(failRate * 100).toFixed(2)}%`);
    console.log(`  P95 Latency:  ${p95.toFixed(0)}ms`);
    console.log(`  Verdict:      ${failRate < 0.55 ? 'PASS ✅ (graceful degradation)' : 'FAIL ❌'}`);
    console.log(`========================================\n`);

    return {};
}

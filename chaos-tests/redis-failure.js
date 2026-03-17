import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';

/**
 * Chaos Test: Redis Failure Simulation
 * 
 * This test runs sustained load against the URL shortener while Redis is
 * paused/unpaused mid-test. It verifies that the system degrades gracefully
 * (no 5xx flood) when Redis becomes unavailable.
 * 
 * Prerequisites:
 *   - Docker Compose cluster running (docker compose up -d)
 *   - k6 installed
 *   
 * Usage:
 *   k6 run chaos-tests/redis-failure.js
 *   
 * Note: Redis pause/unpause must be triggered externally (see run-chaos.ps1)
 */

export const options = {
    scenarios: {
        sustained_load: {
            executor: 'constant-vus',
            vus: 50,
            duration: '2m',
        },
    },
    thresholds: {
        'http_req_duration': ['p(95)<2000'],  // Relaxed during chaos
        'http_req_failed': ['rate<0.10'],      // Allow up to 10% failure during chaos
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost';

export function setup() {
    // Create a test URL to use for redirects
    const createRes = http.post(`${BASE_URL}/api/shorten`, 
        JSON.stringify({ originalUrl: 'https://example.com/chaos-redis-test' }),
        { headers: { 'Content-Type': 'application/json' } }
    );
    
    check(createRes, { 'setup: created test URL': (r) => r.status === 200 });
    
    const body = JSON.parse(createRes.body);
    return { shortCode: body.shortCode };
}

export default function(data) {
    // Mix of operations
    const rand = Math.random();
    
    if (rand < 0.3) {
        // 30% - Create new URLs
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/chaos-${Date.now()}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        check(res, {
            'create: status is 200 or 429': (r) => r.status === 200 || r.status === 429,
        });
    } else if (rand < 0.8) {
        // 50% - Redirect (cache-dependent)
        const res = http.get(`${BASE_URL}/${data.shortCode}`, { redirects: 0 });
        check(res, {
            'redirect: not 5xx': (r) => r.status < 500,
        });
    } else {
        // 20% - Stats
        const res = http.get(`${BASE_URL}/api/stats/${data.shortCode}`);
        check(res, {
            'stats: not 5xx': (r) => r.status < 500,
        });
    }
    
    sleep(0.1);
}

export function handleSummary(data) {
    const failRate = data.metrics.http_req_failed?.values?.rate || 0;
    const p95 = data.metrics.http_req_duration?.values?.['p(95)'] || 0;
    
    console.log(`\n=== Redis Failure Chaos Test Results ===`);
    console.log(`  Failure Rate: ${(failRate * 100).toFixed(2)}%`);
    console.log(`  P95 Latency:  ${p95.toFixed(0)}ms`);
    console.log(`  Verdict:      ${failRate < 0.10 ? 'PASS ✅' : 'FAIL ❌'}`);
    console.log(`========================================\n`);
    
    return {};
}

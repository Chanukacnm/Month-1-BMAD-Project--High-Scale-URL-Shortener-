import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * Chaos Test: Spike Load
 * 
 * Ramps from 0 → 1000 VUs in 10s, holds for 30s, then ramps down.
 * Verifies the system handles sudden traffic spikes without cascading
 * failures or excessive error rates.
 * 
 * Prerequisites:
 *   - Docker Compose cluster running
 *   - k6 installed
 *   
 * Usage:
 *   k6 run chaos-tests/spike-load.js
 */

export const options = {
    stages: [
        { duration: '5s', target: 100 },  // Warm up
        { duration: '10s', target: 1000 },  // Spike!
        { duration: '30s', target: 1000 },  // Hold peak
        { duration: '10s', target: 100 },  // Ramp down
        { duration: '5s', target: 0 },  // Cool down
    ],
    thresholds: {
        'http_req_duration': ['p(95)<500'],
        'http_req_failed': ['rate<0.01'],    // Less than 1% error rate
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost';

export function setup() {
    const codes = [];
    for (let i = 0; i < 50; i++) {
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/spike-test-${i}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        if (res.status === 200) {
            codes.push(JSON.parse(res.body).shortCode);
        }
    }
    console.log(`Setup: created ${codes.length} URLs for spike test`);
    return { codes };
}

export default function (data) {
    if (data.codes.length === 0) return;

    const code = data.codes[Math.floor(Math.random() * data.codes.length)];

    // 85% reads, 15% writes (typical real-world ratio)
    if (Math.random() < 0.85) {
        const res = http.get(`${BASE_URL}/${code}`, { redirects: 0 });
        check(res, {
            'spike redirect: status ok': (r) => r.status === 302 || r.status === 301 || r.status === 429,
        });
    } else {
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/spike-${Date.now()}-${Math.random()}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        check(res, {
            'spike create: status ok': (r) => r.status === 200 || r.status === 429,
        });
    }

    sleep(0.05);
}

export function handleSummary(data) {
    const failRate = data.metrics.http_req_failed?.values?.rate || 0;
    const p95 = data.metrics.http_req_duration?.values?.['p(95)'] || 0;
    const totalReqs = data.metrics.http_req_duration?.values?.count || 0;

    console.log(`\n=== Spike Load Chaos Test Results ===`);
    console.log(`  Total Requests: ${totalReqs}`);
    console.log(`  Failure Rate:   ${(failRate * 100).toFixed(2)}%`);
    console.log(`  P95 Latency:    ${p95.toFixed(0)}ms`);
    console.log(`  PASS Criteria:  P95<500ms, Error<1%`);
    console.log(`  Verdict:        ${failRate < 0.01 && p95 < 500 ? 'PASS ✅' : 'FAIL ❌'}`);
    console.log(`=====================================\n`);

    return {};
}

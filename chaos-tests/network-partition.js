import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * Chaos Test: Network Partition Simulation
 * 
 * Simulates network partition by having the test runner continue to
 * send requests while the external chaos orchestrator disconnects
 * containers from the Docker network. Verifies timeout handling
 * and error responses are clean.
 * 
 * Prerequisites:
 *   - Docker Compose cluster running
 *   - k6 installed
 *   
 * Usage:
 *   k6 run chaos-tests/network-partition.js
 */

export const options = {
    scenarios: {
        partition_test: {
            executor: 'constant-vus',
            vus: 20,
            duration: '90s',
        },
    },
    thresholds: {
        'http_req_duration': ['p(95)<5000'],   // Allow higher latency during partition
        'http_req_failed': ['rate<0.30'],       // Up to 30% failure acceptable
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost';

export function setup() {
    const codes = [];
    for (let i = 0; i < 10; i++) {
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/partition-test-${i}` }),
            { headers: { 'Content-Type': 'application/json' } }
        );
        if (res.status === 200) {
            codes.push(JSON.parse(res.body).shortCode);
        }
    }
    return { codes };
}

export default function (data) {
    if (data.codes.length === 0) return;

    const code = data.codes[Math.floor(Math.random() * data.codes.length)];

    // Mix of read and write operations
    if (Math.random() < 0.7) {
        const res = http.get(`${BASE_URL}/${code}`, {
            redirects: 0,
            timeout: '10s'
        });
        check(res, {
            'partition: response received': (r) => r.status > 0,
            'partition: no unhandled crash': (r) => r.status !== 502 && r.status !== 503,
        });
    } else {
        const res = http.post(`${BASE_URL}/api/shorten`,
            JSON.stringify({ originalUrl: `https://example.com/during-partition-${Date.now()}` }),
            { headers: { 'Content-Type': 'application/json' }, timeout: '10s' }
        );
        check(res, {
            'partition create: response received': (r) => r.status > 0,
        });
    }

    sleep(0.3);
}

export function handleSummary(data) {
    const failRate = data.metrics.http_req_failed?.values?.rate || 0;
    const p95 = data.metrics.http_req_duration?.values?.['p(95)'] || 0;

    console.log(`\n=== Network Partition Chaos Test Results ===`);
    console.log(`  Failure Rate: ${(failRate * 100).toFixed(2)}%`);
    console.log(`  P95 Latency:  ${p95.toFixed(0)}ms`);
    console.log(`  Verdict:      ${failRate < 0.30 ? 'PASS ✅' : 'FAIL ❌'}`);
    console.log(`=============================================\n`);

    return {};
}

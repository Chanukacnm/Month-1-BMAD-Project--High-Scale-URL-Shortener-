import http from 'k6/http';
import { check, sleep } from 'k6';

// Three required test scenarios: baseline, sustained, spike
export let options = {
    scenarios: {
        // Scenario 1: Baseline (low load)
        baseline: {
            executor: 'constant-vus',
            vus: 10,
            duration: '1m',
            startTime: '0s',
        },
        // Scenario 2: Sustained load (150+ RPS target)
        sustained: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '30s', target: 50 },   // Ramp up
                { duration: '2m', target: 50 },     // Sustain
                { duration: '30s', target: 0 },     // Ramp down
            ],
            startTime: '1m10s', // Start after baseline completes
        },
        // Scenario 3: Spike test (500+ RPS burst)
        spike: {
            executor: 'ramping-vus',
            startVUs: 10,
            stages: [
                { duration: '10s', target: 200 },   // Spike up
                { duration: '30s', target: 200 },   // Hold spike
                { duration: '10s', target: 10 },     // Recover
            ],
            startTime: '4m30s', // Start after sustained completes
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<500'],   // P95 under 500ms (standalone SQLite)
        http_req_failed: ['rate<0.01'],     // Less than 1% failure rate
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5023';

export default function () {
    // 1. Create a short URL
    let payload = JSON.stringify({
        originalUrl: `https://example.com/${Math.random().toString(36).substring(7)}`
    });
    let params = {
        headers: { 'Content-Type': 'application/json' },
    };

    let createRes = http.post(`${BASE_URL}/api/shorten`, payload, params);
    check(createRes, {
        'url created': (r) => r.status === 200 || r.status === 201,
    });

    if (createRes.status === 200 || createRes.status === 201) {
        let shortCode = JSON.parse(createRes.body).shortCode;

        // 2. Perform multiple redirections (simulates read-heavy 5:1 ratio)
        for (let i = 0; i < 5; i++) {
            let redirectRes = http.get(`${BASE_URL}/${shortCode}`, {
                redirects: 0,
            });
            check(redirectRes, {
                'redirect successful': (r) => r.status === 302,
            });
            sleep(0.1);
        }

        // 3. Check stats endpoint
        let statsRes = http.get(`${BASE_URL}/api/stats/${shortCode}`);
        check(statsRes, {
            'stats returned': (r) => r.status === 200,
            'stats has clicks': (r) => {
                let body = JSON.parse(r.body);
                return body.totalClicks !== undefined;
            },
        });
    }

    sleep(0.5);
}

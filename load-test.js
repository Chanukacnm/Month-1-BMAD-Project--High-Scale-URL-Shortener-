import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '1m', target: 50 }, // Ramp up to 50 users
        { duration: '3m', target: 50 }, // Stay at 50 users
        { duration: '1m', target: 0 },  // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<100'], // 95% of requests must be below 100ms
    },
};

const BASE_URL = 'http://localhost'; // Target Nginx

export default function () {
    // 1. Create a short URL
    let payload = JSON.stringify({
        originalUrl: `https://example.com/${Math.random().toString(36).substring(7)}`
    });
    let params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };
    
    let createRes = http.post(`${BASE_URL}/api/urls`, payload, params);
    check(createRes, {
        'url created': (r) => r.status === 201 || r.status === 200,
    });

    if (createRes.status === 201 || createRes.status === 200) {
        let shortCode = JSON.parse(createRes.body).shortCode;

        // 2. Perform multiple redirections to test cache
        for (let i = 0; i < 5; i++) {
            let redirectRes = http.get(`${BASE_URL}/${shortCode}`, {
                redirects: 0,
            });
            check(redirectRes, {
                'redirect successful': (r) => r.status === 302,
            });
            sleep(0.1);
        }
    }

    sleep(1);
}

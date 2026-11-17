import http from 'k6/http';
import { check } from 'k6';

const api = __ENV.API || 'https://localhost:7119';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    reads: { executor: 'constant-vus', vus: 50, duration: '30s' },
  },
  // Thresholds per endpoint make k6 print each one's percentiles in the summary.
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:rewards}': ['p(95)<10000'],
    'http_req_duration{endpoint:balance}': ['p(95)<10000'],
    'http_req_duration{endpoint:redemptions}': ['p(95)<10000'],
  },
};

export default function () {
  const rewards = http.get(`${api}/rewards?category=Merch&page=1&pageSize=20`, { tags: { endpoint: 'rewards' } });
  const balance = http.get(`${api}/customers/C-100/balance`, { tags: { endpoint: 'balance' } });
  const list = http.get(`${api}/customers/C-100/redemptions?limit=20`, { tags: { endpoint: 'redemptions' } });

  check(rewards, { 'rewards 200': (r) => r.status === 200 });
  check(balance, { 'balance 200': (r) => r.status === 200 });
  check(list, { 'redemptions 200': (r) => r.status === 200 });
}
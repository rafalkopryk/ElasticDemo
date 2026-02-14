import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5275';
const VUS = parseInt(__ENV.K6_VUS || '3');
const DURATION = __ENV.K6_DURATION || '20s';
const DISCOVERY_SIZE = parseInt(__ENV.K6_DISCOVERY_SIZE || '500');

const ALL_ROLES = ['MainClient', 'Spouse', 'CoApplicant'];

// Parse duration string to seconds
function parseDuration(d) {
  const m = d.match(/^(\d+)(s|m|h)$/);
  if (!m) return 20;
  const v = parseInt(m[1]);
  return m[2] === 'h' ? v * 3600 : m[2] === 'm' ? v * 60 : v;
}

const DURATION_SECS = parseDuration(DURATION);

// Per-scenario custom trends
const trends = {
  // Baseline
  empty_search: new Trend('search_empty_duration', true),
  by_product: new Trend('search_by_product_duration', true),
  by_date_range: new Trend('search_by_date_range_duration', true),
  not_found_cif: new Trend('search_not_found_cif_duration', true),
  // Per-role: main client
  main_client_fullName: new Trend('search_by_main_client_fullName', true),
  main_client_clientId: new Trend('search_by_main_client_clientId', true),
  // Per-role: spouse
  spouse_fullName: new Trend('search_by_spouse_fullName', true),
  spouse_clientId: new Trend('search_by_spouse_clientId', true),
  // Per-role: co-applicant
  coApplicant_fullName: new Trend('search_by_coApplicant_fullName', true),
  coApplicant_clientId: new Trend('search_by_coApplicant_clientId', true),
  // All roles
  all_roles_clientId: new Trend('search_by_all_roles_clientId', true),
  all_roles_fullName: new Trend('search_by_all_roles_fullName', true),
  // Combined: 1 representative per group
  mainClient_clientId_product_channel_status: new Trend('search_by_mainClient_clientId_product_channel_status', true),
  mainClient_fullName_product_channel_status: new Trend('search_by_mainClient_fullName_product_channel_status', true),
  all_roles_clientId_product_channel_status: new Trend('search_by_all_roles_clientId_product_channel_status', true),
  all_roles_fullName_product_channel_status: new Trend('search_by_all_roles_fullName_product_channel_status', true),
};

// Sequential scheduling: each scenario runs after the previous one finishes
let _nextStart = 0;
function scenario(exec) {
  const start = _nextStart;
  _nextStart += DURATION_SECS;
  return {
    executor: 'constant-vus',
    vus: VUS,
    duration: DURATION,
    startTime: `${start}s`,
    exec,
  };
}

export const options = {
  scenarios: {
    // Baseline
    empty_search: scenario('emptySearch'),
    by_product: scenario('byProduct'),
    by_date_range: scenario('byDateRange'),
    not_found_cif: scenario('notFoundCif'),
    // Per-role: main client
    main_client_fullName: scenario('mainClientFullName'),
    main_client_clientId: scenario('mainClientClientId'),
    // Per-role: spouse
    spouse_fullName: scenario('spouseFullName'),
    spouse_clientId: scenario('spouseClientId'),
    // Per-role: co-applicant
    coApplicant_fullName: scenario('coApplicantFullName'),
    coApplicant_clientId: scenario('coApplicantClientId'),
    // All roles
    all_roles_clientId: scenario('allRolesClientId'),
    all_roles_fullName: scenario('allRolesFullName'),
    // Combined: 1 representative per group
    mainClient_clientId_product_channel_status: scenario('mainClientClientIdProductChannelStatus'),
    mainClient_fullName_product_channel_status: scenario('mainClientFullNameProductChannelStatus'),
    all_roles_clientId_product_channel_status: scenario('allRolesClientIdProductChannelStatus'),
    all_roles_fullName_product_channel_status: scenario('allRolesFullNameProductChannelStatus'),
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

const headers = { 'Content-Type': 'application/json' };

function searchUrl() {
  return `${BASE_URL}/api/applications/search`;
}

function post(payload) {
  return http.post(searchUrl(), JSON.stringify(payload), { headers });
}

function pick(arr, iter) {
  return arr[iter % arr.length];
}

function run(trendKey, payload) {
  const res = post(payload);
  trends[trendKey].add(res.timings.duration);
  check(res, { [`${trendKey}: status 200`]: r => r.status === 200 });
  sleep(0.1);
}

// --- setup: discover real data from the index ---

export function setup() {
  const res = http.post(searchUrl(), JSON.stringify({ size: DISCOVERY_SIZE, sort: 'desc' }), { headers });
  if (res.status !== 200) {
    throw new Error(`Discovery request failed: ${res.status} ${res.body}`);
  }

  const body = res.json();
  const apps = body.applications || [];
  if (apps.length === 0) {
    throw new Error('No applications found — seed the index first');
  }

  const products = [...new Set(apps.map(a => a.product))];
  const channels = [...new Set(apps.map(a => a.channel))];
  const statuses = [...new Set(apps.map(a => a.status))];
  const transactions = [...new Set(apps.map(a => a.transaction))];

  // Main client data
  const mainClients = apps.map(a => a.mainApplicant.client);
  const mainNames = mainClients.map(c => ({ firstName: c.firstName, lastName: c.lastName }));
  const mainClientIds = [...new Set(mainClients.map(c => c.clientId))];

  // Spouse data
  const spouseClients = [];
  for (const app of apps) {
    const sp = app.mainApplicant.spouse;
    if (sp) spouseClients.push(sp);
    for (const co of app.coApplicants || []) {
      if (co.spouse) spouseClients.push(co.spouse);
    }
  }
  const spouseNames = spouseClients.map(c => ({ firstName: c.firstName, lastName: c.lastName }));
  const spouseClientIds = [...new Set(spouseClients.map(c => c.clientId))];

  // Co-applicant data
  const coClients = [];
  for (const app of apps) {
    for (const co of app.coApplicants || []) {
      coClients.push(co.client);
    }
  }
  const coApplicantNames = coClients.map(c => ({ firstName: c.firstName, lastName: c.lastName }));
  const coApplicantClientIds = [...new Set(coClients.map(c => c.clientId))];

  // Date range covering ~30% of data
  const dates = apps.map(a => new Date(a.createdAt).getTime());
  const minDate = new Date(Math.min(...dates));
  const maxDate = new Date(Math.max(...dates));
  const range = maxDate.getTime() - minDate.getTime();
  const dateFrom = new Date(minDate.getTime() + range * 0.35).toISOString();
  const dateTo = new Date(minDate.getTime() + range * 0.65).toISOString();

  const fb = { firstName: 'ZZZFallback', lastName: 'ZZZFallback' };

  const data = {
    products,
    channels,
    statuses,
    transactions,
    mainNames,
    mainClientIds,
    spouseNames: spouseNames.length > 0 ? spouseNames : [fb],
    spouseClientIds: spouseClientIds.length > 0 ? spouseClientIds : ['CIF000000000'],
    coApplicantNames: coApplicantNames.length > 0 ? coApplicantNames : [fb],
    coApplicantClientIds: coApplicantClientIds.length > 0 ? coApplicantClientIds : ['CIF000000000'],
    dateFrom,
    dateTo,
  };

  const totalTime = _nextStart;
  console.log(`Sequential run: ${Object.keys(options.scenarios).length} scenarios × ${DURATION} = ${totalTime}s total`);
  console.log(`Discovered: ${apps.length} apps, ${products.length} products, ${channels.length} channels, ${statuses.length} statuses, ${transactions.length} transactions`);
  console.log(`  Main clients: ${mainNames.length} names, ${mainClientIds.length} CIFs`);
  console.log(`  Spouses: ${spouseNames.length} names, ${spouseClientIds.length} CIFs`);
  console.log(`  CoApplicants: ${coApplicantNames.length} names, ${coApplicantClientIds.length} CIFs`);
  console.log(`  Date range for queries: ${dateFrom} to ${dateTo}`);

  return data;
}

// --- Baseline scenarios ---

export function emptySearch() {
  run('empty_search', { size: 10 });
}

export function byProduct(data) {
  run('by_product', { product: pick(data.products, __ITER), size: 10 });
}

export function byDateRange(data) {
  run('by_date_range', { createdAtFrom: data.dateFrom, createdAtTo: data.dateTo, size: 10 });
}

export function notFoundCif() {
  const res = post({ clientId: 'CIF000000000', roles: ALL_ROLES, size: 10 });
  trends.not_found_cif.add(res.timings.duration);
  check(res, {
    'not_found_cif: status 200': r => r.status === 200,
    'not_found_cif: empty results': r => (r.json().applications || []).length === 0,
  });
  sleep(0.1);
}

// --- Per-role: main client ---

export function mainClientFullName(data) {
  const c = pick(data.mainNames, __ITER);
  run('main_client_fullName', { firstName: c.firstName, lastName: c.lastName, roles: ['MainClient'], size: 10 });
}

export function mainClientClientId(data) {
  run('main_client_clientId', { clientId: pick(data.mainClientIds, __ITER), roles: ['MainClient'], size: 10 });
}

// --- Per-role: spouse ---

export function spouseFullName(data) {
  const c = pick(data.spouseNames, __ITER);
  run('spouse_fullName', { firstName: c.firstName, lastName: c.lastName, roles: ['Spouse'], size: 10 });
}

export function spouseClientId(data) {
  run('spouse_clientId', { clientId: pick(data.spouseClientIds, __ITER), roles: ['Spouse'], size: 10 });
}

// --- Per-role: co-applicant ---

export function coApplicantFullName(data) {
  const c = pick(data.coApplicantNames, __ITER);
  run('coApplicant_fullName', { firstName: c.firstName, lastName: c.lastName, roles: ['CoApplicant'], size: 10 });
}

export function coApplicantClientId(data) {
  run('coApplicant_clientId', { clientId: pick(data.coApplicantClientIds, __ITER), roles: ['CoApplicant'], size: 10 });
}

// --- All roles ---

export function allRolesClientId(data) {
  run('all_roles_clientId', { clientId: pick(data.mainClientIds, __ITER), roles: ALL_ROLES, size: 10 });
}

export function allRolesFullName(data) {
  const c = pick(data.mainNames, __ITER);
  run('all_roles_fullName', { firstName: c.firstName, lastName: c.lastName, roles: ALL_ROLES, size: 10 });
}

// --- Combined: 1 representative per group ---

export function mainClientClientIdProductChannelStatus(data) {
  run('mainClient_clientId_product_channel_status', {
    clientId: pick(data.mainClientIds, __ITER), product: pick(data.products, __ITER),
    channel: pick(data.channels, __ITER + 2), status: pick(data.statuses, __ITER + 4),
    roles: ['MainClient'], size: 10,
  });
}

export function mainClientFullNameProductChannelStatus(data) {
  const c = pick(data.mainNames, __ITER);
  run('mainClient_fullName_product_channel_status', {
    firstName: c.firstName, lastName: c.lastName, product: pick(data.products, __ITER),
    channel: pick(data.channels, __ITER + 2), status: pick(data.statuses, __ITER + 4),
    roles: ['MainClient'], size: 10,
  });
}

export function allRolesClientIdProductChannelStatus(data) {
  run('all_roles_clientId_product_channel_status', {
    clientId: pick(data.mainClientIds, __ITER), product: pick(data.products, __ITER),
    channel: pick(data.channels, __ITER + 2), status: pick(data.statuses, __ITER + 4),
    roles: ALL_ROLES, size: 10,
  });
}

export function allRolesFullNameProductChannelStatus(data) {
  const c = pick(data.mainNames, __ITER);
  run('all_roles_fullName_product_channel_status', {
    firstName: c.firstName, lastName: c.lastName, product: pick(data.products, __ITER),
    channel: pick(data.channels, __ITER + 2), status: pick(data.statuses, __ITER + 4),
    roles: ALL_ROLES, size: 10,
  });
}

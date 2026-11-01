import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHmac, randomBytes } from 'node:crypto';
import { mkdtempSync, writeFileSync, unlinkSync, rmdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

const cwd = dirname(fileURLToPath(import.meta.url));
const project = `fiapgames-search-test-${randomBytes(4).toString('hex')}`;
const temporary = mkdtempSync(join(tmpdir(), 'fiap-search-test-'));
const envFile = join(temporary, 'test.env');
const secret = randomBytes(32).toString('hex');
writeFileSync(envFile, `TEST_PASSWORD=${randomBytes(24).toString('hex')}Aa1\nTEST_JWT=${secret}\n`, { mode: 0o600 });
const composeArgs = ['compose', '-p', project, '--env-file', envFile, '-f', join(cwd, 'compose.search.yml')];
function compose(args, capture = false) {
  return execFileSync('docker', [...composeArgs, ...args], {
    cwd, encoding: 'utf8', stdio: capture ? ['ignore', 'pipe', 'pipe'] : 'inherit', timeout: 300000,
  });
}
function address(service, port) {
  return `http://${compose(['port', service, String(port)], true).trim()}`;
}
const jwtPart = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const unsigned = `${jwtPart({ alg: 'HS256', typ: 'JWT' })}.${jwtPart({
  sub: 'search-integration-tests', iss: 'User.Games.Fiap', aud: 'Games.Fiap', exp: Math.floor(Date.now() / 1000) + 3600,
})}`;
const token = `${unsigned}.${createHmac('sha256', secret).update(unsigned).digest('base64url')}`;
let base;
let searchBase;
let assertions = 0;
async function request(path, { method = 'GET', body, auth = true } = {}) {
  const response = await fetch(`${base}${path}`, {
    method, headers: { 'Content-Type': 'application/json', ...(auth ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(12000),
  });
  const text = await response.text();
  let data;
  try { data = JSON.parse(text); } catch { data = text; }
  return { status: response.status, data };
}
async function eventually(description, check, timeout = 90000) {
  const deadline = Date.now() + timeout;
  let last;
  while (Date.now() < deadline) {
    try { if (await check()) { console.log(`PASS ${description}`); assertions++; return; } }
    catch (error) { last = error; }
    await delay(2000);
  }
  throw new Error(`Timeout: ${description}`, { cause: last });
}
function check(description, assertion) {
  assertion(); assertions++; console.log(`PASS ${description}`);
}
async function create(title, description = '', genre = 'Adventure') {
  const result = await request('/games', { method: 'POST', body: { title, description, genre, price: 99.9 } });
  assert.equal(result.status, 201, JSON.stringify(result));
  return result.data;
}
async function search(q, extra = '') { return request(`/search?q=${encodeURIComponent(q)}${extra}`); }

try {
  compose(['up', '-d', '--build', '--wait', '--wait-timeout', '180']);
  base = address('kong', 8000);
  searchBase = address('opensearch', 9200);
  await eventually('API and search ready through the real Kong configuration', async () => (await search('initial')).status === 200);
  assert.equal((await request('/health/ready', { auth: false })).status, 200);
  const directAnonymous = compose(['exec', '-T', 'opensearch', 'curl', '-s', '-o', '/dev/null', '-w', '%{http_code}',
    'http://catalog-api:8080/search?q=minecraft'], true);
  check('JWT also enforced inside the API', () => assert.equal(directAnonymous, '401'));
  const anonymous = await request('/search?q=minecraft', { auth: false });
  check('JWT required at gateway', () => assert.equal(anonymous.status, 401));
  for (const query of ['', 'q=%20%20', 'q=minecraft&page=0', 'q=minecraft&pageSize=101', 'q=minecraft&page=501&pageSize=20', `q=${'a'.repeat(201)}`]) {
    assert.equal((await request(`/search?${query}`)).status, 400, query);
  }
  console.log('PASS empty, long and invalid pagination requests rejected'); assertions++;

  const minecraft = await create('Minecraft', 'Sandbox adventure');
  const guide = await create('Sandbox Guide', 'Minecraft guide');
  const accent = await create('Ação Galáctica', 'Exploração espacial');
  let result = await search('minecrfat');
  check('fuzzy typo, title boost, relevance and total', () => {
    assert.equal(result.status, 200, JSON.stringify(result));
    assert.equal(result.data.total, 2);
    assert.equal(result.data.items[0].game.id, minecraft.id);
    assert.ok(result.data.items[0].score > result.data.items[1].score);
    assert.equal(result.data.items[0].game.price, 99.9);
  });
  result = await search('acao');
  check('accent-insensitive text analysis', () => assert.equal(result.data.items[0].game.id, accent.id));
  const page1 = (await search('minecraft', '&pageSize=1')).data;
  const page2 = (await search('minecraft', '&page=2&pageSize=1')).data;
  check('stable pagination', () => {
    assert.equal(page1.total, 2); assert.equal(page2.page, 2);
    assert.notEqual(page1.items[0].game.id, page2.items[0].game.id);
  });

  const edited = { title: 'Terraria', description: 'Updated adventure', price: 49, genre: 'Adventure', active: true };
  assert.equal((await request(`/games/${minecraft.id}`, { method: 'PUT', body: edited })).status, 200);
  result = await search('Terraria');
  check('update replaces original document', () => {
    assert.equal(result.data.items[0].game.id, minecraft.id);
    assert.equal(result.data.items[0].game.price, 49);
  });
  assert.equal((await search('minecraft')).data.total, 1);
  console.log('PASS old title no longer matches changed game'); assertions++;
  assert.equal((await request(`/games/${guide.id}`, { method: 'DELETE' })).status, 204);
  assert.equal((await search('minecraft')).data.total, 0);
  console.log('PASS inactive games excluded'); assertions++;

  const stale = await fetch(`${searchBase}/search-test-games/_doc/${minecraft.id}?version=1&version_type=external_gte&require_alias=true`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...edited, title: 'Stale title', id: minecraft.id }),
  });
  check('stale OpenSearch version cannot overwrite latest edit', () => assert.equal(stale.status, 409));

  compose(['stop', 'opensearch']);
  assert.equal((await search('Terraria')).status, 503);
  assert.equal((await request('/health/ready', { auth: false })).status, 503);
  assert.equal((await request('/health', { auth: false })).status, 200);
  console.log('PASS outage returns 503, no SQL search fallback'); assertions++;
  const pending = await create('DurablePendingGame');
  assert.equal((await request(`/games/${pending.id}`, { method: 'PUT', body: { ...edited, title: 'DurableRecoveredGame' } })).status, 200);
  // Restart with OpenSearch still unavailable: retry state must live in SQL.
  compose(['restart', 'catalog-api']);
  await eventually('API starts even while OpenSearch is down', async () => (await request('/health', { auth: false })).status === 200);
  compose(['start', 'opensearch']);
  // Published ports can change when starting a stopped container.
  searchBase = address('opensearch', 9200);
  await eventually('pending latest write recovered after API and OpenSearch restart', async () => {
    const recovered = await search('DurableRecoveredGame');
    return recovered.status === 200 && recovered.data.total === 1 && recovered.data.items[0].game.id === pending.id;
  });
  assert.equal((await search('DurablePendingGame')).data.total, 0);

  const removed = await fetch(`${searchBase}/search-test-games-000001`, { method: 'DELETE' });
  assert.equal(removed.status, 200);
  await eventually('deleted index rebuilt automatically from SQL including existing games', async () => {
    const recovered = await search('Terraria');
    return recovered.status === 200 && recovered.data.total === 1;
  });
  assert.equal((await search('minecraft')).data.total, 0);
  compose(['run', '--rm', '-e', 'OpenSearch__RebuildOnly=true', 'catalog-api']);
  assert.equal((await search('DurableRecoveredGame')).data.total, 1);
  console.log('PASS explicit rebuild is idempotent'); assertions++;
  console.log(`\n${assertions} integration scenarios passed (SQL Server + OpenSearch + API + Kong).`);
} catch (error) {
  console.error(error);
  compose(['logs', '--tail', '60', 'catalog-api', 'opensearch', 'kong']);
  process.exitCode = 1;
} finally {
  try { compose(['down', '--volumes', '--remove-orphans']); }
  finally { unlinkSync(envFile); rmdirSync(temporary); }
}

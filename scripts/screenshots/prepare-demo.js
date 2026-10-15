// Run only against the dedicated local development server. Never use a production library.
const fs = require('node:fs/promises');
const path = require('node:path');

async function main() {
  const repo = path.resolve(__dirname, '../..');
  const output = path.join(repo, '_out/codex/screenshots');
  const base = 'http://127.0.0.1:13283';
  const photos = JSON.parse(await fs.readFile(path.join(__dirname, 'photos.json')));
  const stateFile = path.join(repo, '.local/immich/screenshots-demo.json');
  const readJson = async file => JSON.parse(await fs.readFile(file, 'utf8'));
  const writeJson = async (file, data) => {
    await fs.mkdir(path.dirname(file), { recursive: true });
    await fs.writeFile(file, JSON.stringify(data, null, 2) + '\n');
  };
  const account = await readJson(path.join(repo, '.local/immich/development-account.json'));
  async function api(route, method = 'GET', body, token) {
    const response = await fetch(base + route, {
      method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (!response.ok) throw new Error(`${method} ${route}: HTTP ${response.status}`);
    return response.status === 204 ? undefined : response.json();
  }
  const login = await api('/api/auth/login', 'POST', { email: account.adminEmail, password: account.adminPassword });
  let state;
  try { state = await readJson(stateFile); } catch (error) { if (error.code !== 'ENOENT') throw error; state = { assets: [], albums: {} }; }
  await fs.mkdir(path.join(output, 'photos'), { recursive: true });
  for (const [index, photo] of photos.entries()) {
    const file = path.join(output, 'photos', photo.file);
    try { await fs.access(file); } catch {
      const response = await fetch(`https://images.pexels.com/photos/${photo.id}/pexels-photo-${photo.id}.jpeg?auto=compress&cs=tinysrgb&w=2560`);
      if (!response.ok || !response.headers.get('content-type')?.startsWith('image/')) throw new Error(`Photo ${photo.id} unavailable`);
      await fs.writeFile(file, Buffer.from(await response.arrayBuffer()));
    }
    if (state.assets.some(asset => asset.file === photo.file)) continue;
    const form = new FormData();
    form.set('deviceAssetId', `lounge-screenshots-${photo.id}`);
    form.set('deviceId', 'immich-lounge-screenshot-demo');
    form.set('fileCreatedAt', `2025-07-${String(12 + index).padStart(2, '0')}T12:00:00.000Z`);
    form.set('fileModifiedAt', '2025-07-15T12:00:00.000Z');
    form.set('assetData', new Blob([await fs.readFile(file)], { type: 'image/jpeg' }), photo.file);
    const response = await fetch(base + '/api/assets', { method: 'POST', headers: { Authorization: `Bearer ${login.accessToken}` }, body: form });
    if (!response.ok) throw new Error(`Upload ${photo.file}: HTTP ${response.status}`);
    const asset = await response.json();
    state.assets.push({ file: photo.file, id: asset.id });
    await writeJson(stateFile, state);
  }
  for (const [name, files] of [
    ['Alpine Escapes', photos.slice(0, 3).map(photo => photo.file)],
    ['Quiet Evenings', ['mountain-reflection.jpg', 'waterfall.jpg']],
    ['Travel Favourites', photos.map(photo => photo.file)]
  ]) {
    if (state.albums[name]) continue;
    const album = await api('/api/albums', 'POST', {
      albumName: name, description: 'Curated Immich Lounge screenshot demo. Photos licensed by Pexels.',
      assetIds: state.assets.filter(asset => files.includes(asset.file)).map(asset => asset.id)
    }, login.accessToken);
    state.albums[name] = album.id;
    await writeJson(stateFile, state);
  }
  if (!state.apiKey) {
    const key = await api('/api/api-keys', 'POST', {
      name: 'Immich Lounge screenshot demo',
      permissions: ['asset.read', 'asset.statistics', 'asset.view', 'album.read', 'person.read', 'tag.read', 'memory.read']
    }, login.accessToken);
    state.apiKey = key.secret;
    await writeJson(stateFile, state);
  }
  await writeJson(path.join(output, 'data/settings.json'), {
    schemaVersion: 1, friendlyName: 'Immich Lounge Demo', companionUuid: '0c3299aa-e4b0-4980-b9de-8800351716c8',
    immich: { serverUrl: base, apiKey: state.apiKey }
  });
  for (const [id, name, description, interval, background] of [
    ['alpine-escapes', 'Alpine Escapes', 'Mountain lakes and summer walks', 15, 'blur'],
    ['quiet-evenings', 'Quiet Evenings', 'Slow fades, portrait pairs, and a soft glow', 30, 'ambilight'],
    ['travel-favourites', 'Travel Favourites', 'Favourite views from our travels', 12, 'blur']
  ]) {
    await writeJson(path.join(output, `data/profiles/${id}.json`), {
      schemaVersion: 1, id, name, description, photoSource: 'selected',
      contentSources: [{ type: 'album', id: state.albums[name], label: name }],
      slideshow: { intervalSeconds: interval, shuffle: false, dateOrder: 'oldestFirst', transitionEffect: 'fade', transitionDurationSeconds: 1, photoMotion: 'none', refreshIntervalMinutes: 60 },
      display: { overlayStyle: 'bottom', overlayBehavior: 'always', overlayFields: ['date', 'album'], backgroundEffect: background, zoomToFit: true, sideBySidePortraits: id === 'quiet-evenings', clockAlwaysVisible: true, showDate: true, showTimer: false },
      imageQuality: 'preview'
    });
  }
  await writeJson(path.join(output, 'data/schedules/living-room.json'), {
    id: 'living-room', name: 'Living Room', fallbackProfileId: 'travel-favourites', enabled: true,
    timeZoneId: 'Europe/Zurich', entries: [
      { profileId: 'alpine-escapes', days: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'], start: '08:00', end: '18:00' },
      { profileId: 'quiet-evenings', days: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'], start: '18:00', end: '23:00' }
    ]
  });
  console.log(`Prepared ${photos.length} curated photos, 3 profiles, and a weekly schedule. Credentials remain in ignored local files.`);
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});

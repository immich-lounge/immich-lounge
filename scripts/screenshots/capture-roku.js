// Explicit device workflow: temporarily deploy capture fixtures, then restore the normal channel.
const fs = require('node:fs/promises');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const net = require('node:net');
const { createWriteStream } = require('node:fs');

async function main() {
  if (!process.argv.includes('--replace-dev-app')) throw new Error('Requires --replace-dev-app: this replaces the installed development app, then restores the current checkout normal channel.');
  const repo = path.resolve(__dirname, '../..');
  const roku = path.join(repo, 'roku');
  require(path.join(roku, 'node_modules/dotenv')).config({ path: path.join(roku, '.env'), quiet: true });
  const deploy = require(path.join(roku, 'node_modules/roku-deploy'));
  const host = process.env.ROKU_HOST;
  const password = process.env.ROKU_PASSWORD;
  if (!host || !password) throw new Error('ROKU_HOST and ROKU_PASSWORD are required');
  const output = path.join(roku, '_out/screenshots');
  const captures = path.join(repo, '_out/codex/screenshots/captures');
  await fs.mkdir(captures, { recursive: true });
  const options = {
    host, password, rootDir: path.join(output, 'app'), stagingDir: path.join(output, 'upload'),
    outDir: output, outFile: 'immich-lounge-screenshot-demo', files: ['**/*'],
    retainStagingDir: true, retainStagingFolder: true, retainDeploymentArchive: true
  };
  async function post(route) {
    for (let attempt = 0; attempt < 4; attempt++) {
      const response = await fetch(`http://${host}:8060${route}`, { method: 'POST' });
      if (response.ok) return;
      if (response.status !== 503 || attempt === 3) throw new Error(`Roku ECP ${route}: HTTP ${response.status}`);
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
  }
  const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
  const requestedViews = process.argv.find(argument => argument.startsWith('--views='))?.slice('--views='.length).split(',');
  // Build the restoration package before modifying the installed development app.
  const restore = path.join(output, 'restore');
  const build = spawnSync(process.execPath, ['node_modules/brighterscript/dist/cli.js', '--project', 'bsconfig.json', '--stagingDir', restore, '--createPackage', 'false'], { cwd: roku, stdio: 'inherit' });
  if (build.status !== 0) throw new Error('Restore build failed; device left unchanged');
  const consoleLog = createWriteStream(path.join(output, 'console.log'));
  const consoleSocket = net.createConnection({ host, port: 8085 });
  consoleSocket.pipe(consoleLog);
  consoleSocket.on('error', error => console.log(`Console unavailable: ${error.message}`));
  let deployed = false;
  try {
    await post('/keypress/Home');
    await delay(1200);
    deployed = true;
    await deploy.deploy(options);
    await delay(5000);
    for (const [view, file, wait] of [
      ['slideshow', 'slideshow', 7000], ['portraits', 'slideshow-portraits', 10000],
      ['profiles', 'change-profile', 3000], ['connection', 'change-companion', 3000],
      ['settings', 'config-dialog', 7000]
    ]) {
      if (requestedViews && !requestedViews.includes(view)) continue;
      await post('/keypress/Home');
      await delay(1200);
      await post(`/launch/dev?view=${view}`);
      await delay(wait);
      const screenshot = await deploy.takeScreenshot({ host, password, outDir: captures, outFile: file });
      console.log(`Captured ${path.basename(screenshot)}`);
    }
  } finally {
    if (deployed) {
      await post('/keypress/Home');
      await delay(1200);
      await deploy.deploy({ ...options, rootDir: restore, stagingDir: path.join(output, 'restore-upload'), outFile: 'immich-lounge-restored-channel' });
      await post('/keypress/Home');
      console.log('Restored normal channel from the current checkout; Roku returned to Home.');
    }
    consoleSocket.destroy();
    consoleLog.end();
  }
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});

// Build-only capture fixture. Never publish this package as a release.
// Run prepare-demo.js first. Only generated Roku staging files are adapted.
const fs = require('node:fs/promises');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

async function main() {
  const repo = path.resolve(__dirname, '../..');
  const roku = path.join(repo, 'roku');
  const stage = path.join(roku, '_out/screenshots/app');
  const demo = path.join(repo, '_out/codex/screenshots');
  const result = spawnSync(process.execPath, ['node_modules/brighterscript/dist/cli.js', '--project', 'bsconfig.json', '--stagingDir', '_out/screenshots/app', '--createPackage', 'false'], { cwd: roku, stdio: 'inherit' });
  if (result.status !== 0) throw new Error('Roku build failed');
  const state = JSON.parse(await fs.readFile(path.join(repo, '.local/immich/screenshots-demo.json')));
  await fs.mkdir(path.join(stage, 'demo'), { recursive: true });
  const assetFiles = {};
  for (const asset of state.assets) {
    await fs.copyFile(path.join(demo, 'photos', asset.file), path.join(stage, 'demo', asset.file));
    assetFiles[asset.id] = `pkg:/demo/${asset.file}`;
    const response = await fetch(`http://127.0.0.1:13283/api/assets/${asset.id}`, { headers: { 'x-api-key': state.apiKey } });
    if (!response.ok) throw new Error(`Demo metadata: HTTP ${response.status}`);
    const metadata = await response.json();
    // Keep only the fields needed by the existing playback UI.
    await fs.writeFile(path.join(stage, `demo/${asset.id}.json`), JSON.stringify({
      id: asset.id, fileCreatedAt: metadata.fileCreatedAt,
      exifInfo: { dateTimeOriginal: metadata.exifInfo?.dateTimeOriginal, exifImageWidth: metadata.exifInfo?.exifImageWidth, exifImageHeight: metadata.exifInfo?.exifImageHeight }
    }));
  }
  await fs.writeFile(path.join(stage, 'demo/assets.json'), JSON.stringify(assetFiles));
  const selections = [];
  for (const id of ['alpine-escapes', 'quiet-evenings', 'travel-favourites']) {
    const response = await fetch(`http://127.0.0.1:4463/api/profiles/${id}`);
    if (!response.ok) throw new Error(`Demo profile: HTTP ${response.status}`);
    const profile = await response.json();
    // Packaged media requires no API key or reachable Immich endpoint.
    profile.immich = { serverUrl: 'http://demo.invalid', apiKey: '' };
    const files = id === 'quiet-evenings' ? ['mountain-reflection.jpg', 'waterfall.jpg'] : id === 'alpine-escapes' ? ['mountain-lake.jpg', 'alpine-trail.jpg', 'mountain-reflection.jpg'] : state.assets.map(asset => asset.file);
    const playlist = files.map(file => ({ id: state.assets.find(asset => asset.file === file).id, type: 'photo', sourceLabel: profile.name }));
    await fs.writeFile(path.join(stage, `demo/${id}.json`), JSON.stringify({ profile, playlist }));
    selections.push({ id, name: profile.name, kind: 'profile' });
  }
  selections.unshift({ id: 'living-room', name: 'Living Room', kind: 'schedule' });
  await fs.writeFile(path.join(stage, 'demo/selections.json'), JSON.stringify(selections));
  await fs.writeFile(path.join(stage, 'source/main.brs'), `
  sub Main(args as Dynamic)
      print "[Screenshot Demo] starting"
      screen = CreateObject("roSGScreen")
      port = CreateObject("roMessagePort")
      screen.setMessagePort(port)
      view = "slideshow"
      if args.view <> invalid then view = args.view
      if view = "profiles" or view = "connection" then
          scene = screen.CreateScene("DiscoveryScene")
          scene.suspendStartup = true
          screen.show()
          scene.callFunc("ShowScreenshotDemo", view)
      else
          id = "alpine-escapes"
          if view = "portraits" then id = "quiet-evenings"
          fixture = ParseJson(ReadAsciiFile("pkg:/demo/" + id + ".json"))
          scene = screen.CreateScene("SlideshowScene")
          ConfigureSlideshowSceneForLaunch(scene, fixture.profile, fixture.playlist, "", id, false, invalid, 0, true, view = "settings")
          screen.show()
          scene.setFocus(true)
      end if
      while true
          msg = wait(0, port)
          if type(msg) = "roSGScreenEvent" and msg.isScreenClosed() then exit while
      end while
  end sub
  `);
  const discoveryXmlFile = path.join(stage, 'components/settings/DiscoveryScene.xml');
  const discoveryXml = await fs.readFile(discoveryXmlFile, 'utf8');
  await fs.writeFile(discoveryXmlFile, discoveryXml.replace('<interface>', '<interface>\n    <function name="ShowScreenshotDemo" />'));
  const discoveryFile = path.join(stage, 'components/settings/DiscoveryScene.brs');
  await fs.appendFile(discoveryFile, `
  sub ShowScreenshotDemo(view as String)
      if view = "profiles" then
          m.profiles = ParseJson(ReadAsciiFile("pkg:/demo/selections.json"))
          ShowProfilePickerForScene(m)
      else
          InitializeFormDefaultsForScene(m)
          m.manualForm.host = "192.168.1.10"
          ShowManualEntryForScene(m)
      end if
  end sub
  `);
  await fs.writeFile(path.join(stage, 'source/MediaUrls.brs'), `
  function BuildMediaUrl(entry as Object, profile as Object) as String
      assets = ParseJson(ReadAsciiFile("pkg:/demo/assets.json"))
      return assets[entry.id]
  end function
  function BuildBackgroundMediaUrl(entry as Object, profile as Object) as String
      return BuildMediaUrl(entry, profile)
  end function
  `);
  const metaFile = path.join(stage, 'components/AssetMetaTask.brs');
  const meta = await fs.readFile(metaFile, 'utf8');
  if (!meta.includes('r = HttpGetJson(url, headers)')) throw new Error('Metadata fixture hook changed');
  await fs.writeFile(metaFile, meta.replace('r = HttpGetJson(url, headers)', 'r = { ok: true, data: ParseJson(ReadAsciiFile("pkg:/demo/" + assetId + ".json")) }'));
  // Preserve saved device settings, caches, and resume state throughout the capture session.
  const registryFile = path.join(stage, 'source/Registry.brs');
  let registry = await fs.readFile(registryFile, 'utf8');
  for (const name of ['RegistryWrite', 'RegistryWriteJson', 'RegistryDelete', 'RegistryClearAll']) {
    const pattern = new RegExp('sub ' + name + '\\([^\\n]*\\)[\\s\\S]*?end sub', 'i');
    if (!pattern.test(registry)) throw new Error(`Registry fixture hook changed: ${name}`);
    registry = registry.replace(pattern, match => match.split('\n')[0] + '\n    return\nend sub');
  }
  await fs.writeFile(registryFile, registry);
  console.log('Prepared capture-only Roku staging with packaged demo media and disabled registry writes. No deployment performed.');
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});

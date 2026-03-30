const fs = require("fs");
const path = require("path");

function fail(message) {
  console.error(message);
  process.exit(1);
}

const root = path.resolve(__dirname, "..");
const mainScreensaverPath = path.join(root, "source", "main_screensaver.brs");
const bsconfigPath = path.join(root, "bsconfig.screensaver.json");

const mainScreensaver = fs.readFileSync(mainScreensaverPath, "utf8");
const bsconfig = JSON.parse(fs.readFileSync(bsconfigPath, "utf8"));
const files = bsconfig.files || [];

if (!mainScreensaver.includes('scene = screen.CreateScene("DiscoveryScene")')) {
  fail("Screensaver settings must open DiscoveryScene directly.");
}

if (!mainScreensaver.includes("scene.isScreensaver = true")) {
  fail("Screensaver settings must mark DiscoveryScene as screensaver mode.");
}

if (!mainScreensaver.includes("scene.changeCompanionMode = true")) {
  fail("Screensaver settings must open DiscoveryScene in explicit companion-change mode.");
}

if (!mainScreensaver.includes("scene.changeProfileMode = true")) {
  fail("Screensaver settings must continue to explicit profile selection after companion changes.");
}

if (!mainScreensaver.includes('scene.observeField("setupComplete", port)')) {
  fail("Screensaver settings must observe setupComplete so the standalone settings screen exits cleanly.");
}

if (!mainScreensaver.includes('scene.observeField("cancelled", port)')) {
  fail("Screensaver settings must observe cancelled so the standalone settings screen exits cleanly.");
}

if (mainScreensaver.includes('scene = screen.CreateScene("ScreensaverSettingsScene")')) {
  fail("Screensaver settings still point at the placeholder settings scene.");
}

for (const required of ["components/settings/*.xml", "components/settings/*.brs"]) {
  if (!files.includes(required)) {
    fail(`Screensaver build is missing required settings include: ${required}`);
  }
}

console.log("Screensaver settings wiring looks correct.");

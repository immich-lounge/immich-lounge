// Capture the real Blazor UI. No DOM replacement or mocked API responses.
const fs = require('node:fs/promises');
const path = require('node:path');

async function main() {
  const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
  const repo = path.resolve(__dirname, '../..');
  const output = path.join(repo, '_out/codex/screenshots/captures');
  const base = 'http://127.0.0.1:4463';
  await fs.mkdir(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, ...(process.env.SCREENSHOT_BROWSER ? { executablePath: process.env.SCREENSHOT_BROWSER } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1, locale: 'en-GB', colorScheme: 'dark' });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    async function open(route, heading) {
      await page.setViewportSize({ width: 1440, height: 900 });
      await page.goto(base + route);
      await page.getByRole('heading', { name: heading, exact: true }).waitFor();
      // Wait for the Blazor circuit to connect before interacting.
      await page.locator('#components-reconnect-modal').waitFor({ state: 'attached' });
      await page.waitForTimeout(600);
    }
    async function capture(name, fullPage = false) {
      await page.evaluate(() => document.fonts.ready);
      if (fullPage) {
        // A tall viewport keeps the real sticky footer below the content it would otherwise cover in a full-page capture.
        await page.setViewportSize({ width: 1440, height: await page.evaluate(() => document.documentElement.scrollHeight) });
      }
      await page.screenshot({ path: path.join(output, `${name}.png`), animations: 'disabled' });
      await page.setViewportSize({ width: 1440, height: 900 });
      console.log(`Captured ${name}`);
    }
    await open('/profiles', 'Profiles');
    await page.getByText('Mountain lakes and summer walks').waitFor();
    await capture('companion-profiles');
    await open('/profiles/alpine-escapes/edit', 'Alpine Escapes');
    await page.getByRole('button', { name: 'Preview selection', exact: true }).click();
    await page.locator('.photo-preview-grid').waitFor();
    await page.locator('.photo-preview-grid').scrollIntoViewIfNeeded();
    await page.waitForFunction(() => Array.from(document.querySelectorAll('.photo-preview-grid img')).length > 0 && Array.from(document.querySelectorAll('.photo-preview-grid img')).every(img => img.complete && img.naturalWidth > 0));
    await page.evaluate(() => window.scrollTo(0, 0));
    await capture('companion-profile-editor', true);
    await page.locator('.photo-preview-grid').scrollIntoViewIfNeeded();
    await capture('companion-photo-selection');
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.getByRole('button', { name: 'Playback', exact: true }).click();
    await page.getByRole('heading', { name: 'Set the pace', exact: true }).waitFor();
    await capture('companion-playback');
    await page.getByRole('button', { name: 'Appearance', exact: true }).click();
    await page.getByRole('heading', { name: 'Make it yours', exact: true }).waitFor();
    await capture('companion-appearance');
    await open('/schedules/living-room/edit', 'Edit Schedule');
    await page.locator('legend').first().waitFor();
    await capture('companion-schedule-overview');
    await capture('companion-schedule', true);
    await open('/connection', 'Connect to Immich');
    // Show the documented example address in the editable form, without saving or connecting to it.
    await page.getByLabel('Immich Server URL', { exact: true }).fill('http://192.168.1.10:2283');
    await capture('companion-connection');
    if (errors.length) throw new Error(errors.join('\n'));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});

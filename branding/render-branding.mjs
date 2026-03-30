#!/usr/bin/env node
/**
 * Render all branding SVGs to PNGs using Inkscape and deploy them to all
 * destinations that consume each asset (roku/images/, docs/website/assets/,
 * companion wwwroot, etc.).
 *
 * Prerequisites
 * -------------
 * Inkscape must be installed. On Windows the default path is:
 *   C:\Program Files\Inkscape\bin\inkscape.exe
 * Download from https://inkscape.org/release/
 *
 * Usage
 * -----
 *   node branding/render-branding.mjs
 *
 * Run this after editing any SVG in branding/ to regenerate all PNGs and sync
 * them to their destinations in one step. The script is intentionally
 * dependency-free beyond Node.js built-ins + Inkscape.
 *
 * Adding a new asset
 * ------------------
 * Add a row to the ASSETS array below:
 *   ["source.svg", "output.png", width, height, ["dest/path1.png", ...]]
 * The deploy paths are relative to the repo root.
 */

import { execFileSync } from "child_process";
import { mkdirSync, cpSync, existsSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..");
const BRANDING = join(ROOT, "branding");
const INKSCAPE = "C:\\Program Files\\Inkscape\\bin\\inkscape.exe";

// [svgName, pngName, width, height, [...deployTo]]
const ASSETS = [
  // Roku channel icons
  ["roku-fhd-540x405.svg",  "roku-fhd-540x405.png",  540,  405, ["roku/images/roku-fhd-540x405.png"]],
  ["roku-hd-290x218.svg",   "roku-hd-290x218.png",   290,  218, ["roku/images/roku-hd-290x218.png"]],
  ["roku-sd-246x140.svg",   "roku-sd-246x140.png",   246,  140, ["roku/images/roku-sd-246x140.png"]],

  // Roku splash screens
  ["roku-splash-hd.svg",    "splash_hd.png",         1280, 720,  ["roku/images/splash_hd.png"]],
  ["roku-splash-fhd.svg",   "splash_fhd.png",        1920, 1080, ["roku/images/splash_fhd.png"]],

  // Branding / logos
  ["github-readme-banner.svg",           "github-readme-banner.png",           1280, 480, []],
  ["immich-lounge-horizontal-dark.svg",  "immich-lounge-horizontal-dark.png",  1280, 480,
    ["roku/images/immich-lounge-horizontal-dark.png",
     "docs/website/assets/immich-lounge-horizontal-dark.png"]],
  ["immich-lounge-horizontal.svg",       "immich-lounge-horizontal.png",       1280, 480,
    ["roku/images/immich-lounge-horizontal.png",
     "docs/website/assets/immich-lounge-horizontal.png"]],

  // App icon (all sizes)
  ["immich-lounge-icon.svg", "icon-16.png",   16,   16,  []],
  ["immich-lounge-icon.svg", "icon-32.png",   32,   32,  []],
  ["immich-lounge-icon.svg", "icon-64.png",   64,   64,  []],
  ["immich-lounge-icon.svg", "icon-128.png",  128,  128, []],
  ["immich-lounge-icon.svg", "icon-256.png",  256,  256, []],
  ["immich-lounge-icon.svg", "icon-512.png",  512,  512, ["docs/website/assets/icon-512.png"]],
  ["immich-lounge-icon.svg", "icon-1024.png", 1024, 1024, []],

  // Favicon
  ["favicon.svg", "favicon.png", 32, 32,
    ["companion/src/ImmichLoungeCompanion/wwwroot/favicon.png",
     "docs/website/assets/favicon.png"]],
];

function render(svgName, pngName, width, height, deployTo) {
  const src = join(BRANDING, svgName);
  const dst = join(BRANDING, pngName);

  if (!existsSync(src)) {
    console.log(`  SKIP   ${svgName} (not found)`);
    return;
  }

  process.stdout.write(`  render ${svgName} → ${pngName} (${width}×${height}) ... `);

  execFileSync(INKSCAPE, [
    "--export-type=png",
    `--export-filename=${dst}`,
    `--export-width=${width}`,
    `--export-height=${height}`,
    src,
  ], { stdio: "pipe" });

  console.log("done");

  for (const rel of deployTo) {
    const dest = join(ROOT, rel);
    mkdirSync(dirname(dest), { recursive: true });
    cpSync(dst, dest);
    console.log(`         → ${rel}`);
  }
}

console.log("Rendering branding assets with Inkscape...\n");
for (const [svg, png, w, h, deploy] of ASSETS) {
  render(svg, png, w, h, deploy);
}
console.log("\nDone.");

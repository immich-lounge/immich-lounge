# Browser favicon

`favicon.svg` is drawn specifically for browser tabs: a transparent background,
a large white TV frame, flat landscape colors, and a blue stand. Keep the
landscape simple enough to read at 16 pixels; the larger app and Roku artwork
retain their detailed illustrations.

After editing, run `node branding/render-branding.mjs --favicon-only` to render
the 32-pixel PNG with Inkscape and copy the PNG and SVG to the companion and
website. Modern browsers use the SVG; the companion also supplies a PNG fallback.

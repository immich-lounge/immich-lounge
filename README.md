# Immich Lounge

![Immich Lounge](./branding/github-readme-banner.png)

> Your memories, on the big screen.

Immich Lounge brings your immich photo library to Roku.

It turns a TV into a photo display with curated slideshows, a companion app for setup, and a separate Roku screensaver experience.

Website: [immich-lounge.github.io](https://immich-lounge.github.io/)

Immich Lounge is an independent project and is not affiliated with Roku or immich. They make great products, and Immich Lounge is built to work with them.

The companion currently has no authentication. It is intended for trusted home networks only. Do not expose it directly to the internet.

Roku install links:

- [Add Immich Lounge](https://my.roku.com/account/add/immichlounge)
- [Add Immich Lounge Screensaver](https://my.roku.com/account/add/immichloungesaver)

## What It Does

- Shows photos from your immich library on Roku
- Lets you create profiles for different slideshow setups
- Supports a main channel and a separate screensaver
- Includes overlays, photo motion, and background effects

## Screenshots

![Immich Lounge slideshow on Roku](./docs/website/assets/screenshots/slideshow.png)


## Getting Started

The usual setup is:

- add the companion service from [docker-compose.yml](./docker-compose.yml) to your existing Docker Compose stack
- start it on port `4383`
- open the companion and connect it to your immich server
- choose what to show
- open Immich Lounge on Roku

Full setup and configuration docs live in the website and docs.

## Documentation

- Website: [immich-lounge.github.io](https://immich-lounge.github.io/)

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md).

## License

This project is licensed under the [GNU Affero General Public License v3.0](./LICENSE).


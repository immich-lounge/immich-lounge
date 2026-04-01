---
icon: material/key-outline
---

# Immich API Key

Immich Lounge needs an API key from your immich account so the companion can read albums, people, tags, memories, and asset metadata, and so the Roku can fetch media directly from immich.

## Create the key in immich

In the immich web app:

1. Click your user menu in the top-right corner.
2. Open **Account Settings**.
3. Open the API keys section.
4. Create a new key for **Immich Lounge**.
5. Copy the key and paste it into the companion Connection page.

If your immich version supports scoped API keys, use a dedicated key for Immich Lounge instead of reusing a broader key.

Official immich docs:
[User Settings](https://docs.immich.app/features/user-settings/)

## Recommended permissions

If your immich version supports scoped API keys, this is the practical minimum set for most setups:

- `asset.read`
- `asset.statistics`
- `asset.view`
- `album.read`
- `person.read`
- `tag.read`
- `memory.read`

Add `asset.download` only if you plan to use **Original** image quality.

If your immich version does not support scoped permissions for API keys, create a normal user API key and keep it limited to the account whose library you want Immich Lounge to use.

## Where the key is used

- the companion stores the immich URL and API key in its settings
- profile files on disk do not contain the API key
- the Roku receives the API key through the enriched profile so it can fetch media directly from immich

Because of that, Immich Lounge is intended for trusted home networks only.

For more on where the key is stored and how the profile enrichment works, see [Architecture](./architecture.md).

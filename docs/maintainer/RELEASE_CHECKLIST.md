# Release Checklist

## Prepare the candidate

- Create a release branch from current `master`; keep unrelated checkout changes separate.
- Update both Roku manifests and the companion project version together. For 1.2.1, Roku uses `major_version=1`, `minor_version=2`, and `build_version=0001`.
- Move released entries into dated sections in both changelogs and keep their links intact.
- Run companion Release tests, Roku script tests, both builds/lints/static analyses, the physical-device suite, and the docs build.
- Run the Release workflow on the candidate branch with `release_name=v1.2.1` to build reviewable artifacts. This input labels downloads only; versions come from the companion project and both Roku manifests, which must agree. This workflow does not publish a GitHub release or update the stable Docker tag.
- Prepare a draft GitHub release with notes, artifact checksums, and the exact candidate commit. Roku build ZIPs are unsigned; Store packaging/signing is a separate step.

## Verify candidate artifacts

The Release workflow stores one candidate download containing companion, channel, screensaver, and docs ZIPs plus `SHA256SUMS`, using GitHub's official `actions/upload-artifact`, with signed build provenance from `actions/attest`. It attests all four ZIPs and `SHA256SUMS`. GitHub's upload digest covers its outer artifact archive; `SHA256SUMS` covers the four inner ZIPs.

Use a current [GitHub CLI](https://cli.github.com/manual/gh_attestation_verify) and select the successful candidate run and expected source commit/ref from its Actions page. Download into a new empty directory. In Bash (for example, WSL or Git Bash):

```bash
set -euo pipefail
repo=immich-lounge/immich-lounge
run_id=REPLACE_WITH_RUN_ID
candidate_sha=REPLACE_WITH_FULL_COMMIT_SHA
candidate_ref=refs/heads/REPLACE_WITH_CANDIDATE_BRANCH
mkdir candidate-artifacts
cd candidate-artifacts
gh run view "$run_id" --repo "$repo" --json headSha,headBranch,conclusion,url
gh run download "$run_id" --repo "$repo" --name REPLACE_WITH_CANDIDATE_LABEL --dir .
sha256sum --check --strict SHA256SUMS
for file in SHA256SUMS companion-release.zip roku-channel-build.zip roku-screensaver-build.zip docs-site.zip; do
  gh attestation verify "$file" --repo "$repo" \
    --signer-workflow "$repo/.github/workflows/release.yml" \
    --source-digest "$candidate_sha" --source-ref "$candidate_ref" \
    --deny-self-hosted-runners || exit 1
done
```

Confirm the run succeeded and its SHA and branch match the expected candidate before accepting the files. All four checksum checks and all five attestation verifications must succeed. Inspect the ZIP contents as part of release review. Provenance binds bytes to the source and workflow; Roku ZIPs still require separate Store signing and submission. Actions downloads expire according to repository retention settings, so retain the verified files if needed. After merging, rebuild and verify against the new master commit/ref.

## GitHub

- Merge the reviewed release PR into `master` and verify its exact commit.
- Confirm CI passes
- Confirm Pages deploy passes
- Confirm the docs site renders correctly
- Confirm the GHCR `edge` and commit-tagged image publishes and can be pulled.
- Run the Release workflow on the merged commit and replace draft assets if its source commit changed.
- Publish the draft release only after validation. Promote the companion to `latest` using Publish Docker Image with `publish_latest=true` on the verified commit.

## Companion

- Start the latest image with `./companion-data:/data`
- Open `http://<host-ip>:4383`
- Verify `http://<host-ip>:4383/healthz`
- Verify existing settings and profiles survive a container update

## Roku

- Verify `Immich Lounge` installs and launches
- Verify `Immich Lounge Screensaver` installs and launches
- Run `npm run analyze` and `npm run analyze:screensaver`, or run Roku's official Static Analysis Tool directly for both packaged apps, and review the findings
- Verify first-time setup
- Verify changing profile
- Verify changing companion
- Verify screensaver falls back to the channel profile when no screensaver profile is set
- Verify offline fallback behavior
- Verify a longer slideshow run for stability

## Store Metadata

- Final app descriptions
- Final screenshots and artwork
- Final privacy URL
- Final support URL
- Final terms URL
- Final signing and encryption setup

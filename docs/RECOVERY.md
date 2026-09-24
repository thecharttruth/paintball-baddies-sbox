# Source provenance

- Game: `solovolt.paintball-baddies`.
- Public build: **382169**, published September 17, 2026.
- Recovered September 24, 2026 from a pre-pivot local snapshot and checked against
  every non-generated file in the s&box public source viewer.
- 183 files checked after normalizing CRLF/LF and a leading UTF-8 BOM. The
  comparison used text length and FNV-1a fingerprints. 182 matched immediately.
  `TrainingHud.razor` differed only by three blank lines between its opening
  directives; these were restored to match the published viewer as well.
- `SOURCE_MANIFEST.json` records SHA-256 hashes of the recovered UTF-8/LF files.
  These are integrity hashes of this release, not a cryptographic signature from
  Facepunch. Generated `.obj/__compiler_extra.cs` and machine-specific project
  and launch files are deliberately excluded.
- The asset credits and third-party code notice were recovered from locally
  cached files whose content-addressed names and sizes match the published
  build manifest. The MIT license matches the project's existing notice.

This is a new clean source distribution. It contains no older private Git
history, private conversations, player telemetry or later sausage-game code.
It is not an export of every development tool or of the original art sources.

## Validation limits

All 183 recovered source file fingerprints match the public viewer. File hashes,
JSON, staged file types and a credential-pattern scan were checked locally.
The restored code also passed an isolated native s&box publishing compilation
on engine 26.09.22 with zero errors or warnings. This used a temporary project
identity and did not publish or replace the active development project. No
models or other runtime assets were supplied for that code compilation.
There has been no clean-PC gameplay restoration from this code-only repository;
required assets and scenes are not included. Native play/animation/network tests
on the later sausage prototype do not validate this historical build.

To check the recorded files locally, run `python tools/verify_source.py` with
Python 3. No game, network access or asset-generation API key is needed for this
integrity check. Changed files in a fork will intentionally fail the historical
snapshot check; document them as changes rather than calling them the original.

# How community work reaches the official game

The original creator maintains the official release. Contributors retain their
attribution and may publish independent mods/forks under the applicable licenses.

Use one public issue per submission as its review record. Track it through:

| Status | Meaning | Evidence needed to advance |
| --- | --- | --- |
| Submitted | A map, fix or improvement is proposed | Source/package link, author, purpose and license |
| In review | Scope, code and asset provenance are being checked | Review notes and requested changes resolved |
| Playtest | A candidate build is available for voluntary testing | Versioned build, reproducible test instructions and feedback |
| Accepted | Maintainer has approved official inclusion | Recorded approval, source revision and passed checks |
| Released | Shipped in the original game | Release version/date, change notes and author credit |
| Needs changes / Declined | A reason is recorded publicly | Specific explanation and, where useful, a path to reconsideration |

The repository's `community/registry.json` records accepted and released work.
Each entry links the issue, exact source revision, license, test evidence,
maintainer decision, and eventual release version. Empty registry means no
community submissions have yet been accepted. Editing the registry in a fork
does not approve a submission; only a maintainer-reviewed merge does.

On GitHub, use an issue board with these statuses, and protect the default branch
against direct contributor pushes. Configure these when the public repository is
created; these remote settings are not established merely by this document.

## Selection criteria

Favor fun, readable paintball combat, smooth control, reliable multiplayer, clear
collision/cover behavior, accessible controls and affordable rendering cost.
Maps need six viable spawn locations, alternate routes, usable standing/low cover,
AI navigation, painted surfaces and fair shield placement. Check sustained play,
not only the empty map's frame rate. Preserve the protected adult-female roster
and the established fitted clothing direction.

Compare a candidate with the current build on the same hardware. Check regression
risks, license/source availability, and download size. A popular but unfinished
submission can stay an optional community map while it improves. Keep experimental
balance/rules separate from the official rules until explicitly accepted.

After a release, link reports back to the originating submission. If a change
causes regressions, revert it in a new release and explain why without deleting
its history or attribution. No contributor is promised inclusion or a prize.

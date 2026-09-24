# Paintball Baddies — competition source release

Paintball Baddies is a third-person multiplayer paintball game created by
**SoloVolt Games**, a solo developer, for **s&box Game Jam III: One More Round**.
This repository fulfills the promise to share the game's code so others can
study it, fork it, report problems and propose improvements.

- [Play the published beta](https://sbox.game/solovolt/paintball-baddies)
- [The competition](https://sbox.game/jam/three)
- [Original public source viewer](https://sbox.game/solovolt/paintball-baddies/source)

## How it started, and why it changed

Paintball Baddies was SoloVolt Games' first implementation of an idea that had
previously existed only in discussions. The **One More Round** competition gave
the solo project a deadline and a clear challenge: make something people would
want to play again. The starting point was a cast of female paintballers,
responsive third-person cover combat, and short matches where everyone stayed
in the action instead of being eliminated. AI opponents filled empty seats so
the game could be played even without a full human lobby.

The public beta was a learning experience. Development and playtesting uncovered
rough edges in movement, animation, multiplayer and presentation. More features
alone did not make the core loop as satisfying as the creator wanted. Experiments
with collecting and banking tokens, cooperative targets and attacking drones
helped explore what was missing: clearer danger, stronger reactions to each shot,
and more memorable moments.

That search led to **Baddies vs Sausages**, a new direction built on the same
foundation. The setting moved toward a restaurant, with angry sausage enemies,
flying plates and eggs, destructible props, explosive arrows and freezing effects.
The aim became playful, chaotic arcade combat with a stronger sense of cause and
effect. This later prototype is still being developed and tested locally; it is
not included in this source snapshot or a claim of a finished public release.

This repository keeps the original Paintball Baddies chapter available and
fulfills the promise to share its competition code. Anyone can study it, fork it
under the applicable licenses, or suggest improvements. It records a starting
point and the lessons behind the pivot, rather than erasing the earlier game.

## What is here

The original Paintball Baddies code from public build **382169**, published on
September 17, 2026: 183 C#, Razor and stylesheet files. It includes multiplayer
sessions with AI filling empty seats, third-person cover, paintball impacts,
shields, knockdowns, configurable controls, voice chat, timed rounds and the
experimental Paint Heist mode.

This is a historical **beta source release**, with known rough edges. It is not
a claim of finished gameplay, bug-free multiplayer or competition results.
The later **Baddies vs Sausages** pivot is separate and is not included here.

## Code, assets and running the game

The code is released under the [MIT license](LICENSE). Adapted Facepunch NPC
code retains its [third-party notice](THIRD_PARTY_NOTICES.md).

**This is a code distribution, not a complete standalone game project.** The
original models, animations, maps, textures, sound recordings and engine binaries
are not bundled. They have separate licenses, and MIT does not relicense them.
The historical [asset credits](docs/PUBLISHED_ASSET_CREDITS.md) describe their
sources. Some art/audio used generative tools; these notices remain intact.

To play, use the published game in s&box. To work with the source:

1. Install s&box through Steam and open its editor.
2. Create your own game project and copy the `Code` directory into it. Let the
   editor generate machine-specific solution/project files.
3. Supply or replace the referenced assets and scenes using content you have
   permission to use. `docs/published-project.json` records the original project
   settings and startup scene; it is reference information, not a ready-to-run
   project file. Use your own publishing organization and identifier.
4. Compile and test in the native editor. The original build used engine protocol
   29; future s&box releases may require API adjustments.

The code alone will not recreate the original visuals or playable scene. There
is no promise that cloning this repository downloads the missing art. See
[source provenance and validation](docs/RECOVERY.md) for what was verified.

## Contributions

Bug reports and focused pull requests are welcome. Describe the problem, the
expected behavior and the tests you actually ran. See [CONTRIBUTING.md](CONTRIBUTING.md).
Forks are welcome under the applicable licenses; changes reach the official game
only after maintainer review. Contributions do not create an entitlement to jam
prizes, revenue or automatic inclusion. Credit is retained for accepted work.

A solo developer cannot promise response times, but clear reports make it easier
to improve the game. Please avoid posting personal information or credentials.

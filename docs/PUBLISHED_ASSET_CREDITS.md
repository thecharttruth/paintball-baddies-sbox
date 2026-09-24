# Asset credits and redistribution scope

Original Paintball Baddies code, tools and documentation are MIT licensed; see
LICENSE. Third-party code notices are in THIRD_PARTY_NOTICES.md. This document
does not replace or override an asset's original terms.

| Material | Creator/source | License and use |
| --- | --- | --- |
| Character effort D/E and knockdown voice recordings | CiciFyre, [Female RPG Voice Starter Pack](https://opengameart.org/content/female-rpg-voice-starter-pack) | CC0; edited/selected samples in `Assets/sounds/voices/` |
| Scoring rubbery pop and interface source sounds | [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0; edited selected scoring sample |
| Contact impact source sounds | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0; layered/edited contact effects |
| Paintball field recording | Joseph Sardin, [BigSoundBank paintball shots](https://bigsoundbank.com/paintball-tirs-s0528.html) | CC0; processed roster marker recordings |
| Marker mechanical source | Arq1998, [Freesound 408274](https://freesound.org/people/Arq1998/sounds/408274/) | CC0; processed roster marker recordings |
| Brick, concrete and metal source textures | ambientCG Bricks060, Concrete034, MetalPlates006; [license](https://docs.ambientcg.com/license/) | CC0; material channel/scale adaptations |
| Universal Animation Library, retained fallback locomotion | [Quaternius](https://quaternius.com/packs/universalanimationlibrary.html) | CC0; adapted/retargeted. Current Citizen presentation also uses native engine animation |
| Adapted NPC sensing/flank routines | Facepunch Sandbox, revision c872eb87cf7f6e7aecff0ad3bc203e3ab3953ba6 | MIT; notice retained in THIRD_PARTY_NOTICES.md |
| Citizen model/rig, clothing, animation sources and installed engine assets | Facepunch/s&box | Used and adapted for this s&box experience under the platform's terms; Facepunch retains ownership. Not relicensed under this project's MIT license. Loose redistribution outside s&box remains separate from this game release |
| Cloud lights/panel/material/vent | Facepunch industrial_wall_light, electricalindustrialpanel1, floorresinablend, airduct_a_vent_64x32 | CC0, verified through native Package.FetchAsync on September 14. Editable source restoration remains to be checked |
| Cloud sandbag and toolchest | [Facepunch sandbag](https://sbox.game/facepunch/sandbag), [toolchest](https://sbox.game/facepunch/toolchest) | No AssetLicense supplied by native package metadata on September 14. Retain as declared engine/cloud dependencies; do not bundle or relicense their raw sources without establishing permission |
| Meshy-created source models/texture variants | Creator-generated project assets using the paid Meshy API, then edited for s&box | Generative AI art created for this project with author-supplied/reference artwork. Meshy's ownership guidance permits ownership of paid/private generations; provenance remains in the development workspace |
| Generated reference and texture/paint artwork | Project artwork using OpenAI image generation, then edited/processed | Generative AI art. Distinguish original outputs from derivatives of existing assets |

Four additional quieter knockdown grunts in `sounds/voices/selected/` are user-supplied ElevenLabs generated audio from the paid-plan workflow confirmed September 15. They join the four CC0 knockdown recordings in the randomized event. These generated clips are project audio, not CC0 or MIT-licensed standalone recordings; original files and processing provenance are preserved outside the public package.

Voice, marker and scoring audition files that were not selected are development
references, not automatically cleared release assets. In particular the
CC BY-NC marker audition is not part of the chosen runtime roster recordings.
Do not copy audition folders wholesale into a public source package.

## Source release work still required

The author has requested full modifiability and an open-source release. Complete
the per-file source/license manifest, retain required notices, and resolve any
loose-source redistribution gaps before claiming the entire art bundle is open
licensed. If an asset cannot be redistributed, provide an authorized dependency
restore path or replace it with an openly licensed equivalent. Do not silently
apply MIT to third-party art or generated derivatives.

## Public beta platform scope — September 14

The beta is distributed as an s&box experience, with engine/cloud dependencies
resolved through the native publisher. It does not grant rights to reuse
Facepunch assets in another engine. Facepunch's [Citizen source documentation](https://sbox.game/dev/doc/assets/ready-to-use-assets/citizen-characters)
provides the base for character adaptations; its [s&box EULA](https://facepunch.com/legal/sbox/eula)
distinguishes creator experiences from the Facepunch property used within them.
The platform [modding guidelines](https://facepunch.com/legal/modding) retain
Facepunch ownership. No standalone Facepunch asset pack is offered here.

The native package manifest includes compiled runtime assets and the game code
archive. Cloud references retain their original identities and notices. Sandbag
is a Facepunch cloud reference used by an optional review component; the raw
development extraction folders are outside the game upload. Toolchest is not a
compiled-code cloud reference in this beta manifest.

Meshy's [ownership guidance](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models)
distinguishes paid/private generation from other cases. This project's generation
jobs used the user-authorized paid API. Runtime audio retains the CC0 sources
listed above and the paid-plan marker sounds described below; noncommercial
auditions are not included.

Public Beta title artwork and SoloVolt Games branding were created with OpenAI's
built-in image generation tool. Store gameplay screenshots are actual s&box
captures; the title card is branding artwork, not a gameplay render.

The portable development workspace retains detailed source receipts outside this
game folder. A future external source/art snapshot must include the needed
non-private license records and editable sources rather than private logs. This
in-platform beta release does not certify that all raw art can be redistributed
under an unrestricted open license.

## September 15 body-hit sounds and loading artwork

The eight light body-hit variants in `sounds/paintball/body/` use Kenney Impact
Sounds (CC0-1.0), processed with original synthetic air and paint-splat layers.
Source: https://kenney.nl/assets/impact-sounds . The original license and source
recordings are retained in the portable development workspace under
`source-assets/original-paintball-audio/body-hits-v2/sources/`; the processing
script is `tools/build_light_body_impact_auditions.py`. Soundsnap previews were
references only and are not included in this game.

The six-character loading illustration, `textures/ui/splash-roster-2026-09-15.png`,
was created with OpenAI image generation and approved by the creator. It is
promotional artwork, not a gameplay screenshot. Existing marker, shield and
environment-impact recordings retain the credits above.

## September 15 selected marker sounds

The five marker sounds in `sounds/paintball/selected/` were supplied by the
creator from ElevenLabs, with a confirmed paid plan permitting commercial use.
Source service and usage guidance: https://elevenlabs.io/sound-effects .
They replace the active marker-shot selection for human and AI players.
Original downloads are retained privately outside this game package. Runtime
copies use generic names and contain only PCM audio, with trimmed quiet tails,
boundary fades and matched levels. These audio files are not covered by the
MIT code license or offered as an independently reusable CC0 sound pack.

## Paint Heist prototype — September 16

Gold token pickups reuse Facepunch's installed Citizen `models/citizen_props/coin01.vmdl`.
The interface paint-drop and lock symbols are simple project-authored CSS shapes.
The bank ring and chip halos use the native LineRenderer; four approach lights reuse the development box and primary_white_emissive material. These visuals have no collision.
Four short interface cues in `sounds/heist/` adapt the existing Kenney Interface
Sounds pack (CC0): pluck_001, switch_003, drop_002 and confirmation_002.
The source pack, license and download record remain in
`source-assets/scoring-sound-options/library/`; the selected source clips and cue
notes are in `source-assets/heist-audio-2026-09-16/`. Existing marker and hit
confirmation sounds retain their earlier credits. No new paid assets were used.


## Native visual and movement polish — September 16

The player and AI graphs reuse Facepunch Citizen's existing Run_N_f clip for
forward running, with the existing directional, sprint, rifle and IK systems.
Bloom, LineRenderer, ParticleEffect and ParticleSpriteRenderer are built-in
s&box systems; deposit and shield glints use the native white texture. The
existing curved shield fracture meshes retain their original project provenance.
No new paid assets, copied third-party effects, or generated rigs were introduced.

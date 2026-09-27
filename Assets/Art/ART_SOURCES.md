# Lockstep Arena v3-A art sources

Only the selected assets listed below belong to the committed repository. Complete source packs remain external at `E:\unityproject\LockstepArena_ArtSource`; additional user-imported local candidates are deliberately not staged.

## Kenney UI Pack: Sci-fi 2.0

- Creator: Kenney
- Source: http://www.kenney.nl/
- License: Creative Commons Zero (CC0 1.0), http://creativecommons.org/publicdomain/zero/1.0/
- Source license file: `04_UI/kenney_ui-pack-space-expansion/License.txt`
- Original local source folder: `04_UI/kenney_ui-pack-space-expansion/PNG`
- Imported assets:
  - `Assets/Art/UI/SciFi/panel_dark_frame.png`
  - `Assets/Art/UI/SciFi/panel_glass.png`
  - `Assets/Art/UI/SciFi/button_primary.png`
  - `Assets/Art/UI/SciFi/button_secondary.png`
  - `Assets/Art/UI/SciFi/button_ready.png`
  - `Assets/Art/UI/SciFi/button_danger.png`
  - `Assets/Art/UI/SciFi/field_blue.png`
  - `Assets/Art/UI/SciFi/field_dark.png`
  - `Assets/Art/UI/SciFi/selection_highlight.png`

The button state treatment uses Unity UI color transitions over these source sprites; the pack does not provide separate hover and pressed files for the selected controls.

## Modular SciFi MegaKit — Standard free version

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Source license file: `01_Environment/Modular SciFi MegaKit[Standard]/License_Standard.txt`
- Original local source folder: `01_Environment/Modular SciFi MegaKit[Standard]/FBX (Unity)`
- Imported assets:
  - `Assets/Art/Environment/SciFiArena/Platform_Simple.fbx`
  - `Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx`
  - `Assets/Art/Environment/SciFiArena/Column_Simple.fbx`
  - `Assets/Art/Environment/SciFiArena/Prop_Computer.fbx`

## Sci-Fi Essentials Kit — Standard free version

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Source license file: `02_Props/Sci-Fi Essentials Kit[Standard]/License_Standard.txt`
- Original local source folder: `02_Props/Sci-Fi Essentials Kit[Standard]/FBX (Unity)`
- Imported assets:
  - `Assets/Art/Props/Prop_Crate.fbx`

All imported 3D models are decorative v3-A menu/lobby dressing. Lockstep Arena supplies its own URP materials at authoring time; no source-pack scripts, shaders, colliders, characters, rigs, or animations were imported.

## v3-B Styloo Robot Character

- Creator: Styloo
- Source: https://styloo.itch.io/robot-character
- License: Creative Commons Zero (CC0 1.0 Universal), as stated on the official asset page
- Original local source folder: `V3B/01_Player/StylooRobot/Styloorobotcharacte fbx and gltfr/FBXrobot`
- Imported assets:
  - `Assets/Art/Characters/StylooRobot/robot.fbx`
  - `Assets/Art/Characters/StylooRobot/robot_color.png`
  - `Assets/Art/Characters/StylooRobot/robot_emission.png`
  - `Assets/Art/Characters/StylooRobot/robot_normal.png`
  - `Assets/Art/Characters/StylooRobot/robot_metallic.png`

The FBX supplies the embedded `iddle`, `walking`, and `attackminiguns` clips used by v3-B. The glTF duplicate, preview images, roughness/bump duplicates, and unrelated clips were not copied.

## v3-B Quaternius Sci-Fi Gun Pack

- Creator: Quaternius
- Source: https://quaternius.com
- License: CC0 1.0 Universal
- Source license file: `V3B/02_Weapon/Quaternius_SciFiGun/License.docx`
- Imported asset:
  - `Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx`

Only the smallest readable blaster was selected. The remaining FBX, OBJ, and Blender variants stay in the external ArtSource folder.

## v3-B Kenney Particle Pack

- Creator: Kenney
- Source: https://kenney.nl/assets/particle-pack
- License: CC0 1.0 Universal
- Source license file: `V3B/06_VFX/Kenney_ParticlePack/kenney_particle-pack/License.txt`
- Imported assets:
  - `Assets/Art/VFX/Kenney/muzzle_03.png`
  - `Assets/Art/VFX/Kenney/spark_05.png`

The runtime uses restrained Unity Particle Systems. No particle collision or VFX gameplay logic is imported.

## v3-B Kenney Audio

- Creator: Kenney
- Source: https://kenney.nl/assets?q=audio
- License: CC0 1.0 Universal
- Source license files:
  - `V3B/07_Audio/Kenney_DigitalAudio/kenney_digital-audio/License.txt`
  - `V3B/07_Audio/Kenney_ImpactSounds/kenney_impact-sounds/License.txt`
  - `V3B/07_Audio/Kenney_SciFiSounds/kenney_sci-fi-sounds/License.txt`
  - `V3B/07_Audio/Kenney_UIAudio/kenney_ui-audio/License.txt`
- Imported assets:
  - `Assets/Art/Audio/Kenney/laserSmall_003.ogg`
  - `Assets/Art/Audio/Kenney/impactMetal_001.ogg`
  - `Assets/Art/Audio/Kenney/impactMetal_light_001.ogg`
  - `Assets/Art/Audio/Kenney/lowFrequency_explosion_000.ogg`
  - `Assets/Art/Audio/Kenney/tone1.ogg`
  - `Assets/Art/Audio/Kenney/twoTone1.ogg`
  - `Assets/Art/Audio/Kenney/powerUp3.ogg`
  - `Assets/Art/Audio/Kenney/click3.ogg`
  - `Assets/Art/Audio/Kenney/spaceEngineLow_000.ogg`

All complete source packs remain outside the repository at `E:\unityproject\LockstepArena_ArtSource\V3B`.

## v3-B user-authored combat arena

The authored `BattleScene.unity` uses a minimal subset of the Quaternius Modular SciFi MegaKit Standard pack. Its local `License_Standard.txt` explicitly identifies these models as CC0 1.0 Universal; that license accompanies the committed subset.

Base path: `Assets/Art/04_Arena/Quaternius_ModularSciFiMegaKit/Modular SciFi MegaKit[Standard]`.

- `FBX/Columns/Column_Hollow.fbx`
- `FBX/Columns/Column_Pipes.fbx`
- `FBX/Columns/Column_Simple.fbx`
- `FBX/Decals/Decal_Line_90.fbx`
- `FBX/Decals/Decal_Line_Bend1_R.fbx`
- `FBX/Decals/Decal_Logo.fbx`
- `FBX/Decals/Decal_Logo_Letters.fbx`
- `FBX/Platforms/Door_Frame_A.fbx`
- `FBX/Props/Prop_AccessPoint.fbx`
- `FBX/Walls/WallWindow_Corner_Square_Inner.fbx`
- FBX-referenced textures: `Textures/T_Decals.png`, `T_PaddedWall_BaseColor.png`, `T_Trim_01_BaseColor_Red.png`, `T_Trim_01_Normal.png`, and `T_Trim_02_BaseColor_Red.png`.
- Matching asset/folder `.meta` files and `License_Standard.txt` are included.

The remaining FBX variants, OBJ/glTF copies, preview files, unused textures, and the locally imported Sci-Fi Essentials pack are not committed. No source-pack gameplay scripts are included. Embedded model materials/texture references are retained for this authored scene; these are not gameplay authority.

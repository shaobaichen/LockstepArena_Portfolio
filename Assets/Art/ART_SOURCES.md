# Lockstep Arena 美术资源来源说明（v3-A / v3-B）

仓库只提交了下文列出的已选用素材。完整原始素材包仍保存在外部目录 `E:\unityproject\LockstepArena_ArtSource`；其他由用户导入的本地候选素材没有加入 Git 版本管理。

## Kenney 科幻 UI 素材包（Sci-fi 2.0）

- 作者： Kenney
- 来源： http://www.kenney.nl/
- 授权： Creative Commons Zero (CC0 1.0), http://creativecommons.org/publicdomain/zero/1.0/
- 原始许可证文件： `04_UI/kenney_ui-pack-space-expansion/License.txt`
- 原始本地素材目录： `04_UI/kenney_ui-pack-space-expansion/PNG`
- 实际导入的素材：
  - `Assets/Art/UI/SciFi/panel_dark_frame.png`
  - `Assets/Art/UI/SciFi/panel_glass.png`
  - `Assets/Art/UI/SciFi/button_primary.png`
  - `Assets/Art/UI/SciFi/button_secondary.png`
  - `Assets/Art/UI/SciFi/button_ready.png`
  - `Assets/Art/UI/SciFi/button_danger.png`
  - `Assets/Art/UI/SciFi/field_blue.png`
  - `Assets/Art/UI/SciFi/field_dark.png`
  - `Assets/Art/UI/SciFi/selection_highlight.png`

按钮状态变化通过 Unity UI 的颜色过渡作用于这些原始 Sprite 实现；所选按钮并没有单独的 Hover 或 Pressed 图片文件。

## Quaternius 模块化科幻场景素材包（Modular SciFi MegaKit，免费 Standard 版）

- 作者： Quaternius (`@Quaternius`)
- 来源： https://quaternius.com
- 授权： CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- 原始许可证文件： `01_Environment/Modular SciFi MegaKit[Standard]/License_Standard.txt`
- 原始本地素材目录： `01_Environment/Modular SciFi MegaKit[Standard]/FBX (Unity)`
- 实际导入的素材：
  - `Assets/Art/Environment/SciFiArena/Platform_Simple.fbx`
  - `Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx`
  - `Assets/Art/Environment/SciFiArena/Column_Simple.fbx`
  - `Assets/Art/Environment/SciFiArena/Prop_Computer.fbx`

## Quaternius 科幻道具素材包（Sci-Fi Essentials Kit，免费 Standard 版）

- 作者： Quaternius (`@Quaternius`)
- 来源： https://quaternius.com
- 授权： CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- 原始许可证文件： `02_Props/Sci-Fi Essentials Kit[Standard]/License_Standard.txt`
- 原始本地素材目录： `02_Props/Sci-Fi Essentials Kit[Standard]/FBX (Unity)`
- 实际导入的素材：
  - `Assets/Art/Props/Prop_Crate.fbx`

上述导入的 3D 模型仅用于 v3-A 菜单和大厅的装饰。工程在场景制作时使用自行配置的 URP 材质；没有从这些原始素材包导入脚本、Shader、碰撞体、角色、骨骼绑定或动画。

## v3-B Styloo 机器人角色

- 作者： Styloo
- 来源： https://styloo.itch.io/robot-character
- 授权： Creative Commons Zero (CC0 1.0 Universal), as stated on the official asset page
- 原始本地素材目录： `V3B/01_Player/StylooRobot/Styloorobotcharacte fbx and gltfr/FBXrobot`
- 实际导入的素材：
  - `Assets/Art/Characters/StylooRobot/robot.fbx`
  - `Assets/Art/Characters/StylooRobot/robot_color.png`
  - `Assets/Art/Characters/StylooRobot/robot_emission.png`
  - `Assets/Art/Characters/StylooRobot/robot_normal.png`
  - `Assets/Art/Characters/StylooRobot/robot_metallic.png`

v3-B 使用该 FBX 自带的 `iddle`、`walking` 和 `attackminiguns` 动画片段。没有复制重复的 glTF 模型、预览图、粗糙度/凹凸贴图的重复版本以及无关动画。

## v3-B Quaternius 科幻枪械素材包

- 作者： Quaternius
- 来源： https://quaternius.com
- 授权： CC0 1.0 Universal
- 原始许可证文件： `V3B/02_Weapon/Quaternius_SciFiGun/License.docx`
- 实际导入的素材：
  - `Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx`

枪械仅选用了体量最小且轮廓易于辨认的一款能量武器。其余 FBX、OBJ 和 Blender 版本保留在外部 ArtSource 素材目录中。

## v3-B Kenney 粒子特效素材包

- 作者： Kenney
- 来源： https://kenney.nl/assets/particle-pack
- 授权： CC0 1.0 Universal
- 原始许可证文件： `V3B/06_VFX/Kenney_ParticlePack/kenney_particle-pack/License.txt`
- 实际导入的素材：
  - `Assets/Art/VFX/Kenney/muzzle_03.png`
  - `Assets/Art/VFX/Kenney/spark_05.png`

运行时使用较为克制的 Unity Particle System 粒子表现；没有导入依赖粒子碰撞的游戏规则或特效驱动的战斗逻辑。

## v3-B Kenney 音频素材

- 作者： Kenney
- 来源： https://kenney.nl/assets?q=audio
- 授权： CC0 1.0 Universal
- 原始许可证文件：
  - `V3B/07_Audio/Kenney_DigitalAudio/kenney_digital-audio/License.txt`
  - `V3B/07_Audio/Kenney_ImpactSounds/kenney_impact-sounds/License.txt`
  - `V3B/07_Audio/Kenney_SciFiSounds/kenney_sci-fi-sounds/License.txt`
  - `V3B/07_Audio/Kenney_UIAudio/kenney_ui-audio/License.txt`
- 实际导入的素材：
  - `Assets/Art/Audio/Kenney/laserSmall_003.ogg`
  - `Assets/Art/Audio/Kenney/impactMetal_001.ogg`
  - `Assets/Art/Audio/Kenney/impactMetal_light_001.ogg`
  - `Assets/Art/Audio/Kenney/lowFrequency_explosion_000.ogg`
  - `Assets/Art/Audio/Kenney/tone1.ogg`
  - `Assets/Art/Audio/Kenney/twoTone1.ogg`
  - `Assets/Art/Audio/Kenney/powerUp3.ogg`
  - `Assets/Art/Audio/Kenney/click3.ogg`
  - `Assets/Art/Audio/Kenney/spaceEngineLow_000.ogg`

所有完整原始素材包仍保留在仓库外部目录 `E:\unityproject\LockstepArena_ArtSource\V3B`。

## v3-B 自行搭建的战斗竞技场

手工搭建的 `BattleScene.unity` 只使用 Quaternius Modular SciFi MegaKit Standard 素材包中的少量模型。原始 `License_Standard.txt` 明确标注这些模型采用 CC0 1.0 Universal 授权，该许可证随所提交的素材子集一起保留。

基础路径： `Assets/Art/04_Arena/Quaternius_ModularSciFiMegaKit/Modular SciFi MegaKit[Standard]`.

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
- FBX 引用的贴图： `Textures/T_Decals.png`, `T_PaddedWall_BaseColor.png`, `T_Trim_01_BaseColor_Red.png`, `T_Trim_01_Normal.png`, and `T_Trim_02_BaseColor_Red.png`.
- 同时包含与资源及文件夹匹配的 `.meta` 文件和 `License_Standard.txt`。

其余 FBX 变体、OBJ/glTF 副本、预览文件、未使用贴图和本地导入的 Sci-Fi Essentials 素材包均未提交。仓库不包含这些素材包自带的游戏玩法脚本。为了保留手工场景的外观，模型原有材质和贴图引用继续保留，但它们**不负责决定游戏战斗结果**。

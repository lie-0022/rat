# Naming Conventions — [TBD]

## 스크립트 (C#)

### 클래스 suffix 규칙

| suffix | 용도 | 예시 |
|--------|------|------|
| `Controller` | 입력·흐름 제어 | `PlayerController`, `CameraController` |
| `System` | 게임 규칙 처리 | `XPSystem`, `GoldSystem`, `WeaponSystem` |
| `Component` | 데이터 컨테이너 | `HealthComponent` |
| `Manager` | 싱글턴 조율자 | `GameManager` |
| `UI` | UI 전용 스크립트 | `SkillSelectionUI`, `HUDController` |
| `SO` | ScriptableObject | `SkillDefinitionSO`, `EnemyStatsSO` |
| `Spawner` | 오브젝트 생성 | `WaveSpawner` |
| `Dealer` | 효과 적용자 | `DamageDealer` |

### 기타 규칙
- 클래스명: PascalCase
- private 필드: `_camelCase`
- public 프로퍼티: PascalCase
- 메서드: PascalCase
- 이벤트: `On` + PascalCase (예: `OnPlayerDied`, `OnGameStateChanged`)
- 네임스페이스: 사용 안 함

---

## 씬 (Scenes/)

| 씬 | 용도 |
|----|------|
| `MainMenu.unity` | 시작 화면, 캐릭터 선택 |
| `GamePlay.unity` | 실제 게임 (Stage 1~3 전부) |
| `GameOver.unity` | 결과 화면 (킬 수, 생존 시간) |

---

## 모델 (Art/Models/)

```
{대상}_{종류}_{변형}

Player_Swordsman
Enemy_Goblin
Enemy_Goblin_Elite
Environment_Rock
Environment_Tree
```

---

## 애니메이션 (Art/Animations/)

```
{대상}_{동작}

Player_Idle
Player_Run
Player_Dash
Player_Jump
Player_Attack
Enemy_Idle
Enemy_Run
Enemy_Attack
Enemy_Death
```

---

## VFX (Art/VFX/)

```
VFX_{효과명}

VFX_HitSpark
VFX_LevelUp
VFX_DashTrail
VFX_Death
VFX_ProjectileTrail
```

---

## 머티리얼 (Art/Materials/)

```
MAT_{대상}_{설명}

MAT_Player_Body
MAT_Enemy_Goblin
MAT_Environment_Ground
```

---

## UI 스프라이트·아이콘 (Art/UI/)

```
UI_{종류}_{이름}

UI_Icon_Skill_Slash
UI_Icon_Skill_Fireball
UI_BG_SkillPanel
UI_Button_Confirm
UI_Frame_Card_Common
UI_Frame_Card_Epic
UI_Frame_Card_Unique
UI_Frame_Card_Legend
```

---

## 프리팹 (Prefabs/)

```
{도메인}_{이름}

Player_Swordsman
Enemy_Goblin
Enemy_Goblin_Elite
Projectile_Arrow
Projectile_MagicBolt
VFX_HitSpark
VFX_LevelUp
UI_HUD
UI_SkillSelectionPanel
```

---

## 데이터 ScriptableObject (Data/)

```
{대상}_{이름}_Data

Player_Swordsman_Data       → Data/Player/
Enemy_Goblin_Data           → Data/Enemy/
Enemy_Goblin_Elite_Data     → Data/Enemy/
Skill_Slash_Data            → Data/Skills/
Skill_Fireball_Data         → Data/Skills/
```

---

## 오디오 (Audio/)

```
BGM_{스테이지or상황}       → Audio/BGM/
SFX_{대상}_{동작}          → Audio/SFX/

BGM_Stage1
BGM_Stage2
BGM_Stage3
SFX_Player_Dash
SFX_Player_Jump
SFX_Enemy_Hit
SFX_LevelUp
SFX_Chest_Open
```

---

## 씬 계층 구조 (Scene Hierarchy)

> 출처: `dev-conventions.md` §4 "씬은 빈 부모로 그룹화". 2026-06-13 Prototype 씬 정리 시 확정.
> 목적: 루트에 오브젝트가 흩어지지 않게 하고, Hierarchy에서 즉시 찾도록 한다.

### 규칙

1. **빈 GameObject 디바이더**로 최상위를 그룹화한다. 형식: `---그룹명---` (앞뒤 대시 3개, 영문 PascalCase).
2. 디바이더는 **원점·회전0·스케일1**(identity)로 둔다. 자식 재배치는 항상 world position 유지(`SetParent(parent, true)`).
3. 디바이더에는 **컴포넌트를 붙이지 않는다**(순수 그룹용).
4. 표준 최상위 그룹 7종(필요한 것만 사용):

| 그룹 | 용도 | 예 |
|------|------|-----|
| `---Cameras---` | 카메라/시네머신 | Main Camera, CM_TopDown |
| `---Environment---` | 맵 비주얼·라이팅 | Directional Light, MapEnvironment |
| `---Managers---` | 비주얼 없는 로직/싱글턴 | Systems, RoundManager, ScoreTracker, MainHall |
| `---Onboarding---` | 인트로/튜토리얼 연출 | IntroSequence, Spline |
| `---Player---` | 플레이어 리그 | Player |
| `---World---` | 월드 인터랙션 오브젝트 | Cauldron, WaterSource, GatherNodes, Merchant, Monster_*, Obstacle_*, Zone_* |
| `---UI---` | 모든 Canvas/UITK 패널 + EventSystem | UICanvas, HUD, *UI 패널, EventSystem |

### 오브젝트 이름

- 인스턴스: **{도메인}_{이름}** (프리팹 규칙과 동일). 예: `Monster_A`, `Obstacle_Lava`, `Zone_Fire`, `Gather_Fire_불꽃사리`.
- UI 패널 GameObject 이름은 **붙은 컨트롤러/역할과 일치**시킨다. 예: Dialogue UI는 `DialogueUI`(이전 `DialogueSystem` ✗).
- "Sandbox/Temp/Test" 등 **개발 잔재 접두는 금지** — 정식 역할명으로 바꾸거나 제거.
- 외부 임포트 에셋(예: `MapEnvironment` 하위 dungeon 프랍)은 **원본 이름 유지** — 그룹 안에 있으면 충분히 찾을 수 있고, 내부 메시까지 리네임하는 비용 대비 이득이 없다.

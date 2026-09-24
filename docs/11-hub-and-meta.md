# 11. 허브·메타·저장 (W8–9)

## Hub 씬 = 로비

- 쥐구멍 안 아지트. 최대 4인이 돌아다니는 **플레이 가능한 로비** (레포·피크 방식).
- 배치물: 출발 지점(전원 모이면 출발), 자판기(상점), 거울(스킨), 도감 책, 연습용 전리품 몇 개.
- 호스트가 만든 세션에 친구가 스팀 초대로 합류 → Hub에 스폰. 런 시작 = **출발 발판에 전원이 올라서면** 카운트다운 후 출발 (2026-09-14 결정 — 귀환과 같은 방식, 레버 없음).

### 단계 구현 (2026-09-14 — 기지·맵 분리)

1. 기지 씬 기본: 벽으로 둘러싼 작은 방 + 스폰 4개. **호스트 시작(DevNetHud Host 버튼·`-autohost`) = 기지 로드** (샌드박스 직행 폐지 — 샌드박스는 에디터에서 직접 열어 테스트용으로만).
2. 스테이지 씬 `Stage_Warehouse01` (맵 1개, 구역 3개는 6단계)
3. 기지 출발 발판(`World/DeparturePad`, 호스트 권한): 다운 안 된 전원이 발판 위 → departCountdownSeconds 카운트다운(이탈 시 취소) → 스테이지 씬 로드 → RunManager가 전원 로드 완료(OnLoadEventCompleted) 후 PlayerSpawn으로 순간이동(소유 클라 RPC) + 자동 출발. 집합 판정은 쥐구멍 귀환과 공용(`World/GatherCheck`) / 4. 귀환·전멸 → 결과 화면 뒤 기지 로드 (전원 드랍·상태 초기화, 기지 도착 시 PlayerSpawn으로 이동 — `Player/PlayerPlacement` 공용) / 5. 기지 누계 표시 (`DeparturePad.TotalValue` 복제 — 저장값, 스테이지 번호 없음)

## 상점 = 자판기 (2026-09-14 구현 — 스탯 업그레이드)

- `World/VendingMachine` (NetworkObject, IInteractable "상점", 홀드 0) — 기지 서쪽 벽 앞. E로 열면 그 클라에만 `UI/ShopPanel`(uGUI) 표시. 열면 커서 해제(시선·클릭은 lockState로 자동 정지), Esc/닫기로 잠금.
- **통화 = 팀 누계(HaulTotal)**. 레포식이라 업그레이드도 팀 공유·호스트 저장(`SaveData.Upgrades`). 구매 판정·차감·저장은 호스트만(`PurchaseServerRpc`), 패널은 `HaulTotal`·`Levels` NetworkVariable만 읽는다.
- 효과 적용: `Player/PlayerUpgrades` NetworkVariable<UpgradeLevels> — 호스트가 스폰 시·구매 시 전원에게 쓴다. 이동·스태미나·던지기는 소유 클라가 배율 적용, 인벤 칸은 서버가 `ServerEnsureSlots`(늘기만 함).
- 레벨당 수치는 BalanceConfigSO `업그레이드` 헤더 (BaseCarrySlots 2, MoveSpeed +8%/Lv, Stamina +20%/Lv, Throw +15%/Lv). 항목은 `Data/Upgrades/UpgradeSO` ×4:

| 항목 (Effect) | 설명 | 최대 Lv | 가격 (Lv1 → 이후 +) |
|---|---|---|---|
| 주머니 확장 (CarrySlots) | 인벤 칸 +1 (3·4키 전환, HUD 슬롯 자동 확장) | 2 | 150 → +150 |
| 튼튼한 다리 (MoveSpeed) | 걷기·달리기 +8% | 3 | 100 → +100 |
| 큰 허파 (StaminaMax) | 최대 스태미나 +20% | 3 | 80 → +80 |
| 힘센 앞발 (ThrowPower) | 던지기 속도 +15% (궤적선도 반영) | 2 | 120 → +120 |
| 통통 뒷발 (JumpPower) | 점프 임펄스 +10% (높이 약 +21%) | 3 | 100 → +100 |

- 장비 4종(아래 표)·소모품·치즈코인은 아직 미구현 — 업그레이드 상점이 먼저 (사용자 요청: 스탯·인벤 증가 필수).
- 연습용 전리품: 기지 동쪽 구석에 GrayBox_S ×2 + Test_Phone (씬 배치, `PracticeLoot`).

## 거울·도감 (2026-09-14 구현 — 1차)

- **거울** `World/Mirror` (북쪽 벽 오른쪽, IInteractable "거울") → `UI/MirrorPanel`: 기본(팀 색) + `Data/Skins` 4종(노란·검은·흰·분홍 쥐, 틴트만·모자 없음). 착용 = `Player/PlayerSkin` NetworkVariable<FixedString32Bytes>(소유 클라 쓰기) → 전 클라 `PlayerVisual.SetBodyColor`. 개인 저장 `SaveData.EquippedSkinId`. **도전과제 미구현이라 전부 해금** (AchievementId 빈 값).
  - **진짜 거울 (2026-09-14, `World/MirrorReflection`)**: 각 클라 로컬 평면 반사. 거울 면 뒤로 뒤집은 눈 위치에서 거울 사각형을 창으로 삼는 off-axis 카메라로 방을 1024px 텍스처에 그려 유리 Quad(URP Unlit)에 입힌다. 근평면 = 거울 면(액자·벽 자동 제외). 1인칭이라 내 몸은 평소 그림자 전용 → 반사 카메라가 그리는 동안만 보이게 해서 스킨 확인 가능. 눈이 10m 밖이거나 거울이 화면에 없으면 갱신 안 함.
- **도감** `World/CodexBook` (북쪽 벽 왼쪽, "도감") → `UI/CodexPanel` (ScrollRect): ItemDatabase 중 Id `loot_*` 20종. 해금 = 정산 이벤트(EventBus.LootDeposited, 호스트) → 각 플레이어의 `Player/PlayerCodex`가 자기 소유 클라에 ClientRpc → `SaveData.UnlockedCodexIds`에 저장. 미해금은 "???". 토스트는 아직 없음.
- 상호작용 프롬프트 HUD가 아직 없어 각 배치물 위에 월드 TMP 라벨 "[E] 거울/도감/쥐 상점".

## Meta/MetaWallet.cs + SaveService

```csharp
public class SaveData   // JSON 직렬화 (Application.persistentDataPath/save.json)
{
    public int Version = 1;
    public int CheeseCoin;
    public List<string> UnlockedCodexIds;
    public List<string> UnlockedSkinIds;
    public List<string> OwnedEquipmentIds;
    public string EquippedSkinId;
    public List<string> CompletedAchievementIds;
    public Dictionary<string,int> Stats;   // "runs","extracts","deposits","revives","eggsDelivered"...
}
```

- **저장은 로컬 개인별** (각자 자기 코인·스킨). 런 보상 지급은 호스트가 액수만 ClientRpc → 각 클라가 자기 저장에 반영.
  (치팅 가능하지만 코옵 게임 — 신경 쓰지 않는다. v1 결정)
- SaveService: Load(부트 시)/Save(변경 시 debounce 2s)/백업 1개(save.bak). Version 필드로 마이그레이션 대비.
- **2026-09-14 1차 구현**: `Core/SaveService`(정적) + `SaveData { Version, HaulTotal }`. 팀 누계(HaulTotal)는 **호스트 기기에만** 저장(세션 주인 기준, 레포식) — 귀환 정산 시 즉시 저장(지금은 1회라 debounce 없음). 전멸해도 누계 유지·초기화 없음. 코인·스킨 등 개인 필드는 메타 단계에서 추가.

## 장비 (Data/EquipmentSO)

```csharp
public class EquipmentSO : ScriptableObject
{
    public string Id; public string Name; public int Price;
    public EquipmentEffect Effect; public float Value; public float Penalty;
}
public enum EquipmentEffect { CarrySlots, MoveSpeed, ClimbAssist, DarkVision }
```

| Id | 이름 | 가격 | 효과 | 페널티 |
|---|---|---|---|---|
| eq_backpack | 등짐 주머니 | 150 | Small 아이템 2개 소지 (잡기 상태로 등에 부착) | 이동 -5% |
| eq_sneakers | 운동화 | 200 | 이동 +12% | 발소리 loudness ×1.6 |
| eq_hooktail | 갈고리 꼬리 | 250 | 낙하 데미지 무효 + 6m 낙하 제한 해제 | — |
| eq_vest | 야광 조끼 | 180 | 어둠 페널티 무효(본인 시야) | 고양이 viewDistance +25% (본인 한정) |

- 장비는 1개만 착용. 효과 적용 지점: PlayerController/CarryController가 시작 시 SO 읽어 수치 보정.
- 트레이드오프가 전부 06·07 시스템 수치로 표현됨 — 별도 시스템 불필요.

## 스킨 (Data/SkinSO) — 도전과제 해금, 판매 아님

```csharp
public class SkinSO : ScriptableObject
{
    public string Id; public string Name;
    public Color TintColor;            // 셰이더 틴트 (모델 1개 재사용)
    public GameObject HatPrefab;       // 머리 소켓 부착 (null 가능)
    public string AchievementId;       // 해금 조건
}
```

- 동기화: 플레이어 NetworkVariable<FixedString32> SkinId → 전 클라 적용.

## 도전과제 (Meta/AchievementService)

```csharp
public class AchievementSO : ScriptableObject
{ public string Id; public string Title; public string Desc; public string StatKey; public int Target; public SkinSO RewardSkin; }
```

초기 10종 (Stats 키와 1:1):

| Id | 조건 (StatKey ≥ Target) | 보상 |
|---|---|---|
| ach_first_extract | extracts 1 | 스킨: 노란 쥐 |
| ach_egg_courier | eggsDelivered 3 (한 런, 특수 집계) | 모자: 계란 껍질 |
| ach_ghost | 무발각 존 클리어 1 | 모자: 골무 |
| ach_medic | revives 10 | 모자: 휴지 망토 |
| ach_deep_rat | 존4 도달 | 스킨: 검은 쥐 |
| ach_rich | 누적 코인 1000 | 모자: 병뚜껑 |
| ach_chef_enemy | 치킨 정산 1 | 모자: 치즈 헬멧 |
| ach_survivor | 솔로 귀환 1 | 스킨: 흰 쥐 |
| ach_collector | 도감 15종 | 모자: 반지 왕관 |
| ach_troll | 동료를 던진 아이템으로 비틀거리게 30회 | 모자: 광대 |

- EventBus 구독으로 Stats 가산 → Target 도달 시 해금 + 토스트 UI. 스팀 도전과제 연동은 P1(출시 전).

## 수용 기준 (W9)

- [ ] Hub에서 4인 대기→레버→런→결과→Hub 루프 — (2026-09-24 고양이 53) **2인 ✅**: 기지 출발 발판에 둘 다 → 0.5s 뒤 카운트다운(2/2) → 2.8s 뒤 스테이지 StageActive → 쥐구멍 집합 3.0s 뒤 귀환 → 결과 뒤 8.2s에 Hub, 둘 다 접속·에러 0. 4인 미검증
- [ ] 코인으로 장비·소모품 구매, 효과가 실제 수치에 반영 (운동화 발소리 증가 확인)
- [ ] 스킨 해금→착용→상대 화면에 반영 — (고양이 53) 착용→반영 ✅: 호스트가 skin_yellow 착용 → 빌드 클라 "스킨 연출: client 0 → skin_yellow", 기본으로 되돌림도 반영. 해금 조건 경로는 미검증
- [x] 저장 파일 삭제 후 새 시작, 손상된 JSON에도 크래시 없이 초기화 (고양이 53: 본 파일·백업 둘 다 없음 → 새 저장(0) / 쓰레기·잘린 JSON → 에러 로그 1줄 + 새 저장 / 본 파일 손상·백업 정상 → 백업(4321) 복구 / 타입 틀림 → 기본값, 전부 크래시 0. 테스트 뒤 원래 저장 복구)

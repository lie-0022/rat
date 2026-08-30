# 08. 전리품·경제 (W5–6)

## Data/LootItemSO.cs — 아이템 데이터 원장

```csharp
[CreateAssetMenu(menuName = "RatGame/Loot Item")]
public class LootItemSO : ScriptableObject
{
    public string Id;                 // "loot_egg" — 저장·도감 키. 절대 변경 금지
    public string DisplayName;        // "계란"
    public string CodexFlavor;        // 댕청한 도감 한 줄 (B급 텍스트)
    public int BaseValue;             // 정산 가치
    public float Mass;                // 물리 질량 (05 표와 일치)
    public ItemTrait Traits;
    public GameObject Prefab;         // CarryableItem 프리팹
    public LootTier Tier;             // Small / Tricky / Large / Special
    public Sprite Icon;               // 도감·HUD
}
```

- `ItemDatabase` SO가 전체 목록 보유 — Id→SO 조회, NetworkVariable에는 Id 해시(int)만 실어 보낸다.

## 초기 아이템 목록 (데모 20종 — 이 표대로 SO 에셋 생성)

| Id | 이름 | Tier | Value | Mass | Traits |
|---|---|---|---|---|---|
| loot_cracker | 크래커 | Small | 10 | 0.4 | — |
| loot_candy | 사탕 | Small | 12 | 0.3 | — |
| loot_coin | 동전 | Small | 15 | 0.5 | — |
| loot_bottlecap | 병뚜껑 | Small | 8 | 0.3 | — |
| loot_grape | 포도알 | Small | 10 | 0.4 | Rolling |
| loot_cheese_bit | 치즈 조각 | Small | 14 | 0.6 | Edible |
| loot_egg | 계란 | Tricky | 40 | 2.0 | Fragile+Rolling |
| loot_jelly | 젤리 | Tricky | 35 | 1.8 | Wobbly |
| loot_plate | 접시 | Tricky | 45 | 2.5 | Fragile+Slippery |
| loot_soap | 비누 | Tricky | 30 | 1.5 | Slippery |
| loot_can | 참치캔 | Tricky | 38 | 3.0 | Rolling |
| loot_lightbulb | 전구 | Tricky | 42 | 1.2 | Fragile |
| loot_cheese_wheel | 치즈 덩어리 | Large | 120 | 10 | Edible |
| loot_baguette | 바게트 | Large | 90 | 8 | — |
| loot_ham | 햄 덩어리 | Large | 110 | 11 | — |
| loot_watermelon | 수박 조각 | Large | 160 | 18 | Slippery |
| loot_chicken | 로스트치킨 | Large | 200 | 22 | — |
| loot_ring | 반지 | Special | 250 | 0.3 | — (고양이 스팟 옆 스폰 고정, 10 문서) |
| loot_phone | 스마트폰 | Special | 220 | 2.5 | Alarming |
| loot_watch | 회중시계 | Special | 180 | 1.5 | Alarming |

## 정산 — World/DepositZone.cs (쥐구멍)

```csharp
public class DepositZone : NetworkBehaviour
{
    // 호스트: OnTriggerEnter(Carryable 레이어)
    //  1) Downed 플레이어 몸 → RunManager.ServerRevive(clientId)  (09)
    //  2) CarryableItem → 가치 = BaseValue × DurabilityMul → RunManager.ServerDeposit(value)
    //     아이템 디스폰 + 흡입 연출 ClientRpc + EventBus.LootDeposited
}
```

- 던져 넣어도 인정 (쥐구멍 슛 플레이 허용 — 단 Fragile은 던지면 깨져서 가치가 깎이는 자연 페널티).
- 정산된 가치는 **팀 공유** (개인 소유 없음 — 협동 마찰 제거).

## 지갑 구조

| 지갑 | 위치 | 수명 | 용도 |
|---|---|---|---|
| RunWallet (DepositedValue) | RunManager NetworkVariable | 런 | 할당량 판정 |
| CheeseCoin (메타) | MetaWallet + SaveService | 영구 | 상점 구매 |

- 런 종료(탈출 성공) 시: `CheeseCoin += 총정산가치 × 탈출보너스(1.0 + 0.15×클리어존수)` — 호스트가 계산, 전원 동일 지급.
- 전멸 시: 이미 정산한 가치의 30%만 지급 (완전 0은 라이트 유저에게 가혹 — "그래도 조금 벌었다").

## 소모품 (상점 구매 → 런에 들고 입장, 11 문서 상점과 연결)

| Id | 이름 | 가격(코인) | 효과 (07 유인 표와 일치) |
|---|---|---|---|
| use_yarn | 털실뭉치 | 30 | 던지면 고양이 Distracted 8s |
| use_catnip | 캣닢 | 80 | 설치, 접근 시 15s 무력화 |
| use_crumbs | 쿠키 부스러기 | 20 | 순찰 경로 유도 |

- 소지 방식: 런 시작 시 각자 소모품 슬롯 2칸. 사용 = 던지기와 동일 입력 흐름 (별도 UI 없이 숫자키 1·2 선택).

## 도감 연동

- 최초 정산 시 `ProgressionService.UnlockCodex(lootId)` (호스트 → 전 클라 ClientRpc → 각자 저장).
- 도감 데이터는 CodexEntrySO가 아니라 LootItemSO 재사용 (중복 원장 금지).

## 수용 기준 (W6)

- [ ] 표의 20종 SO + 임시 프리미티브 프리팹 생성, 스폰→운반→정산 전 루프
- [ ] 계란을 던져 넣으면 깨져서 0원, 굴려 넣으면 40원 재현
- [ ] 전멸 vs 탈출의 코인 차이 로그 검증
- [ ] 털실뭉치 구매→입장→사용 플로우

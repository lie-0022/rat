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
| loot_phone | 스마트폰 | Special | 220 | 5 | Alarming (5kg — 샌드박스 끌기 검증용으로 올림, 커밋 5c1f400. 2026-09-24 표 정정) |
| loot_watch | 회중시계 | Special | 180 | 1.5 | Alarming |
| loot_hairball | 헤어볼 | Small | 5 | 0.3 | Slippery — **스폰 안 됨, 고양이가 그루밍 뒤 20%로 뱉는다** (2026-09-24, docs/07). 도감: "고양이가 준 선물. 받기 싫었다." |

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
| 스테이지 적립 (StashedValue) / 런 누계 (RunTotalValue) | RunManager NetworkVariable + RunSession | 스테이지 / 런 | 귀환 정산 (docs/09) |
| CheeseCoin (메타) | MetaWallet + SaveService | 영구 | 상점 구매 |

- 메타 보상(치즈코인 적립) 규칙은 미정 — 레포식 루프(docs/09)에 맞춰 새로 정한다.
- 전멸 시: 이미 정산한 가치의 30%만 지급 (완전 0은 라이트 유저에게 가혹 — "그래도 조금 벌었다").

## 소모품 (상점 구매 → 런에 들고 입장, 11 문서 상점과 연결)

| Id | 이름 | 가격(코인) | 효과 (07 유인 표와 일치) |
|---|---|---|---|
| use_yarn | 털실뭉치 | 30 | 던지면 고양이 Distracted 8s |
| use_catnip | 캣닢 | 80 | 설치, 접근 시 15s 무력화 |
| use_crumbs | 쿠키 부스러기 | 20 | 순찰 경로 유도 |

- 소지 방식 (2026-09-15 변경): **별도 소모품 칸 없음 — 인벤 슬롯(1~4키, 상점 주머니 확장)에 일반 물건처럼 들어간다.** 사용 = 선택 슬롯으로 들고 던지기(우클릭 홀드) 입력 흐름 그대로. 인벤이 1~4키를 쓰게 되면서 docs의 "소모품 1·2키"와 겹쳐 합침 (docs/12 와이어프레임 결정).

## 도감 연동

- 최초 정산 시 `ProgressionService.UnlockCodex(lootId)` (호스트 → 전 클라 ClientRpc → 각자 저장).
- 도감 데이터는 CodexEntrySO가 아니라 LootItemSO 재사용 (중복 원장 금지).

## 수용 기준 (W6)

- [x] 표의 20종 SO + 임시 프리미티브 프리팹 생성, 스폰→운반→정산 전 루프 (2026-09-24 고양이 51: loot_ SO 21개(표 20 + 헤어볼) 모두 프리팹 있음, 쥐구멍에 스폰 → 전부 기본 가치 그대로 적립 21/21. 운반은 docs/05 검증(고양이 47))
- [ ] 계란을 던져 넣으면 깨져서 0원, 굴려 넣으면 40원 재현 — (고양이 51) 굴려 넣기 ✅ +40. ⚠ 던져 넣기도 +40 — 착지 충돌 5~7 m/s라 내구도 0.72까지만 깎이고 금도 안 감(docs/05 공식). docs/05 계란 던지기와 같은 사용자 판단 대기
- [x] 털실뭉치 구매→입장→사용 플로우 — (2026-09-25 고양이 90) **새 루프 목적지 상점 경로로 확인**: 스테이지 1 판매대에서 털실 구매(남은 식량·초과분 −40) → 스테이지 2 출발방에 택배 → 집기 → 고양이 쪽 4.3m 던지기(실제 던지기 RPC) → "털실뭉치 발동" → 고양이 Distracted, 털실 소모, 예외 0. 기지 자판기의 치즈코인 소모품은 여전히 없음(판단 부탁 4 — 추천: 목적지 상점이 소모품 담당)

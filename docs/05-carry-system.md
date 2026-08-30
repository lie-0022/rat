# 05. 운반 시스템 (W3–4) — 게임의 코어, 가장 공들일 것

## 설계 요약

- 아이템은 호스트에서만 물리 시뮬 (03 문서).
- 잡기 = 호스트가 아이템의 GripPoint와 플레이어의 HandAnchor 사이에 **스프링 성격의 조인트** 생성.
- 여러 명이 잡으면 조인트가 여러 개 — 힘이 자연히 합산되어 "다인 운반"이 물리로 공짜 구현된다.
- 무게 초과 상태는 조인트 힘 부족으로 "질질 끌림"이 자연 발생 — 이것이 코미디의 원천.

## World/CarryableItem.cs

```csharp
public class CarryableItem : NetworkBehaviour, IInteractable
{
    [SerializeField] private LootItemSO _data;          // 가치·무게·트레잇 (08 문서)
    [SerializeField] private Transform[] _gripPoints;    // 잡기 지점 (1~4개, 아이템 크기 따라)
    public NetworkList<ulong> CarrierIds;                // 현재 잡은 클라 (호스트 기록)
    public NetworkVariable<float> Durability;            // 0~1, 파손 트레잇만 사용

    // 호스트 전용
    public bool ServerTryGrab(ulong clientId, int gripIndex);   // 검증+조인트 생성
    public void ServerRelease(ulong clientId, bool thrown);
    public void ServerReleaseAll();                              // 다운·정산 시
    private void OnCollisionEnter(Collision c);                  // 소음+파손 판정 (호스트)
}
```

### 조인트 사양 (호스트에서 생성)

- `ConfigurableJoint`: 아이템(GripPoint 로컬 앵커) ↔ 플레이어 손 위치.
- Linear Drive: spring `grabSpring=600 × 플레이어수 보정`, damper 40, maxForce `grabMaxForce=80`.
- Angular: slerp drive 약하게 (spring 50) — 물건이 대충 손 방향을 향하되 출렁이게.
- **핵심 규칙: maxForce는 아이템 mass와 무관하게 고정.** 무거우면 힘이 부족해 끌리는 게 의도.

### 무게 → 이동 페널티 (PlayerCarryController가 소유 클라에서 적용)

```
totalMass   = 아이템 mass
carriers    = CarrierIds.Count
loadPerRat  = totalMass / carriers
speedMul    = Clamp01(1.2f - loadPerRat / heavyThreshold)   // heavyThreshold = 6
             → loadPerRat 1.2 이하: 페널티 없음
             → loadPerRat 6 이상: speedMul 0.2 (기다시피)
sprint 금지  = loadPerRat > 3
점프 금지    = loadPerRat > 4.5
```

### 아이템 무게·GripPoint 기준표 (초기값 — LootItemSO에 저장)

| 분류 | 예시 | mass | grip 수 | 권장 인원 |
|---|---|---|---|---|
| 소형 | 크래커, 동전, 병뚜껑 | 0.3~0.8 | 1 | 1 |
| 중형 | 계란, 사과, 컵 | 1.5~3 | 1~2 | 1 (느림) |
| 대형 | 치즈 덩어리, 바게트 | 8~12 | 2~3 | 2 |
| 특대 | 수박 조각, 로스트치킨 | 16~24 | 4 | 3~4 |

## 잡기 판정 플로우 (03 문서의 RPC와 연결)

1. 소유 클라: Grab 홀드 시작 → 카메라 전방 SphereCast(r 0.35, d 1.0, Carryable 레이어)
2. 가장 가까운 미점유 GripPoint 선택 → `GrabRequestServerRpc(netId, gripIndex)`
3. 호스트 검증: 거리 < 1.2m(관용치), GripPoint 비어 있음, 플레이어 Active 상태, 빈손
4. 성공: CarrierIds 추가, 조인트 생성, `CarriedItemNetId` 갱신 → 전 클라 잡기 연출
5. Grab 릴리즈 or 거리 3m 초과(끊김) or Downed → ServerRelease

한 플레이어는 **한 번에 한 아이템만** (소형 아이템 다중 소지는 장비 '등짐 주머니'로 확장 — 11 문서).

## 던지기

- Throw 홀드로 차지 0→1 (1.2s 만충). 궤적 프리뷰는 소유 클라 로컬로만 (점선 10개).
- `ThrowRequestServerRpc(dir, charge)` → 호스트: 조인트 해제 + `AddForce(dir * lerp(2, 9, charge) * massDamp, Impulse)`
  massDamp = `Clamp01(3 / mass)` — 무거운 건 못 던진다(굴리기 유도).
- 던진 아이템에 맞은 플레이어: 0.5s 비틀거림(연출만, 데미지 없음) — 트롤 허용 지점.

## 파손 (Fragile 트레잇)

- OnCollisionEnter (호스트): `impact = collision.relativeVelocity.magnitude`
- impact > fragileBreakSpeed(5 m/s) → Durability -= (impact-5)*0.15
- Durability < 0.5: 금 간 비주얼(머티리얼 스왑) + 가치 50%
- Durability <= 0: 파괴 — 파편 파티클 + **소음 loudness 60** + 가치 0. EventBus.LootBroken.

## 트레잇 시스템 (LootItemSO.Traits — Flags enum)

```csharp
[Flags] public enum ItemTrait { None=0, Fragile=1, Rolling=2, Wobbly=4, Slippery=8, Alarming=16, Edible=32 }
```

| 트레잇 | 구현 |
|---|---|
| Rolling (계란·캔) | CapsuleCollider/SphereCollider + 낮은 angular drag(0.05). 놓으면 굴러간다. 잡기 중 각도 드라이브 끔 |
| Wobbly (젤리) | 조인트 damper 1/4로 — 출렁임 증폭. + 스케일 출렁 셰이더(연출) |
| Slippery (접시·비누) | 잡기 유지 중 3s마다 12% 확률로 강제 Release (호스트 판정) + "미끌!" 이펙트 |
| Alarming (스마트폰·방울) | 잡기/충돌 시 소음 loudness 50 즉시 발생 |
| Edible (치즈류) | Interact 홀드로 먹기 — 스태미나 회복, 아이템 소멸 (04 문서) |

## 다운된 동료 운반 (04·09 연결)

- Downed 플레이어의 래그돌 루트에 `CarryableItem`(mass 3, grip 2, Traits=None) 활성화.
- 쥐구멍 DepositZone 진입 시 정산이 아니라 **부활 처리** (09 문서 분기).

## 수용 기준 (W4)

- [ ] 소형: 잡고 뛰어도 안정. 대형: 1인 시도 시 질질 끌림 + 스태미나 소모, 2인 정상 운반
- [ ] 4인이 특대 아이템을 동시에 잡고 계단 내려오기 성공 (녹화해서 SNS 1호 소재로)
- [ ] 계란: 던지면 깨짐, 굴리면 안전. 접시: 가끔 미끄러짐 재현
- [ ] 다운 동료 운반→쥐구멍 부활 플로우
- [ ] 전 과정 MPPM 4인 + 스팀 원격 1회 검증, 아이템 위치 어긋남이 게임을 깨지 않음

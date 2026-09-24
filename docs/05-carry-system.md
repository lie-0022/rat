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

- Throw 홀드로 차지 0→1 (1.2s 만충). 궤적 프리뷰는 소유 클라 로컬로만 (점선 10개, 손 쪽 폭 0.008 → 끝 0.05 — 눈앞 시작이라 굵으면 화면을 가림). 처음 부딪히는 지점(Linecast)에서 선을 끊는다 — 바닥 아래 점까지 그리면 화면 아래로 휘어 갈고리처럼 보임.
- **던지는 방향 (1인칭, 2026-09-14)**: 손(HandAnchor)이 화면 오른쪽 아래라 시선과 평행하게 던지면 손 오프셋만큼 옆에 떨어진다 → 카메라 조준선이 닿는 점(레이캐스트, `throwAimMinDistance` 1.5m ~ `throwAimMaxDistance` 6m로 제한)을 향해 손에서 던지고 위로 0.15 보정.
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
- 구현 (2026-09-24, 고양이 46): 플레이어 몸은 소유 클라 권한이라 호스트 조인트로 끌 수 없어 **호스트 소유 대리 몸**(`World/DownedBody` 프리팹 — `CarryableItem`, 데이터 `downed_body`: Large·mass 3·가치 0)을 띄운다. `Player/PlayerDownedBody`: Downed가 되면 호스트가 몸 생성, 모든 클라에서 쥐 본체 콜라이더·렌더러 끔(소유 클라 rb는 kinematic), 소유 클라 시점은 0.1s마다 몸 위치로 순간이동 RPC. 대형처럼 자동 자리로 끈다(혼자 가능, 느림). `DepositZone`이 대리 몸이면 정산 대신 `ServerRevive` → 몸 자리에 쥐를 세우고 몸 제거. 몸은 주인 털빛을 칙칙하게.

## 수용 기준 (W4)

- [ ] 소형: 잡고 뛰어도 안정. 대형: 1인 시도 시 질질 끌림 + 스태미나 소모, 2인 정상 운반
- [ ] 4인이 특대 아이템을 동시에 잡고 계단 내려오기 성공 (녹화해서 SNS 1호 소재로)
- [ ] 계란: 던지면 깨짐, 굴리면 안전. 접시: 가끔 미끄러짐 재현
- [x] 다운 동료 운반→쥐구멍 부활 플로우 (고양이 46, 2인: 클라 다운 → 대리 몸 생성·본체 콜라이더 꺼짐 → 호스트가 잡아(대형) 1.6s에 2.2m 끎·클라 시점 0.4m 안 따라감 → 쥐구멍에 넣자 0.01s 부활·몸 제거·쥐구멍에 섬, 클라 예외 0)
- [ ] 전 과정 MPPM 4인 + 스팀 원격 1회 검증, 아이템 위치 어긋남이 게임을 깨지 않음

## 자동 대형 (2026-09-13 팀 결정 — 대형 아이템 잡기 규칙, 위 "잡기 판정 플로우"의 대형 분기를 대체)

- 대형(Large/Special 티어)은 **잡는 자리(CarrySlot)**를 가진다. 콜라이더의 **긴 수평 변 양쪽 면 중앙**에 2자리를 런타임 자동 계산 — 아이템마다 수작업 없음.
- 잡기 요청 → 호스트가 자리 배정: 아무도 없으면 **가장 가까운 자리**, 누가 있으면 **점유 자리에서 가장 먼 자리(반대편)**. 자리가 다 차면 거부.
- 배정되면 소유 클라가 그 자리로 짧게 미끄러져 붙는다(이동 중엔 그 물건과 충돌 무시 — 반대편 자리로 가로지르기). **혼자 잡아도 동일.**
- 조인트: 아이템 쪽 앵커 = 자리의 서는 점, 쥐 쪽 앵커 = 몸 수직축(앞뒤 오프셋 0), **각도 드라이브 0**.
  → 이동·몸 방향은 계속 카메라 기준인데도 쥐가 몸을 돌려 물건이 휘둘리지 않는다. 물건 방향은 자리들의 위치가 결정.
- 들림 여부는 기존대로 질량 vs 인원 힘 합산 (혼자면 끌림, 충분하면 들림).

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| carrySlotStandDistance (물건 면 → 쥐 중심) | 0.45 m |
| carrySlotSnapTime (자리로 붙는 시간) | 0.2 s |
| 대형 자리 수 | 2 (폰 기준 — 긴 변 양쪽) |

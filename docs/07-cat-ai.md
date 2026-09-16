# 07. 고양이 AI (W5–6)

## 구성 (Cat 프리팹)

```
Cat (NetworkObject, NavMeshAgent, CapsuleCollider)
 ├ CatBrain      — FSM (호스트에서만 Update)
 ├ CatSenses     — 시야·청각 → 의심 게이지
 ├ CatMovement   — NavMeshAgent 래퍼 (속도·목적지)
 ├ CatAnimatorLink
 └ NetworkTransform (호스트 권한)
```

클라에서는 Brain/Senses/NavMeshAgent 전부 disable — NetworkTransform 보간 + 상태 연출만.

## FSM

```csharp
public enum CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return }
```

```
Sleep ──(소음 ≥40 근접)──▶ Suspicious
Patrol ──(의심게이지 ≥30)──▶ Suspicious ──(게이지 ≥100 or 직접목격 1s)──▶ Chase
   ▲                            │(게이지 0)                                │
   └────────── Return ◀─────────┘                     (타깃 접촉)──▶ Capture ──▶ Return
Distracted: 털실뭉치 등 아이템 트리거 → 8s 후 이전 상태로
```

| 상태 | 행동 | 이동속도 |
|---|---|---|
| Sleep | 지정 스팟에서 수면. 감각 민감도 30% | 0 |
| Patrol | 웨이포인트 순회 (방마다 2~4개, 랜덤 대기 2~5s) | 2.0 |
| Suspicious | 마지막 자극 지점으로 이동, 주변 3m 배회 6s | 3.0 |
| Chase | 타깃 추적. 시야 상실 3s 후 마지막 목격점 조사 → Suspicious | 5.5 |
| Capture | 앞발 스윙 애니 0.4s → 범위 내 플레이어 Downed 처리 | — |
| Distracted | 유인 아이템 위치에서 놀기 | 4.0 |
| Return | 순찰 경로 복귀 | 2.0 |

플레이어 sprint 7 > Chase 5.5 → **직선으로는 도망칠 수 있다.** 잡히는 건 막다른 곳·스태미나 고갈·문 닫힘 때. 이 관계는 밸런스의 축이므로 변경 시 신중히.

## CatSenses

### 시야 (Update 0.2s 간격, 호스트)

```
foreach player(Active만):
  거리 ≤ viewDistance(8m, 웅크림 시 4m)?
  시야각 ≤ 70° (좌우 35)?
  Linecast 차단물 없음?
  → 목격: 의심게이지 += 목격시간 × 60/s × 거리보정(가까울수록 큼)
직접목격 1s 지속 or 거리 2m 내 목격 → 즉시 Chase
```

- 어둠: 방의 LightZone 볼륨(트리거) 안이면 viewDistance 50% — "어두운 곳이 안전"을 시스템으로.
- 게이지는 자극 없을 때 -20/s. NetworkVariable로 복제 → 타깃 플레이어 HUD에 "?"/"!" 표시.
  - HUD 구현 (2026-09-16, `UI/SuspicionIndicatorWidget`): **나를 쫓는 고양이**(Chase && TargetClientId == 나) → 빨간 "!", 없으면 **가장 가까운 Suspicious 고양이** → 주황 "?" + 게이지(Gauge/chaseThreshold). 의심은 "지점"에 대한 것이라 타깃이 없어 누구에게나 보인다. 남을 쫓는 고양이는 표시 안 함. 주의: TargetClientId 기본값 0 = 호스트 Id — Chase가 아닐 때만 0이 쓰이므로 State와 함께 읽어야 한다.

### 청각

- NoiseSystem.OnNoise 구독 (06 계약): `게이지 += loudness × 거리감쇠 × 0.8`
- Break/Trap/Squeak 타입은 즉시 Suspicious(해당 지점).

## Capture 판정

- Chase 중 타깃과 거리 ≤ 1.0m → Capture 상태: 0.4s 애니 후 전방 반경 1.2m OverlapSphere
- 명중: 해당 플레이어 `PlayerCondition = Downed` (04 문서) → 고양이는 3s 그루밍(승리 연출) 후 Return
- 회피 가능: 0.4s 사이에 점프/방향전환으로 빠질 수 있게 (판정은 애니 종료 시점 위치 기준)

## 유인 아이템 연동 (08 상점 아이템)

| 아이템 | 효과 |
|---|---|
| 털실뭉치 | 던진 낙하지점으로 Distracted 8s |
| 캣닢 | 반경 3m 진입 시 Distracted 15s (한 번만) |
| 쿠키 부스러기 | 설치 지점을 Patrol 웨이포인트에 임시 삽입(1회 경유) |

구현: `CatLure` 컴포넌트(호스트) — 활성 시 모든 CatBrain에 등록, 우선순위 Chase보다 낮게 (추격 중엔 안 속음).

## 스폰·수량

- ZoneDefinitionSO에 catCount (Zone1:1 → Zone3+:2). CatSpawn 태그 지점에서 호스트 스폰.
- NavMesh는 방 모듈 프리팹에 미리 굽지 못하므로 **런타임 베이크**: ZoneGenerator 완료 후 `NavMeshSurface.BuildNavMesh()` (com.unity.ai.navigation). 방 3~4개 규모면 1~2초, 로딩 화면에서 처리.

## 연출 계약 (클라)

- CatState NetworkVariable OnValueChanged → CatAnimatorLink가 애니·사운드 재생:
  Suspicious: 귀 쫑긋+"냐?" / Chase: 낮은 그르렁+BGM 전환(EventBus) / Capture: 앞발 스윙
- 상태가 항상 소리로 먼저 들려야 한다 — 시야 밖 고양이의 상태를 소리로 읽는 것이 은신 플레이의 정보 구조.

## 수용 기준 (W6)

- [ ] 그레이박스 방 2개에서: 순찰→소음 조사→목격 추격→포획→복귀 풀 사이클
- [ ] 웅크려 뒤로 지나가면 미발각, 달리면 발각 재현
- [ ] 추격 중 문 뒤로 도망→3s 후 포기 확인
- [ ] 털실뭉치로 추격 전 고양이 유인 성공
- [ ] 4인 세션에서 고양이 위치·상태 동기화 자연스러움

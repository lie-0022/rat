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
| Sleep | 잠자리(Bed 스팟)에서 60s. **얕은 잠 12~20s(감각 0.5) → 예고 3s → 깊은 잠 10~18s(0.15) → 예고 3s(꼬리 씰룩 = "나가" 신호) → 얕은 잠…** 파동 (2026-09-24, design/cat-ideas/08). 얕은 잠 중 게이지 ≥30 또는 깨짐·함정·찍찍 소음 → HalfAwake 3s(감각 0.5): 그 사이 목격·새 자극이면 Suspicious, 없으면 다시 잔다. 깊은 잠은 게이지 ≥60 또는 깨짐류만. 작은 자극은 자는 동안 잊는다(깬 뒤 엉뚱한 조사 방지). 잠은 얕은 잠에서만 끝난다 | 0 |
| Patrol | **스팟 그래프** 순회 (2026-09-24, `AI/CatSpot`): 가중치 랜덤·최근 2개 제외 → 도착 후 종류별 머무름 — Look 2~5s / Food 25s(감각 0.5) / Sun 40s(0.5) / Groom 20s(0.6) / Bed → Sleep 45s 뒤 깸(다음 스팟으로). CatSpot이 없으면 `CatWaypoint*`를 Look으로. design/cat-design/02 카탈로그 | 2.0 |
| Suspicious | 마지막 자극 지점으로 이동, 주변 3m 배회 6s | 3.0 |
| Chase | **예측 추격** (2026-09-24): 타깃 속도(위치 차분)로 1s 앞 지점을 노림(NavMesh 샘플, 최대 5.5m). 시야 상실 시 마지막 진행 방향으로 **3m 오버슛** 뒤 마지막 목격점, 3s 후 Suspicious. **코너 감속**: 몸 방향과 경로 방향이 어긋나면 속도 ×0.55까지 — 직선은 최고 속도, 지그재그는 실제로 도움. Agent angularSpeed 240·acceleration 8 | 5.5 (직선) |
| Capture | 앞발 스윙 애니 0.4s → 범위 내 플레이어 Downed 처리 | — |
| Distracted | 유인 아이템 위치에서 놀기 | 4.0 |
| Return | 순찰 경로 복귀 | 2.0 |

플레이어 sprint 6 > Chase 5.5 → **직선으로는 도망칠 수 있다.** 잡히는 건 막다른 곳·스태미나 고갈·문 닫힘 때. 이 관계는 밸런스의 축이므로 변경 시 신중히.

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

## 성격 프로필 (2026-09-24, `Data/CatPersonalitySO`, design/cat-ideas/01)

FSM은 같고 배율만 다르다. 스폰 시 프리팹의 프로필 목록에서 랜덤(`PersonalityIndex` NV, `ServerSetPersonality`로 지정 가능). 몸 색이 그레이박스 표현.

| 프로필 | 시야 | 청각 | 추격 속도 | 수면 | 잠자리 선호 | 관찰점 선호/머무름 | 특이 | 색 |
|---|---|---|---|---|---|---|---|---|
| 게으름뱅이 | 0.8 | 0.7 | 1.1 | 1.5× | ×2 | — | 추격 8s 뒤 포기(Return) | 회갈색 |
| 사냥꾼 | 1.3 | 0.9 | 1.0 | 0.5× | — | ×1.5 / ×3 | — (선반 Perch는 층이 생기면) | 진회색 |

- 배율은 `CatSenses.ViewMultiplier/HearingMultiplier`, `CatBrain`의 추격 속도·수면·스팟 가중치·관찰점 머무름에 곱. 새 프로필은 에셋만 추가.
- `CatBrain.ServerWake()` — 자는 고양이를 깨워 순찰로(집주인 부르기·테스트용).
- 검증: 9m 정면에서 게으름뱅이 못 봄(시야 6.4) / 사냥꾼 목격→추격(10.4). 게으름뱅이 추격 8s 뒤 포기(시야 상실 3s가 먼저 오면 그쪽이 우선). 사냥꾼 수면 40s(30 + 얕은 잠 마무리), 게으름뱅이 107s.

## 데모 레이아웃 (2026-09-24)

`Tools/RatGame/Cat/Build Demo Layout (Stage_Warehouse01)`: 스팟 6(Bed·Food·Sun·Groom·Look×2, 쥐구멍 반대편에 잠자리·밥)·시야 가림 상자 4·고양이 1(잠자리에서 시작)·NavMesh 베이크. 방 모듈 전 검증용 — 선반 층·틈은 다음 단계(design/cat-design/02-6). 검증: 4배속 6분 관찰에서 스팟 14회 방문(같은 스팟 연속 없음), Bed 수면 45.0s 뒤 깸, Food 머무는 동안 감각 0.5. 참고: 쥐가 쥐구멍 위에 서 있으면 포획→쥐구멍 부활→재포획이 반복된다(쥐구멍 부활 사양의 자연스러운 결과).

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

- 그레이박스 텔레그래프 (2026-09-24, `AI/CatVisual`, 전 클라 읽기): 몸 색(잠 갈색·의심 노랑·추격 빨강·포획 진빨강·유인 하늘) + 꼬리 피벗(얕은 잠 천천히 / 깊은 잠→얕은 잠 예고 급씰룩 / 의심·추격 빠름) + 깊은 잠 배 펄스·몸 가라앉음 + HalfAwake 머리 들림. `SleepPhase` NetworkVariable로 단계 복제. 소리는 오디오 단계에서 같은 계약으로.
- CatState NetworkVariable OnValueChanged → CatAnimatorLink가 애니·사운드 재생:
  Suspicious: 귀 쫑긋+"냐?" / Chase: 낮은 그르렁+BGM 전환(EventBus) / Capture: 앞발 스윙
- 상태가 항상 소리로 먼저 들려야 한다 — 시야 밖 고양이의 상태를 소리로 읽는 것이 은신 플레이의 정보 구조.

## 수용 기준 (W6)

- [ ] 그레이박스 방 2개에서: 순찰→소음 조사→목격 추격→포획→복귀 풀 사이클
- [ ] 웅크려 뒤로 지나가면 미발각, 달리면 발각 재현
- [ ] 추격 중 문 뒤로 도망→3s 후 포기 확인
- [ ] 털실뭉치로 추격 전 고양이 유인 성공
- [ ] 4인 세션에서 고양이 위치·상태 동기화 자연스러움

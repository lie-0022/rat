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
public enum CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return, Search, Curious, Track, Toy, Blunder, Away, Fight }
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
| Search | **숨을 곳 수색** (2026-09-24, `AI/CatBrain.Search.cs`, design/cat-ideas/14): Chase 시야 상실 3s 뒤 마지막 목격점 반경 6m에 HideSpot이 있으면 진입(없으면 Suspicious). 타깃이 Hidden이 되면 Return이 아니라 시야 상실로 처리. 가까운 순 최대 3곳 → 출구 앞 킁킁 2s(여럿 숨었으면 × 인원 × 1.5) → 30% 건드림 → 안의 쥐 발각(출구로 튀어나옴) → Pounce 창 0.5s → Chase. 25s 또는 스팟 소진 → Return. 목격·소음·미끼가 끊는다 | 3.0 |
| Track | **냄새 추적** (2026-09-24, `AI/CatBrain.Track.cs`, design/cat-ideas/06): Patrol·Return 중(우선순위 Suspicious 아래·Curious 위) 반경 3m 안 강도 ≥8 자국을 맡으면 진입 — 시야 불필요. 그 자국 → 같은 쥐의 다음 자국 순으로 따라가며 자국마다 킁킁 0.5s. 목격·자극이면 끊김. 자국이 끊기면 끝점 3m 안 HideSpot이 있으면 Search, 없으면 Return. 따라간 자국(쥐별 순번)은 다시 안 쫓음 | 3.0 |
| Curious | **호기심 앞발** (2026-09-24, `AI/CatBrain.Curious.cs`, design/cat-ideas/03): Patrol·Return 중 시야에 속도 ≥1.5 m/s로 움직이는 풀린 물건(안 들림·주머니 아님·대형 아님)이 보이면 진입 — 의심·추격·잠 중엔 안 속음. 물건을 따라가 수평 0.9m 안에서 앞발 3~5회(0.6s 간격, 정면 ±60° 수평 + 위, 속도 변화 1.2 m/s — 호스트 물리) → 하품 5s → Return. 그 물건은 10s 무시, 같은 물건 누적 30s면 질려서 60s 무시. 8s 안에 못 닿으면 포기, 쥐가 집으면 즉시 Return. 자기가 친 물건의 충돌 소음은 무시(깨짐은 예외) | 4.0 |
| Capture | 앞발 스윙 애니 0.4s → 범위 내 플레이어 Downed 처리. **움직일 수 있는 동료(Active·Hidden·Stunned)가 있으면 Downed 대신 Pinned + Toy** (2026-09-24) | — |
| Fight | **앙숙 싸움** (2026-09-24, `AI/CatBrain.Fight.cs` + `AI/CatRelation`, design/cat-ideas/07): 고양이 2마리 이상일 때 호스트 중재자가 0.25s마다 판정 — 둘 다 Patrol·Return·Suspicious·Curious·Track·Search, 4m 안, 가림 없음 → 70% 싸움 15s / 30% 째려보고 지나감(10s 재판정 금지). 싸움 뒤 30s 재발 금지. 싸우는 동안 가운데 1.2m를 1s마다 빙글(3.0), 청각 차단(`CatSenses.Deaf`)·시야 ×0.2. 2m 안 쥐 목격이면 싸움 깨고 Chase, 상대가 빠지면 같이 끝. 관계는 지금 앙숙 고정(엄마·아기·짝꿍은 다음) | 3.0 |
| Away | **집주인이 부름** (2026-09-24, `AI/CatBrain.House.cs`, design/cat-ideas/10): 하던 일을 멈추고(추격도 끊음 — 간식이 더 중요, 놀이·포획 중이면 끝난 뒤) 가까운 Door 스팟으로(3.0) → 도착하면 `AwayHidden` NV(렌더러 끔)·감각 0 → 20~40s 뒤 **랜덤 문**으로 Warp → Return. 문이 없으면 쥐들에게서 가장 먼 스팟. **밥 시간**은 Away가 아니라 Patrol에서 Food 스팟 강제·머무름 = 남은 밥 시간(25s, 감각 0.5) | 3.0 |
| Blunder | **댕청한 실패** (2026-09-24, `AI/CatBrain.Blunder.cs`, design/cat-ideas/11 — 쥐가 만든 상황에서만): `BlunderKind` NV. **Slip** — 속도 ≥4로 달리다 바닥의 풀린 Slippery 물건(비누·접시·수박) 0.7m 안을 밟으면 진행 방향 3m를 0.6s에(NavMeshAgent.Move — NavMesh 밖 안 나감, 밟은 물건도 튕김) → 도중에 막히면 **쿵 → 기절 2s**, 아니면 추스르기 0.8s → Return. 쿨다운 4s. **Startle** — 호기심 중 2.5m 안 깨짐·찍찍·함정 → 뒤로 1.2m 펄쩍 1.5s → 소리 난 곳 Suspicious. **Wobble** — 캣닢 놀이 끝나면 10s 비틀비틀(1s마다 주변 2m, 순찰 속도 ×0.5, 감각 ×0.3, 코앞 목격은 추격). 실패는 앙심 안 올림 | 0~1.0 |
| Toy | **가지고 놀기** (2026-09-24, `AI/CatBrain.Toy.cs`, design/cat-ideas/04): 관심 100·전체 30s. Bat 2s(1s마다 잡힌 쥐를 0.4m 툭 — 소유 클라 순간이동 RPC) → 관심 -35 → Release 3s(쥐 Active, 고양이 1m 물러남): 놓아준 자리에서 2m 벗어나면 Chase(10s 안에 다시 잡으면 관심·남은 시간 이어서), 아니면 다시 Pinned. 관심 ≤0 → 하품 2s → **생존**(Active) + 그 쥐 8s 못 본 척. 30s 만료 → 진짜 Downed. 다른 쥐가 시야에 들어오면 잡은 쥐를 놓고 그쪽 Chase(**미끼 = 구출**). 깨짐·찍찍·함정 소리 관심 -40. 솔로면 기존처럼 즉시 Downed | 0 |
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

### 움직임 (2026-09-24, 시야 틱과 같이)
- 풀린 `CarryableItem` 중 속도 ≥ `catCuriosityMinSpeed` 1.5, 시야 거리·각도 안, 가려지지 않음 → `CuriosityTarget` (가장 가까운 것). 목록 2s마다 갱신. 필터(쿨다운·질림)는 CatBrain이 준다.
- 청각 예외: `IgnoreImpactNear`(노는 물건) 1.5m 안의 Impact 소음은 무시.

### 냄새 (2026-09-24, `Noise/ScentSystem`·`Player/PlayerScent`)
- 치즈류(Edible)를 손이나 주머니에 든 쥐 = 강도 60, 고양이에게 찍힌 쥐(앙심) = 빈손이어도 20. 1.5m마다 자국(웅크리면 3m), 초당 -3 감쇠(60 → 20s), 버퍼 32개.
- 고양이는 Patrol·Return 중 3m 안 자국을 맡으면 Track. 수색(Search) 후보 중 2m 안에 자국이 있는 스팟을 먼저 뒤진다.
- 자국은 **남긴 쥐 본인 화면에만** 노란 점으로 보인다(소유 클라 ClientRpc). 2단계: 젖은 발자국, 물·바람·후추로 지우기, 고양이 침대로 덮기.

### 청각

- NoiseSystem.OnNoise 구독 (06 계약): `게이지 += loudness × 거리감쇠 × 0.8`
- Break/Trap/Squeak 타입은 즉시 Suspicious(해당 지점).

## 장소 기억·앙심 (2026-09-24, `AI/CatBrain.Memory.cs`, design/cat-ideas/05)

런 안에서만 쌓인다(고양이 재스폰 = 초기화). 학습은 보여야 한다 — 킁킁·"!!"·"찍힘".

- **장소 기억**: 2m 격자 열 지도. 쥐를 보는 동안 +1/s, 들린 소음 ≥40이면 +loudness/40, -0.1/s 감쇠. 순찰 목적지를 고를 때 30%(연속 금지)로 열 ≥3인 가장 뜨거운 칸에 가서 킁킁 1.5s(`Sniffing` NV) → 그 칸 열 절반.
- **앙심**: 쥐별 0~100. 추격에서 놓침 +40(시야 상실 3s·성격 포기), 들리는 곳 찍찍 +15, -0.2/s 감쇠. 50 이상 중 최고 1명 = `GrudgeClientId`·`HasGrudge` NV.
  - 효과: 그 쥐 시야 게인 ×1.5, 여럿 보이면 그 쥐 우선(거리 ×0.6로 비교), 그 쥐 발소리·찍찍 청각 ×1.3, 성격 추격 포기 +2s.
  - 표시: 나를 쫓는 고양이가 나를 찍었으면 SuspicionIndicator "!!", 팀 상태 "찍힘" 칩(Danger).
- 귀속 (2026-09-24 고양이 12): `NoiseEvent.HasSource` — 귀속된 소리만 앙심 대상. 물건 충돌·깨짐은 `CarryableItem.AttributedClient`(들고 있으면 첫 캐리어, 놓은·던진 지 3s 안이면 마지막 캐리어, 그 밖 환경)로 귀속.
  - **깨뜨림 +30**: 귀속된 Break를 들으면.
  - **털실 속음 +20**: 털실을 던진 쥐에게, 놀이가 끝날 때. 이미 찍힌 쥐가 던진 유인엔 안 속는다.
  - **뇌물 -50**: Patrol·Return·Suspicious·Search 중(추격 아님) 정면 60° 1.5m 안에 쥐가 10s 안에 내려놓은 치즈류 → 그 자리에서 먹음(Distracted 4s) → 물건 사라짐 → 놓은 쥐 앙심 -50. 먹는 도중 쥐가 도로 집으면 무효.

## 경계도 디렉터 손잡이 (2026-09-24, `Run/RunDirector` + `AI/CatBrain.Director.cs`, design/cat-ideas/12)

디렉터 모드를 읽어 **관찰로 알 수 있는 것만** 바꾼다 — 시야·청각·게이지 배율은 절대 안 건드린다(공정성).

| 모드 | 손잡이 |
|---|---|
| Build-up·Finale | 잠 길이 ×0.7, Look 머무름 ×0.6, 기억 칸 방문 확률 +0.3, `Alert` NV → 순찰 중 꼬리 빠르게(예민) |
| Relief | 다음 스팟 가중치 Groom·Sun·Bed ×4 · Look ×0.3, 루틴 머무름 ×1.5 |
| Finale | 순찰 목적지 60%를 쥐구멍 4m 안 랜덤 지점(도착하면 킁킁 1.5s), 호기심 무시 |

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

- 2마리 관계(2026-09-24): `AI/CatRelation`(RunManager 런타임 부착)이 앙숙 싸움을 중재. 데모 스테이지는 1마리 유지(2마리는 Zone3+ — docs/00).

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

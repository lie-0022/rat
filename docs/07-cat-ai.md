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

## 크기 — 실제 비율 (2026-09-25, 고양이 141, 사용자 결정)
- **벽 속** 고양이는 쥐 키(1.2)의 3배 — 키 약 3.6·길이 약 5(`GridZoneSO.CatSizeScale` 1.9, 스폰 전에 `CatBrain.ServerPrepareSize`). 몸·공간에 묶인 거리(잡기·앞발·시야·수색·놀이·매복 덮침·문지기 둘레 등)는 고양이별 균형값 복사본에서 같은 배율(`BalanceConfigSO.ScaledForCat`), **속도는 그대로**(추격 5.5 < 쥐 달리기 7).
- NavMesh 에이전트 타입 "BigCat"(반지름 1.1·높이 3.4 — 테마 SO, 도구 `Create Big Cat Nav Agent`). 창고 데모·부엌(생성) 고양이는 예전 크기.

## FSM

```csharp
public enum CatState { Sleep, Patrol, Suspicious, Chase, Capture, Distracted, Return, Search, Curious, Track, Toy, Blunder, Away, Fight, Zoomies, Ambush, BoxSit, Flank, Respond }
```

```
Sleep ──(소음 ≥40 근접)──▶ Suspicious
Patrol ──(의심게이지 ≥30)──▶ Suspicious ──(게이지 ≥100 or 직접목격 1s)──▶ Chase
   ▲                            │(게이지 0)                                │
   └────────── Return ◀─────────┘                     (타깃 접촉)──▶ Capture ──▶ Return
Distracted: 털실뭉치 등 아이템 트리거 → 8s 후 이전 상태로
- **관심 점수 (고양이 261, design/cat-ideas/13 본체 — `AI/CatBrain.Attention.cs`)**: 유인은 한 저울 — `AttentionKind` 먹이 떨굼 40 · 끈 50 · 털실 60 · 레이저 80 · 캣닢 90 (`BalanceConfigSO.attentionScores`). 유인 중엔 지금 것보다 **낮은** 자극 무시(같거나 높으면 갈아탐). 지루함: 같은 종류에 60s 안 3번 넘게 반응하면 시간 ×0.5. 의심·추격 우선은 그대로(상태 조건이 먼저 거름).
```

| 상태 | 행동 | 이동속도 |
|---|---|---|
| Sleep | 잠자리(Bed 스팟)에서 60s. **얕은 잠 12~20s(감각 0.5) → 예고 3s → 깊은 잠 10~18s(0.15) → 예고 3s(꼬리 씰룩 = "나가" 신호) → 얕은 잠…** 파동 (2026-09-24, design/cat-ideas/08). 얕은 잠 중 게이지 ≥30 또는 깨짐·함정·찍찍 소음 → HalfAwake 3s(감각 0.5): 그 사이 목격·새 자극이면 Suspicious, 없으면 다시 잔다. 깊은 잠은 게이지 ≥60 또는 깨짐류만. 작은 자극은 자는 동안 잊는다(깬 뒤 엉뚱한 조사 방지). 잠은 얕은 잠에서만 끝난다. **코골이 자막**(고양이 152): Deep 들 때 "(드르렁… 고양이가 깊이 잠들었다)", ToLight 들 때 "(코골이가 멈췄다 — 곧 깬다!)" — 루틴 예고와 같은 `CatCueClientRpc`·거리(18m × 몸 배율). 꼬리 씰룩(눈)을 벽 너머에서도 소리로. 문지기·순찰꾼·아기면 이름이 붙음("문지기가 깊이 잠들었다", 고양이 163) | 0 |
| Patrol | **스팟 그래프** 순회 (2026-09-24, `AI/CatSpot`): 가중치 랜덤·최근 2개 제외 → 도착 후 종류별 머무름 — Look 2~5s / Food 25s(감각 0.5) / Sun 40s(0.5) / Groom 20s(0.6) / Bed → Sleep 45s 뒤 깸(다음 스팟으로). CatSpot이 없으면 `CatWaypoint*`를 Look으로. design/cat-design/02 카탈로그 | 2.0 |
| (Patrol 추가, 2026-09-24) | Litter 12s(감각 0.3) → Zoomies / Water 8s(감각 1). **루틴 예고**: 루틴 스팟(Food·Litter·Sun·Bed·Water)으로 출발할 때 `CatCueClientRpc` → `EventBus.CatCue` → 고양이 18m(× 몸 배율 `BodyScale` — 벽 속 큰 고양이 34m, 고양이 149) 안 쥐에게 자막 토스트("배 꼬르륵"·"모래 긁는 소리… 곧 우다다!"·"창가 쪽 기지개"·"쩌억 하품"·"할짝할짝") — 시야 밖에서 상태를 소리로 읽는다(필러 2). 자막 끝에 내 카메라 기준 8방향(" · 왼쪽 뒤" — 고양이 153, 매복 "어디선가"만 방향 없음). 데모 레이아웃 Litter(-15,-15)·Water(11,16) | — |
| Suspicious | 마지막 자극 지점으로 이동, 주변 3m 배회 6s — 배회 6s는 **도착한 뒤부터**(가는 길 상한 `catSuspiciousTravelSeconds` 10s). 게이지가 식어도 조사는 끝까지(2026-09-24 고양이 44 수정: 먼 곳 깨짐은 게이지가 조금만 올라 도착 전에 식어 조사를 안 했다) | 3.0 |
| Chase | **예측 추격** (2026-09-24): 타깃 속도(위치 차분)로 1s 앞 지점을 노림(NavMesh 샘플, 최대 5.5m). 시야 상실 시 마지막 진행 방향으로 **3m 오버슛** 뒤 마지막 목격점, 3s 후 Suspicious. **코너 감속**: 몸 방향과 경로 방향이 어긋나면 속도 ×0.55까지 — 직선은 최고 속도, 지그재그는 실제로 도움. Agent angularSpeed 240·acceleration 8 | 5.5 (직선) |
| Search | **숨을 곳 수색** (2026-09-24, `AI/CatBrain.Search.cs`, design/cat-ideas/14): Chase 시야 상실 3s 뒤 마지막 목격점 반경 6m에 HideSpot이 있으면 진입(없으면 Suspicious). 타깃이 Hidden이 되면 Return이 아니라 시야 상실로 처리. 가까운 순 최대 3곳 → 출구 앞 킁킁 2s(여럿 숨었으면 × 인원 × 1.5) → 30% 건드림 → 안의 쥐 발각(출구로 튀어나옴) → Pounce 창 0.5s → Chase. 25s 또는 스팟 소진 → Return. 목격·소음·미끼가 끊는다 | 3.0 |
| Track | **냄새 추적** (2026-09-24, `AI/CatBrain.Track.cs`, design/cat-ideas/06): Patrol·Return 중(우선순위 Suspicious 아래·Curious 위) 반경 3m 안 강도 ≥8 자국을 맡으면 진입 — 시야 불필요. 그 자국 → 같은 쥐의 다음 자국 순으로 따라가며 자국마다 킁킁 0.5s. 목격·자극이면 끊김. 자국이 끊기면 끝점 3m 안 HideSpot이 있으면 Search, 없으면 Return. 따라간 자국(쥐별 순번)은 다시 안 쫓음 | 3.0 |
| Curious | **호기심 앞발** (2026-09-24, `AI/CatBrain.Curious.cs`, design/cat-ideas/03): Patrol·Return 중 시야에 속도 ≥1.5 m/s로 움직이는 풀린 물건(안 들림·주머니 아님·대형 아님)이 보이면 진입 — 의심·추격·잠 중엔 안 속음. 물건을 따라가 수평 0.9m 안에서 앞발 3~5회(0.6s 간격, 정면 ±60° 수평 + 위, 속도 변화 1.2 m/s — 호스트 물리) → 하품 5s → Return. 그 물건은 10s 무시, 같은 물건 누적 30s면 질려서 60s 무시. 8s 안에 못 닿으면 포기, 쥐가 집으면 즉시 Return. 자기가 친 물건의 충돌 소음은 무시(깨짐은 예외) | 4.0 |
| Capture | 앞발 스윙 애니 0.4s → 범위 내 플레이어 Downed 처리. **움직일 수 있는 동료(Active·Hidden·Stunned)가 있으면 Downed 대신 Pinned + Toy** (2026-09-24) | — |
| (눈높이, 고양이 180) | 시야 선 시작 = 발밑 위 `catEyeHeight` 0.5m(쥐 목격·물건 호기심·고양이끼리). `catEyeHeightScalesWithBody` 켜면 몸 윗면 × 0.8(큰 고양이 2.6m). F4에서 개발용으로 바꿔 보기. 지금 맵은 둘이 차이 없음(판단 12) | 0 |
| (무는 자리, 고양이 167) | 가지고 놀기에서 쥐를 두는 입 자리 = 고양이 앞 (몸 충돌체 반지름 × 고양이 스케일) + 0.4m(최소 0.6) — 큰 고양이 몸 속에 놓이지 않게. 물고 걷는 동안은 고양이 걸음 × 0.2초 앞당김. 앞에 바닥이 없으면 ±45°·±90°·±135°·180° 순서로 몸 밖 자리를 찾고, 없으면 쥐를 제자리에(예전엔 고양이 위치를 대신 써서 한가운데 놓임 — 고양이 179). 아기 고양이는 몸 충돌체도 성격 몸 배율(0.6)만큼 작게(`CatVisual`) | 0 |
| Ambush | **매복** (2026-09-24, `AI/CatBrain.Lurk.cs`, design/cat-ideas/09): Ambush 스팟 도착 → 60s 정지, `AmbushHidden` NV로 **꼬리만 보임**(텔레그래프 타협 불가) + 10s마다 "(츄릅… 어디선가)" 자막(18m). 2m 안 쥐 목격 → **덮치기**(추격 없이 Capture, 스윙 0.2s). 더 멀리서 보이면 평소 전이 | 0 |
| BoxSit | **상자 입구에 앉기** (2026-09-24): Box 스팟(쥐의 숨을 곳 입구) 도착 → 15~40s, 시야 정면 3m·반각 45°만(`CatSenses.ViewDistanceOverride/HalfAngleOverride`). 2m 안 숨을 곳에 `CatBlocking` NV → 쥐가 들어가지도 나오지도 못함(갇힘). 도착 때 안에 쥐가 있으면 끄집어내 덮치기 | 0 |
| Zoomies | **우다다** (2026-09-24, `AI/CatBrain.Routine.cs`, design/cat-ideas/02): Litter 스팟 볼일 12s(감각 0.3) 뒤 10s 동안 속도 6.5(추격보다 빠름)로 반경 8m 랜덤 지점을 1.2s마다 — 목적 없음, 복도는 도박. 코앞 목격은 추격, 비누 밟으면 미끄러짐 | 6.5 |
| Respond | **엄마가 아기에게** (2026-09-24, `AI/CatBrain.Buddy.cs`): 아기가 Suspicious·Chase에 들어가면(10s 쿨다운) 엄마가 추격 속도로 아기 위치까지 → 게이지를 의심 임계로 올려 그 자리 Suspicious. 가는 길에 목격하면 추격. 놀이·추격·부재·싸움 중이면 무시 | 5.5 |
| Flank | **짝꿍 협공** (2026-09-24, `AI/CatBrain.Buddy.cs`, design/cat-ideas/07): 짝꿍이 Chase에 들어가면 20m 안의 한가한(Patrol·Return·Suspicious·Curious·Track·Search) 다른 고양이가 쥐의 **2s 뒤 예상 위치**(위치 차분 속도, 최대 5.5로 자름, NavMesh 샘플)로 0.5s마다 갱신하며 4.0 속도로 8s. 보이면 평소 전이(추격) | 4.0 |
| Fight | **앙숙 싸움** (2026-09-24, `AI/CatBrain.Fight.cs` + `AI/CatRelation`, design/cat-ideas/07): 고양이 2마리 이상일 때 호스트 중재자가 0.25s마다 판정 — 둘 다 Patrol·Return·Suspicious·Curious·Track·Search, 4m 안, 가림 없음 → 70% 싸움 15s / 30% 째려보고 지나감(10s 재판정 금지). 싸움 뒤 30s 재발 금지. 싸우는 동안 가운데 1.2m를 1s마다 빙글(3.0), 청각 차단(`CatSenses.Deaf`)·시야 ×0.2. 2m 안 쥐 목격이면 싸움 깨고 Chase, 상대가 빠지면 같이 끝. 관계는 지금 앙숙 고정(엄마·아기·짝꿍은 다음) | 3.0 |
| Away | **집주인이 부름** (2026-09-24, `AI/CatBrain.House.cs`, design/cat-ideas/10): 하던 일을 멈추고(추격도 끊음 — 간식이 더 중요, 놀이·포획 중이면 끝난 뒤) 가까운 Door 스팟으로(3.0) → 도착하면 `AwayHidden` NV(렌더러 끔)·감각 0 → 20~40s 뒤 **랜덤 문**으로 Warp → Return. 문이 없으면 쥐들에게서 가장 먼 스팟. **밥 시간**은 Away가 아니라 Patrol에서 Food 스팟 강제·머무름 = 남은 밥 시간(25s, 감각 0.5) | 3.0 |
| Blunder | **댕청한 실패** (2026-09-24, `AI/CatBrain.Blunder.cs`, design/cat-ideas/11 — 쥐가 만든 상황에서만): `BlunderKind` NV. **Slip** — 속도 ≥4로 달리다 바닥의 풀린 Slippery 물건(비누·접시·수박·헤어볼) 0.7m 안을 밟거나 **엎은 물그릇 웅덩이** 안이면(2026-09-24) 진행 방향 3m를 0.6s에(NavMeshAgent.Move — NavMesh 밖 안 나감, 밟은 물건도 튕김) → 도중에 막히면 **쿵 → 기절 2s**, 아니면 추스르기 0.8s → Return. 쿨다운 4s. **Startle** — 호기심 중 2.5m 안 깨짐·찍찍·함정 → 뒤로 1.2m 펄쩍 1.5s → 소리 난 곳 Suspicious. **Wobble** — 캣닢 놀이 끝나면 10s 비틀비틀(1s마다 주변 2m, 순찰 속도 ×0.5, 감각 ×0.3, 코앞 목격은 추격). **Hairball** — Patrol에서 Groom 스팟 머무름이 끝날 때 20% → 웩웩 3s(몸 들썩) → 정면 0.6m에 `loot_hairball`(가치 5·Slippery — 깔면 고양이가 미끄러짐) 스폰(Cat 프리팹 `_hairballItem`). 실패는 앙심 안 올림 | 0~1.0 |
| Toy | **가지고 놀기** (2026-09-24, `AI/CatBrain.Toy.cs`, design/cat-ideas/04): 관심 100·전체 30s. Bat 2s(1s마다 잡힌 쥐를 0.4m 툭 — 소유 클라 순간이동 RPC) → 관심 -35 → Release 3s(쥐 Active, 고양이 1m 물러남): 놓아준 자리에서 2m 벗어나면 Chase(10s 안에 다시 잡으면 관심·남은 시간 이어서), 아니면 다시 Pinned. 관심 ≤0 → 하품 2s → **생존**(Active) + 그 쥐 8s 못 본 척. 30s 만료 → 진짜 Downed. 다른 쥐가 시야에 들어오면 잡은 쥐를 놓고 그쪽 Chase(**미끼 = 구출**). 깨짐·찍찍·함정 소리 관심 -40. 솔로면 기존처럼 즉시 Downed. **물고 옮기기**(고양이 34): 한 판에 한 번, 첫 Bat 뒤 60% 확률로 4~12m 안 가장 가까운 Sun·Bed 스팟으로 물고 간다(순찰 ×1.2, 최대 6s, 0.1s마다 입 위치로 소유 클라 순간이동 RPC) → 내려놓고 Bat. 옮기는 중 초당 10% 떨어뜨림 → 바로 Release 창. **버둥**(고양이 35, `Player/PlayerStruggle`): 잡힌 쥐가 A·D를 번갈아 누르면(최소 간격 0.08s) 1s 동안 버둥 — 툭 칠 때 무작위 대신 **쥐가 보는 쪽**으로 ×2(0.8m), Bat 끝에 버둥 6회마다 관심 -8 | 0 |
| Distracted | 유인 아이템 위치에서 놀기. **흔들리는 끈**(2026-09-24, `World/DanglingString`): 쥐가 E 0.3s로 툭 치거나 멈춘 끈 1.5m 옆을 지나가면 30s 흔들림 → 0.5s마다 8m 안·시야 막힘 없는 Patrol·Return·Search 고양이를 끈 아래로 10s 부름(고양이별 40s 쿨다운, 앙심 무관, 소리 없음). **레이저 포인터**(고양이 41, `World/LaserPointer`·`AI/CatBrain.Laser.cs`): 쥐가 손에 든 동안 조준점(25m)에 빨간 점 — 든 쥐 클라가 0.1s마다 조준을 보내고 호스트가 든 사람인지 검증. 점 12m 안·보이는 한가한 고양이(Patrol·Return·Search·Curious·Distracted)는 4.5로 달려 쫓고 점이 움직이면 따라간다. 점이 꺼지면(내려놓기·주머니·배터리 20s 끝) 5s 두리번 뒤 복귀 | 4.0 |
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
  - 구현 (2026-09-24, `World/LightZone`): 판정은 **쥐 위치** 기준(고양이는 밝은 곳에 있어도 된다). 배율 `catDarkViewMultiplier` 0.5, 웅크림과 곱 — 어둠+웅크림 = 8m × 0.25 = 2m(사냥꾼 ×1.3 → 2.6m). `Lit` NV가 켜지면 어둠이 아니다(불 켜짐 이벤트). 바닥의 어두운 판이 곧 표시(Lit이면 숨김). 데모 레이아웃: 동쪽 (13,0,-2) 8×8.
- 게이지는 자극 없을 때 -20/s. NetworkVariable로 복제 → 타깃 플레이어 HUD에 "?"/"!" 표시.
  - HUD 구현 (2026-09-16, `UI/SuspicionIndicatorWidget`): **나를 쫓는 고양이**(Chase && TargetClientId == 나) → 빨간 "!", 없으면 **가장 가까운 Suspicious 고양이** → 주황 "?" + 게이지(Gauge/chaseThreshold). 의심은 "지점"에 대한 것이라 타깃이 없어 누구에게나 보인다. 남을 쫓는 고양이는 표시 안 함. 주의: TargetClientId 기본값 0 = 호스트 Id — Chase가 아닐 때만 0이 쓰이므로 State와 함께 읽어야 한다.

### 움직임 (2026-09-24, 시야 틱과 같이)
- 풀린 `CarryableItem` 중 속도 ≥ `catCuriosityMinSpeed` 1.5, 시야 거리·각도 안, 가려지지 않음 → `CuriosityTarget` (가장 가까운 것). 목록 2s마다 갱신. 필터(쿨다운·질림)는 CatBrain이 준다.
- 청각 예외: `IgnoreImpactNear`(노는 물건) 1.5m 안의 Impact 소음은 무시.

### 냄새 (2026-09-24, `Noise/ScentSystem`·`Player/PlayerScent`)
- 치즈류(Edible)를 손이나 주머니에 든 쥐 = 강도 60, 웅덩이를 지나 젖은 쥐 = 40(20s), 고양이에게 찍힌 쥐(앙심) = 빈손이어도 20. 웅덩이는 그 안의 자국을 지운다. 1.5m마다 자국(웅크리면 3m), 초당 -3 감쇠(60 → 20s), 버퍼 32개.
- 고양이는 Patrol·Return 중 3m 안 자국을 맡으면 Track. 수색(Search) 후보 중 2m 안에 자국이 있는 스팟을 먼저 뒤진다.
- **고양이 침대로 냄새 덮기** (2026-09-24): Bed 스팟 1.5m 안에 들어간 쥐는 30s 동안 자국 없음(치즈를 들어도) — 대신 침대는 고양이가 돌아오는 곳.
- **후추** (2026-09-24, `World/PepperShaker`): 들고 던지는 통. 던져서 착지하거나 4 m/s 이상으로 떨어지면 한 번 쏟아져 바닥 반경 2m 패치 45s — 안의 자국을 계속 지우고(쥐는 안 젖음), 들어온 고양이는 **재채기**(Blunder Sneeze 2s 정지 → 자극·타깃 잊고 Return, 같은 고양이 8s 쿨다운). 추격 중에도 재채기한다 — 추적·추격 끊기 도구.
- 자국은 **남긴 쥐 본인 화면에만** 노란 점으로 보인다(소유 클라 ClientRpc). 2단계: 젖은 발자국, 물·바람·후추로 지우기, 고양이 침대로 덮기.

### 귀 (2026-09-24, 고양이 42, `AI/CatBrain.Ears.cs`·`CatVisual`)
- 귀(Ear_L·Ear_R)가 관심 쪽으로 돈다(몸 기준 yaw ±80° + 앞으로 쫑긋) — 게이지보다 먼저 보이는 텔레그래프(필러 2).
- **들을 뻔한 소리**: 소리 전파 반경의 `catEarRadiusMul` 1.5배 안, 크기×감각 ≥ 청각 임계 × `catEarHearMul` 0.5 → 귀만 `catEarHoldSeconds` 2s 그쪽. 게이지는 반경 안에서만 오르므로 반경 1~1.5배는 "귀만" 구간. 자는 고양이도 귀는 움직인다.
- 상태 대상이 있으면 계속 그쪽: Chase(쫓는 쥐)·Suspicious/Search(조사 지점)·Distracted(유인 지점)·Curious(물건). 호스트 `EarPoint`·`EarActive` NV(0.25s, 0.3m 넘게 바뀔 때만).

### 청각
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

### 추가 성격 (2026-09-24, 고양이 19)

| 성격 | 시야 | 청각 | 추격 | 수면 | 특이 행동 | 몸 색 |
|---|---|---|---|---|---|---|
| 호기심쟁이 | 1.0 | 1.2 | 1.0 | ×0.4 (잠자리 가중치 ×0.5) | 굴러가는 물건 반응 속도 기준 ×0.5(1.5 → 0.75 m/s), **의심 중에도** 호기심(Suspicious → Curious) | 밝은 주황 |
| 겁쟁이 | 1.0 | 1.4 | 0.9 | ×1 | 원 loudness ≥60 소리(깨짐·함정)를 들으면 **Flee 3s**: 소리 반대쪽 6m(속도 5) → 게이지를 의심 임계로 올려 소리 난 곳 조사. 자다가도 도망 | 크림색 |

- `CatPersonalitySO` 필드: curiositySpeedMultiplier · curiousWhileSuspicious · fleeLoudness · fleeSeconds. Flee는 `CatBlunderKind.Flee`(`AI/CatBrain.Personality.cs`).
- Cat 프리팹 성격 4종 — 스폰 시 랜덤.

### 게으름뱅이 기지개 (2026-09-24, 고양이 26)
- 성격 필드 `wakeStretchSeconds`(게으름뱅이 1.5, 나머지 0): HalfAwake에서 깨어 의심으로 넘어갈 때 바로 움직이지 않고 `CatBlunderKind.Stretch`(몸 늘어짐) → 게이지를 의심 임계로 올려 Suspicious. 잠에서 깨는 순간이 읽히는 텔레그래프 — 도망칠 틈.

### 아기 고양이 (2026-09-24, 고양이 23)
- 시야 0.5(≈4m)·청각 1·추격 0.6·**모든 이동 ×0.75**(`CatMovement.SpeedMultiplier`)·수면 ×1.5·몸 크기 0.6(CatVisual). 흰 회색.
- **잡지 못한다**: Capture 명중 시 쥐를 Stunned(2s)만 → Return.
- 무작위 성격 추첨에서 제외(`isKitten`) — 존 생성기·테스트가 `ServerSetPersonality`로 명시. 아기가 있으면 관계는 엄마·아기.

## 선반 관찰대·점프 (2026-09-24, 고양이 39, `AI/CatBrain.Perch.cs`·`AI/CatMovement`, design/cat-ideas/09·01·11)
- Perch 스팟(선반 윗면)에 도착하면 `catPerchSeconds` 25s 머무름(감각 1) + 시야 거리 `catPerchViewDistance` 12(성격 배율 곱). 눈높이가 높아(윗면 1.6 + 0.5) 1.5m 상자 너머도 보인다. 선반 **밑**(1.4m, 쥐만 — 고양이 에이전트 높이 1.7)은 판이 가려 위에서 안 보인다.
- 점프: `NavMeshLink`(양방향)를 `CatMovement`가 직접 넘는다 — 0.45s 호(높이 0.8). **올라가는** 점프는 `catJumpFailChance` 0.15로 실패: 중간에서 출발점으로 떨어져 Blunder Stun `catJumpFailStunSeconds` 1.5s → Return.
- 성격 `perchWeightMultiplier`: 기본 1, 사냥꾼 3(선반을 좋아함).

## 문 닫기·밀기 (2026-09-24, 고양이 40, `World/RoomDoor`·`AI/CatBrain.Door.cs`, design/cat-ideas/09)
- 쥐가 E 0.4s로 문을 닫는다(소리 25, 귀속 — 고양이 1m 안이면 못 닫음). 닫힌 문판의 `NavMeshObstacle`(carve)이 고양이 길을 막는다.
- 목적지 보정: `CatMovement.MoveTo`가 닫힌 문 너머(방 박스 안↔밖) 목적지를 **문 앞(내 쪽 0.8m)**으로 바꾼다 — 안 그러면 NavMesh가 방 벽 바깥 "가장 가까운 곳"으로 보낸다.
- 밀기: Chase·Suspicious·Track·Search 고양이가 닫힌 문 1.4m 안에 있으면 0.25s마다 누적, `doorPushSeconds` 4s면 열림(순찰·복귀 고양이는 안 민다). 미는 동안 `ServerHoldAtDoor`가 추격 놓침 타이머·의심·수색 시간·게이지 감쇠를 붙잡는다(4s > 놓침 3s).
- 쥐 전용 틈: 벽 구멍 폭 0.8·높이 1.4 — 쥐 캡슐(반경 0.3·키 1.2)은 지나가고, 고양이 NavMesh는 안 이어진다(에이전트 반경 0.5·높이 2).

## 문지기 고양이 (2026-09-25, 고양이 80, `AI/CatBrain.Guard.cs`, 잠정)

- 벽 속 깊은 스테이지에서 어른 고양이 한 마리가 **목적지 앞 문지기**가 된다: 목적지방 문 바깥(이웃 방 쪽 1.5m 안)에 `GuardPost` 관찰점을 두고, 그 고양이는 반경 `catGuardRadius`(8m) 안 스팟만 순찰, 초소는 가중치 × `catGuardPostWeight`(4).
- 유인(털실·레이저·후추)·소리·추격은 평소대로 — 끌려갔다가 순찰로 돌아오면 다시 초소 둘레로. 상점 물건을 쓸 이유가 생긴다.
- 목적지에 문이 둘(고리)이면 한쪽만 지킨다 — 다른 길로 돌아가는 선택.
- 호스트 AI만 바뀜(새 동기화 없음). 스테이지별 확률은 `Zone_Walls.GuardChanceByStage`(docs/10).
- **문지기 성격 (고양이 82)**: `Personality_Guard` "문지기" — 푸른 회색(#7E9CB8, 다른 성격과 안 겹침), 시야 ×1.15·청각 ×1.1, 잠 ×0.5, 잠자리 ×0.3, 관찰점 ×2·머묾 ×1.5. `RoleOnly`(아기·문지기)는 무작위 추첨 제외 — 문지기로 정해진 고양이만 이 성격. 성격 인덱스가 이미 동기화돼 클라도 색·이름(F3)으로 알아본다. 에셋·프리팹 목록은 `Tools/RatGame/Cat/Create Guard Personality`(목록 끝에 붙임 — 인덱스가 동기화 값이라 앞에 끼우지 않음).
- 배정 때 초소로 순간이동(먼 방에서 자다 깨면 초반엔 문이 비어서 — 초소에서 잠들면 몰래 지나갈 기회). 측정: 60초 동안 초소 10m 안 100%, 털실 한 번에 약 30초 틈(끌려갔다 Return이 둘레 밖 가까운 스팟에 머문 뒤 복귀). 아기 고양이가 울면 엄마로서 달려감 — 문지기를 빼내는 다른 방법.

## 순찰꾼 고양이 (2026-09-25, 고양이 92, `AI/CatBrain.Route.cs`, 잠정)

- 벽 속 깊은 스테이지에서 어른 고양이 한 마리(문지기·아기 아님)가 **큰길(출발 → 목적지 최단 경로)을 오간다**: 출발방·목적지방을 뺀 큰길 방마다 `PatrolPoint` 관찰점, 순서대로 갔다가 끝에서 되돌아온다(머묾 짧게). 큰길이 위험해져 갈래·배관 우회가 의미 있어진다.
- 성격 `Personality_Patroller` "순찰꾼" — 적갈색(#8E3B2F), 추격 ×1.05, 잠 ×0.3, 관찰점 머묾 ×0.35. `RoleOnly`라 무작위 추첨 제외.
- 유인·소리·추격은 평소대로 — 끝나고 순찰로 돌아오면 가장 가까운 지점부터 다시 오간다. 호스트 AI만(새 동기화 없음).

### 역할 고양이가 졸 때 (2026-09-25, 고양이 101)
- 문지기·순찰꾼이 잠(Sleep)에 들면 루틴 예고와 같은 길로 자막(`CatCueKind.GuardNap/PatrolNap`, 들리는 거리 안 쥐만): "(쿨쿨… 문지기가 졸고 있다 — 지금!)" / "(꾸벅꾸벅… 순찰꾼이 잠들었다 — 큰길이 빈다)". 아기가 자면 엄마 관계로 같이 조는 경우(고양이 92에서 봄)가 대표적인 틈.

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

- 2마리 관계(2026-09-24): `AI/CatRelation`(RunManager 런타임 부착)이 2마리가 처음 보일 때 관계를 뽑는다(`catBuddyChance` 0.5 — **짝꿍**, 아니면 **앙숙**). 앙숙 = 싸움(Fight). 짝꿍 = 싸우지 않고 협공(Flank) + **공동 수면**(한 마리가 잠들면 다른 마리가 Patrol·Return 중이면 같은 잠자리로 가서 수면 ×1.5). **엄마·아기** = 고양이 중 아기 성격이 있으면 추첨과 상관없이(매 스캔 확인): 아기가 놀라면 "(냐앙! — 아기 고양이가 엄마를 부른다)" 자막(18m) + 엄마 Respond, 공동 수면. 데모 스테이지는 1마리 유지(2마리는 Zone3+ — docs/00).

- ZoneDefinitionSO에 catCount (Zone1:1 → Zone3+:2). CatSpawn 태그 지점에서 호스트 스폰.
- NavMesh는 방 모듈 프리팹에 미리 굽지 못하므로 **런타임 베이크**: ZoneGenerator 완료 후 `NavMeshSurface.BuildNavMesh()` (com.unity.ai.navigation). 방 3~4개 규모면 1~2초, 로딩 화면에서 처리.

## 연출 계약 (클라)

- 그레이박스 텔레그래프 (2026-09-24, `AI/CatVisual`, 전 클라 읽기): 몸 색(잠 갈색·의심 노랑·추격 빨강·포획 진빨강·유인 하늘) + 꼬리 피벗(얕은 잠 천천히 / 깊은 잠→얕은 잠 예고 급씰룩 / 의심·추격 빠름) + 깊은 잠 배 펄스·몸 가라앉음 + HalfAwake 머리 들림. `SleepPhase` NetworkVariable로 단계 복제. 소리는 오디오 단계에서 같은 계약으로.
- CatState NetworkVariable OnValueChanged → CatAnimatorLink가 애니·사운드 재생:
  Suspicious: 귀 쫑긋+"냐?" / Chase: 낮은 그르렁+BGM 전환(EventBus) / Capture: 앞발 스윙
- **그레이박스 소리 (고양이 242, `AI/CatVoice` + `Core/ToneSynth` 합성음, 연출값 `Resources/GrayboxAudio` = `GrayboxAudioSO`)**: 전 클라 로컬, State·SleepPhase 읽기만. Suspicious "냐?"(520→820Hz 0.35s) · Chase 그르렁 반복(58Hz, 떨림 22Hz) · Capture 쉭(0.22s) · 깊은 잠 코골이 반복(2.6s, ToLight에서 멈춤). 3D 선형 감쇠 2~28m, 볼륨 = 전체 × 효과음 설정. SO의 클립 칸을 채우면 합성음 대신 진짜 소리. 고양이 발소리(고양이 271, `CatVoice`): 걸음 폭 = catStrideMeters × 몸 배율 / 1.9(화면 흔들림과 같은 박자), 3D 16m, 몸이 클수록 크고 낮게. 추격 배경음(고양이 248, `UI/WorldSfx.Music`): 어느 고양이든 Chase면 켜짐(페이드 인 0.6s·아웃 2.5s), 음량 = 음악 설정. 쥐·물건 소리(고양이 244, `UI/WorldSfx` — 스스로 생김): 찍찍(RatSqueak) · 큰 소음 파문(NoiseRipple, 깨짐 포함, 크기 40→절반·80↑→최대) 3D 1.5~30m · 정산 딸랑(StashedValue가 오를 때) 2D. 집 이벤트(고양이 245, `UI/WorldSfx.House`): 예고 딩동·삐·딸깍·덜컹·달그락·우르릉 2D 한 번, TV·청소기는 그 물건에서 3D 반복(고양이 269 — 청소기가 어디 있는지 소리로), 물소리는 Start~End 반복 2D(런이 끝나면 끔), 루틴 출발 방울·아기 부르기는 고양이 자리 3D. 집주인 부르기 "나~비야" 세 토막(설정 **보이스** 볼륨)·불 켜기 예고 쿵쿵쿵 발소리(고양이 267).
- 상태가 항상 소리로 먼저 들려야 한다 — 시야 밖 고양이의 상태를 소리로 읽는 것이 은신 플레이의 정보 구조.

## 수용 기준 (W6)

- [x] 그레이박스 방 2개에서: 순찰→소음 조사→목격 추격→포획→복귀 풀 사이클 (2026-09-24 고양이 44: 접시 깨짐 5m → Suspicious 8.2s·소리 지점 0.1m까지 → 목격 Chase → Capture → Return → Patrol)
- [x] 웅크려 뒤로 지나가면 미발각, 달리면 발각 재현 (고양이 44: 등 뒤 2.5m 웅크림 통과 게이지 0 / 1.5m에서 4.7s 달리면 1.4s에 Suspicious·게이지 73.8. 단, 한 번 스쳐 달려 지나가기(1s)는 1.5m 14.3·2.5m 8.4로 의심 전 — 귀만 돈다)
- [x] 추격 중 문 뒤로 도망 → 쫓던 고양이는 닫힌 문을 4s 밀어 연다(설계 변경, 고양이 40 — 3.9s 확인). 시야를 끊고 멀리 숨으면 3.2s 뒤 추격 포기 → Suspicious(고양이 44)
- [x] 털실뭉치로 추격 전 고양이 유인 성공 (고양이 44: 던진 털실 착지 0.5s 뒤 Distracted)
- [x] 4인 세션에서 고양이 위치·상태 동기화 자연스러움 (고양이 44: 호스트 + 빌드 클라 3개, 60s, 서버 시각 기준 위치 오차 평균 0.09~0.11m·p95 0.42~0.48m·최대 1.22m, 표시 지연 ≈0.1s(보정 시 평균 0.03m), 상태 불일치는 전이 순간 1~2회, 클라 예외 0)

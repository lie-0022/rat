# 09. 런 흐름 (W6–7)

> **2026-09-24 개편 (사용자 결정: 팀 대화 방향을 단계적으로 적용)** — 아래 "새 루프"가 목표다. 그 밑의 "기지 → 파밍 → 쥐구멍 귀환" 절들은 **현재 구현**이고, 새 루프 단계가 들어오면 하나씩 대체한다. 근거: `design/references/2026-09-24-team-direction.md`.

## 새 루프 (목표, 2026-09-24)

```
기지(로비) ──출발──▶ 스테이지 1 ──▶ 스테이지 2 ──▶ … ──▶ 스테이지 N ──▶ 엔딩
                     │ 출발방(벽 속 입구)에서 시작
                     │ 맵 반대편 목적지방(상점·식량 창고)까지 가며 음식을 모아 창고에 넣는다
                     │ 창고 적립 ≥ 할당량 + 살아 있는 전원이 목적지방 안 → 카운트다운 → 클리어
                     │ 할당량만큼은 가족이 먹음(차감), 남은 음식 = 상점 돈 → 다음 맵용 아이템 구매
                     └ 전원 쓰러짐 = 굶음(런 실패) → 결과 → 기지
```

- **컨셉**: 식량이 떨어져 굶는 쥐 가족. 음식이 곧 돈. 고양이·함정을 피해 모은다.
- **목적지방**: 출발방에서 가장 먼 방(생성기가 정함 — docs/10 생성기 v2). 안에 식량 창고(DepositZone 역할)와 상점.
- **할당량**: `quota(n) = stageQuotaBase + stageQuotaPerStage × (n − 1)`. 못 채우면 목적지에 모여도 못 넘어간다(시간 제한 없음 — 욕심 vs 생존은 그대로).
- **쓰러진 동료**: 몸을 목적지 창고에 넣으면 부활(지금 쥐구멍 부활과 같은 규칙, 자리만 목적지로).
- **엔딩**: 마지막 스테이지(`stagesPerRun`) 클리어 → 엔딩 화면(짧은 이야기) → 기지.

| 수치 (BalanceConfigSO) | 기본값 | 비고 |
|---|---|---|
| stagesPerRun | 5 | 잠정 |
| stageQuotaBase | 150 | 잠정 — 부엌 맵 한 장 전리품 합 약 600~1200 |
| stageQuotaPerStage | 75 | 잠정 |
| stageClearCountdownSeconds | 3 | 귀환 카운트다운과 같은 방식 |

### 잠정 결정 (대화에 없어 기본값으로 둔 것 — 사용자 확인 필요)

1. 할당량만큼 **차감**(가족이 먹음)하고 남은 음식이 상점 돈이 된다. 다음 스테이지로 이월.
2. 기지 자판기(영구 업그레이드)와 저장 누계는 유지 — 누계 = 런에서 가족에게 먹인 총량.
3. 스테이지 사이에 기지로 돌아가지 않는다(목적지 상점 → 바로 다음 맵).

### 구현 — 할당량 (2026-09-24, 고양이 63)

- `Run/StageQuota`(벽 속 스테이지의 RunManager에 붙음 — 창고·부엌은 예전 규칙): NV StageNumber·StagesPerRun·Quota·Pantry. 스폰 때 `RunSession.StageNumber`로 할당량 계산.
- `RunManager`: 전원 창고 집합이어도 **적립 < 할당량이면 카운트다운 안 함**. 클리어 정산 = `StageQuota.ServerOnCleared(haul)` — 할당량만큼 누계(저장), 나머지 `RunSession.Pantry`로. 마지막 스테이지면 런 초기화(엔딩 화면은 단계 5). 전멸 = `ServerOnWiped` → 런 초기화(굶음).
- ~~지금은 클리어 뒤 기지로~~ → 단계 5에서 바로 다음 맵.

### 구현 — 스테이지 연속·엔딩 (2026-09-24, 고양이 64)

- 클리어 결과 화면 뒤 `StageQuota.NextSceneAfterResult` → 마지막이 아니면 **같은 씬(벽 속)을 다시 로드**(새 시드 = 새 맵), `RunSession.DepartPending`으로 전원 로드 뒤 자동 출발·맵 생성. 마지막이면 기지.
- `StageQuota.Finished` NV — 결과 화면(ResultPanelWidget) 문구: 클리어 `스테이지 n 클리어! / 가족이 q만큼 먹었어요 — 남은 식량 p / N초 뒤 다음 맵으로`, 엔딩 `배불리 겨울을 났다! / 스테이지 N개를 모두 넘어 … — 엔딩 / N초 뒤 기지로`, 전멸 `굶었다... / 스테이지 n에서 전원 쓰러짐 — 처음부터 다시`. 엔딩 이야기는 기획이 채울 자리.
- 검증: 1인 — 1스테이지 클리어 → "7초 뒤 다음 맵으로" → 기지 없이 새 맵(시드 바뀜)·스테이지 2 자동 시작, 마지막 스테이지(강제 5/5) 클리어 → 엔딩 문구 → 기지·런 초기화. 2인 — 클리어 뒤 둘 다 새 맵, 클라 NetworkObject 111/111 → 141/141, 클라 출발방, 클라 "할당량 연출: 스테이지 2/5 식량 225 남은 식량 30", 예외 0.
- HUD(RunBar): `스테이지 n/N   식량 적립/할당량`, 창고에 왔는데 모자라면 `식량이 모자라요 — N 더 모아 창고에`, 충분하면 `창고에 모이면 다음으로 k/n` → `다음으로… 3`.
- 검증: 1인 — 0으로 창고에 서면 막힘(안내 문구), 154 적립 → 다음 → 기지, 스테이지 2·남은 식량 4·누계 +150. 다시 출발 → 할당량 225·스테이지 2/5·남은 식량 4. 쓰러짐 → 전멸 → "굶음" → 스테이지 1·남은 식량 0. 2인 — 클라 "할당량 연출: 스테이지 1/5 식량 150", 둘이 모여도 0이면 StageActive 유지, 177 → 다음(남은 27) → 기지, 예외 0.

### 단계 (구현 순서 — production/loop-state.md)

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 생성기 v2: 갈래·고리·막다른 방 그래프, 출발방·목적지방 (docs/10) | ✅ 고양이 62 |
| 2 | 목적지방 창고 적립 + 도착 판정 (쥐구멍 귀환 대체) | ✅ 고양이 62 (벽 속: 창고가 목적지방에) |
| 3 | 할당량 + HUD, 클리어/실패 | ✅ 고양이 63 |
| 4 | 목적지 상점 (음식으로 다음 맵 아이템) | 예정 |
| 5 | 스테이지 연속 + 엔딩 | ✅ 고양이 64 |

---

## (현재 구현) 기지 → 파밍 → 쥐구멍 귀환

> **2026-09-13 개편**: 할당량·탈출 투표 삭제. 레포식 **"기지 → 파밍 → 살아서 귀환"** 루프.
> **2026-09-14**: 스테이지 번호·구분 없음. 누계는 계속 쌓이고 **초기화되지 않으며 저장된다** (전멸해도 유지).

## 루프

```
기지 ──출발 발판──▶ 스테이지(파밍) ──원할 때 쥐구멍에 전원 집합──▶ 귀환 정산(누계에 더하고 저장) ──▶ 기지 ──▶ 다시 출발 …
                     전원 다운 = 전멸 → 이번 파밍 적립만 잃고 기지로 (누계 유지)
```

- 목표 금액 없음. 언제 돌아갈지는 팀이 정한다 — 욕심(더 파밍) vs 생존(고양이)이 긴장의 축.
- 스테이지 번호·단계 구분 없음 — 매번 같은 "파밍 나가기". 누계만 쌓인다.

## Run/RunManager.cs — 스테이지의 심장 (호스트 권한, 스테이지 씬 수명)

```csharp
public enum RunPhase { Ready, StageActive, Returning, Returned, Wiped }

public class RunManager : NetworkBehaviour
{
    public NetworkVariable<RunPhase> Phase;
    public NetworkVariable<int> RunTotalValue;       // 저장된 누계 (RunSession → SaveService)
    public NetworkVariable<int> StashedValue;        // 이번 파밍 쥐구멍 적립
    public NetworkVariable<int> ReturnReadyCount;    // 쥐구멍 위 인원
    public NetworkVariable<int> ReturnNeededCount;   // 귀환에 필요한 인원 (다운 제외)
    public NetworkVariable<double> ReturnAt;         // 귀환 카운트다운 끝 (ServerTime)
    public NetworkVariable<int> ResultCarriedValue;  // 귀환 시 들고 온 가치
    public NetworkVariable<double> ResultEndsAt;     // 결과 화면 끝 (ServerTime)
    public NetworkList<PlayerContribution> Contributions; // 플레이어별 기여 — 결과 화면 "오늘의 쥐들" (2026-09-16)
        // { ClientId, DepositedValue, DepositCount, CarriedValue, Downed }
        // 출발 시 접속 인원으로 채움 · 적립 시 마지막으로 놓은 쥐(LastCarrierId)에 가산 ·
        // 귀환 시 각자 손·주머니 가치(여럿이 든 대형은 먼저 센 한 명) + 다운 여부 · 전멸 시 전원 Downed

    public void ServerStartStage(int seed);          // 기지 발판으로 들어오면 자동 호출
    public void ServerDeposit(int value, ulong by);  // DepositZone(쥐구멍)이 호출
    public void ServerRevive(ulong clientId);
}
```

## 페이즈 흐름

```
Ready        출발 대기. 기지 출발 발판으로 스테이지 씬이 로드되면 전원 로드 완료 시 시작 위치(PlayerSpawn)로 옮기고 자동 StageActive
             (에디터에서 스테이지 씬을 직접 플레이할 때만 호스트 Enter — 개발용)
StageActive  파밍. 쥐구멍에 내려놓은 물건은 즉시 StashedValue에 적립 (안전 보관)
             다운 안 된 전원이 쥐구멍 위 → Returning
Returning    returnCountdownSeconds 카운트다운. 한 명이라도 벗어나면 → StageActive (취소)
             카운트 끝 → Returned
Returned     귀환 정산: 이번 수확 = 쥐구멍 적립 + 귀환자가 손·주머니에 든 물건 가치 → 누계에 더하고 즉시 저장
             → 결과 화면(resultScreenSeconds) → 기지(Hub) 로드
Wiped        StageActive/Returning 중 전원 Downed → 이번 적립 잃음(누계 유지) → 전멸 결과 화면 → 기지(Hub) 로드
```

- 출발 직후 쥐구멍 위에 선 채로 곧장 귀환되지 않게, **출발 후 누군가 한 번 쥐구멍을 벗어나야** 귀환 판정이 시작된다 (스테이지 시작 위치가 쥐구멍 근처여도 안전).
- 쓰러진 사람은 귀환 인원에서 빠진다 — 두고 갈 수 있다. 그 사람이 떨어뜨린 물건은 정산 안 됨.
- 메타 보상(치즈코인) 규칙은 미정 — 누계를 어디에 쓸지는 기지 기능 구성 때 정한다.
- 난이도 변화 규칙은 미정 (스테이지 구분이 없으므로 맵·고양이 단계에서 다시 정한다).

## 저장 (Core/SaveService — docs/11)

- 호스트 기기 `Application.persistentDataPath/save.json` (+ 직전본 `save.bak`). `SaveData { Version, HaulTotal }`.
- 누계 = `RunSession.TotalValue` = `SaveService.Data.HaulTotal`. 귀환 정산 시 `RunSession.AddHaul` → 즉시 저장.
- 팀 누계는 **호스트(세션 주인) 저장**에만 쌓인다 — 레포식. 클라 개인 저장(코인·스킨 등)은 메타 단계(docs/11).

## 다운·부활·전멸

- EventBus.PlayerDowned → 다운 안 된 사람이 없으면 Wiped (이번 파밍 실패).
- 부활(`ServerRevive`): 다운 몸이 쥐구멍 진입 시 (08 DepositZone 분기). 부활 후 Active, 스태미나 50.
- 솔로 플레이: 다운 = 즉시 Wiped (운반해줄 동료 없음). 대신 솔로 보정(고양이 시야 -20%)이 안전망.

## RunResult (구조체 — EventBus.RunEnded 페이로드, 전멸 시 발화)

```csharp
public struct RunResult
{
    public int TotalValue;         // 저장된 누계 (전멸해도 유지)
    public int CoinsAwarded;       // 메타 보상 미정 — 0
    public Dictionary<ulong,int> DepositCounts;  // 결과 화면 "오늘의 일꾼" 표시용
    public List<string> NewCodexIds;
}
```

## 기지 복귀

- 결과 화면 뒤 호스트: 전원 손·주머니 드랍 + 상태이상 초기화(다운 → Active) → GameState = Lobby → 기지(`RunManager._baseScene`, 기본 Hub) 로드.
- 기지 도착 시 DeparturePad가 전원 로드 완료(OnLoadEventCompleted) 후 전원을 기지 PlayerSpawn으로 옮긴다 (`Player/PlayerPlacement` — 스테이지 출발과 공용).
- 기지 화면에 누계 표시 — `DeparturePad.TotalValue`(호스트가 스폰 시 저장값으로 채움)를 클라가 읽는다.
- 플레이어 오브젝트는 씬을 넘어 유지된다 — 위치·손·상태를 씬 전환마다 명시적으로 정리하는 이유.

## 수용 기준 (W7)

- [ ] 파밍 → 쥐구멍 적립 → 전원 집합 → 카운트다운 → 귀환 정산 → 기지 (4인) — (2026-09-24 고양이 50) **2인 ✅**: 출발 → 치즈 적립 14 → 둘 다 쥐구멍 0.5s 뒤 Returning(2/2) → 귀환 누계 97523→97537(save.json 기록) → 결과 8s → Hub 씬, 둘 다 접속 유지. 4인은 미검증(같은 집계 로직)
- [x] 카운트다운 중 한 명 이탈 시 취소 (고양이 50: 클라가 쥐구멍 밖으로 → StageActive)
- [x] 쓰러진 동료를 두고 귀환 가능 (귀환 인원에서 제외) (고양이 50: 클라 다운 → 필요 1/준비 1 → 2.4s 뒤 귀환)
- [x] 전멸 → 이번 적립만 잃고 기지로, 누계 유지 (고양이 50: 적립 14 잃음·누계 97537 유지·Hub로·둘 다 Active)
- [x] 게임을 껐다 켜도 누계 유지 (save.json) (고양이 50: 플레이 재시작 시 "저장 불러오기: 누계 97537". 테스트 뒤 원래 저장 97523으로 되돌림)
- [x] 다운→운반→부활 후 계속 플레이 (고양이 46)
- [x] 런 도중 클라 이탈 시 런 계속, 아이템 드랍 처리 (03 문서) (고양이 50: 스테이지 중 빌드 클라 프로세스 강제 종료 → 호스트 StageActive 유지·귀환 필요 1명·호스트 에러 0. 드랍은 접속 해제 처리(기존)가 담당 — 클라가 든 물건 상태로는 미검증)

## 운영 메모 (2026-09-13~14)

- 에디터에서 스테이지 씬을 직접 플레이하면 Ready에서 호스트 Enter로 출발 (개발용). 결과 화면 뒤엔 정식 흐름대로 기지로 간다.
- 결과·진행 수치는 RunManager NetworkVariable로 복제 — `EventBus`는 호스트 로컬이라 클라 HUD용으로 쓸 수 없음.
- 씬 전환 전 모든 플레이어 `ServerForceDrop` + 상태 초기화 (손·주머니 참조가 다음 씬으로 새지 않게).
- 쥐구멍 위 판정: 쥐구멍 BoxCollider의 수평 범위 안 + 높이 여유 (트리거가 바닥에 얇게 깔려 있음).

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| departCountdownSeconds (기지 발판 전원 집합 후 출발까지) | 3 s |
| returnCountdownSeconds (전원 집합 후 귀환까지) | 3 s |
| resultScreenSeconds (결과 화면 표시) | 8 s |

## 경계도 디렉터 (2026-09-24, `Run/RunDirector`, design/cat-ideas/12)

런의 긴장 곡선을 조절하는 보이지 않는 감독. 호스트 전용 MonoBehaviour — RunManager가 서버 스폰 때 런타임에 붙인다(프리팹·싱글톤 추가 없음). 표시 없음(고양이 연출로만 읽힌다).

- **긴장 0~100**: 고양이 Suspicious +10 · Chase +25 · Toy +20, 쥐 다운 +40, 소음 ≥40 +5. 초당 -1. 추격·놀이·다운 = "위기" 시각.
- **10s마다 판단**: Phase Returning → Finale / Relief 진행 중(20~40s)이면 유지 / 긴장 ≥70 → Relief / 긴장 <25 이고 마지막 위기 뒤 60s → Build-up / 그 밖 Calm.
- 손잡이는 docs/07 "경계도 디렉터 손잡이" 표.

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| directorTickSeconds | 10 |
| tensionSuspicious / Chase / Toy / Down / Noise | 10 / 25 / 20 / 40 / 5 (소음 ≥40) |
| tensionDecayPerSec | 1 |
| buildupThreshold / buildupQuietSeconds | 25 / 60 s |
| reliefThreshold / reliefSecondsRange | 70 / 20~40 s |
| buildupSleepMul / DwellMul / MemoryBonus | 0.7 / 0.6 / +0.3 |
| reliefRoutineWeightMul / LookWeightMul / DwellMul | 4 / 0.3 / 1.5 |
| finaleHoleBias / Radius | 0.6 / 4 m |

## 집주인 이벤트 (2026-09-24, `Run/HouseEventDirector`, design/cat-ideas/10)

고양이 밖에서 오는 사건으로 판의 막을 나눈다. 인간은 안 보인다 — 알림은 `RunManager.HouseEventClientRpc` → `EventBus.HouseEvent` → 토스트(목소리는 NoiseSystem이 아니다 — 고양이가 반응하면 안 되는 소리). 호스트 전용 MonoBehaviour, RunManager가 서버 스폰 때 런타임에 붙인다.

- 스케줄: Phase StageActive에서 스테이지당 2~4건, 첫 사건 90~150s, 간격 180~300s, 같은 사건 연속 금지(부르기·밥·청소기·TV·불 켜짐·창문 중 직전 것 빼고 무작위 — 청소기·TV·불 켜짐·창문은 맵에 해당 물건·어둠 구역이 있을 때만). 경계도 디렉터가 Relief면 마지막 사건 뒤 90s 지났을 때 부르기를 앞당긴다. `ServerTrigger(kind)`로 수동 발동(테스트·초인종 등).
- 사건마다 예고 3s → 시작.

| 사건 | 예고 토스트 | 시작 토스트 | 고양이 |
|---|---|---|---|
| 부르기 CallAway | 멀리서 "나비야~ 간식!" | 고양이가 나갔다! 지금이야 | 문으로 나가 20~40s 부재 → 랜덤 문으로 복귀("고양이가 돌아왔다… 어느 문으로?") |
| 밥 시간 Feeding | 부엌에서 그릇 달그락 — 밥 시간 | 고양이가 밥 먹는 중 — 부엌만 피해 | Food 스팟으로 서둘러 가서 남은 시간(25s)만큼 먹음 |
| 청소기 Vacuum (2026-09-24) | 삐— 로봇청소기가 켜졌다 | 청소기 소리에 발소리가 묻힌다 — 지금 뛰어! (끝: 청소기 멈춤 — 다시 조용히) | 청소기에서 가장 먼 스팟(문 제외)으로 피신해 끝날 때까지 웅크림(감각 0.6). 청소기(`World/RobotVacuum`, 키네마틱·NetworkTransform)는 충전대 → 방 둘레 사각 루프 1.5 m/s·30s → 충전대, 바닥 물건을 밀고 다님. 도는 동안 `NoiseSystem.SetMask(청소기, 40)` — 그 미만 소리(발소리·작은 충돌·찍찍)는 고양이가 못 들음 |
| TV (2026-09-24) | 리모컨 딸깍 — TV가 켜졌다 | 고양이가 TV 앞에 앉았다 — 등 뒤로 지나가 (끝: TV 꺼짐) | TV(`World/TvSet`, `On` NV·화면 깜빡임) 앞 2.5m 시청 지점으로 가서 끝날 때까지 앉아 TV만 본다(감각 1, 매 프레임 TV 쪽). 등 뒤는 시야 밖 — 거실 통과 창. TV 앞을 가로지르면 들킨다. 90s 동안 소리 25 미만 묻힘. 청소기가 켜지면 청소기 피신이 우선 |
| 불 켜짐 LightOn (2026-09-24) | 쿵쿵 — 집주인 발소리, 어두운 방에 불이 켜진다 | 딸깍 — 불 켜짐! 어둠이 사라졌다 (끝: 불 꺼짐 — 다시 어둠) | 꺼진 어둠 구역(`World/LightZone`) 중 무작위 하나가 30s 밝아진다(`ServerLightFor`) — 어둠 은신 붕괴. 고양이는 그 구역 중심으로 와서 집주인 다리에 10s 부빈다(감각 0.5). 예고 3s = 그 방에서 나올 시간 |
| 창문 Window (2026-09-24) | 덜컹 — 집주인이 창문을 연다 | 바람! 냄새가 날아가고 가벼운 물건이 굴러간다 (끝: 창문 닫힘) | 창문(`World/WindowWind`, `Open` NV·창문 판 회전) 20s: 냄새 자국을 매초 전부 지움, 2s마다 돌풍 — 풀린 가벼운 물건(질량 ≤1, 대형 아님)에 바람 방향 3 m/s(±30°, ×0.7~1.3)+위로 절반. 굴러가는 물건은 고양이 호기심(03)을 끈다. 바람 소리 20 미만 묻힘. 고양이 반응은 따로 없다 — 물건이 알아서 끈다 |

고양이를 스팟이 아닌 곳에 앉히는 사건(TV·불 켜짐)은 `CatBrain.ServerGoSit(지점, 바라볼 곳?, 포기 시각, 머무름, 감각)` 하나로 처리한다.

소리 마스킹은 소스별(`NoiseSystem.SetMask(source, loudness)`)이고 실제 값은 켜진 소스의 최댓값 — 청소기 40과 TV 25가 겹치면 40, 청소기가 꺼지면 25, 둘 다 꺼지면 0. 판이 바뀌면 고양이 스폰 때 `ClearMasks()`.

- **초인종** (2026-09-24, `World/Doorbell`): 쥐가 E 1s 홀드로 누름(런당 1회, `Used` NV) → 예고 "딩동! 누가 초인종을…" → 시작 "집주인이 현관으로 — 고양이도 따라갔다!" → 부르기와 같은 부재. 스케줄 몫(2~4건)은 안 깎는다 — 쥐가 유발한 사건.

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| houseEventsPerStage | 2~4 |
| houseEventFirstDelay / Gap | 90~150 s / 180~300 s |
| houseEventWarnSeconds | 3 |
| catCallAwaySeconds | 20~40 |
| catFeedingSeconds | 25 |
| catAwayWalkSpeed | 3 |
| reliefCallAwayMinGap | 90 s |

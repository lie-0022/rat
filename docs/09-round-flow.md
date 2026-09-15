# 09. 런 흐름 (W6–7)

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

- [ ] 파밍 → 쥐구멍 적립 → 전원 집합 → 카운트다운 → 귀환 정산 → 기지 (4인)
- [ ] 카운트다운 중 한 명 이탈 시 취소
- [ ] 쓰러진 동료를 두고 귀환 가능 (귀환 인원에서 제외)
- [ ] 전멸 → 이번 적립만 잃고 기지로, 누계 유지
- [ ] 게임을 껐다 켜도 누계 유지 (save.json)
- [ ] 다운→운반→부활 후 계속 플레이
- [ ] 런 도중 클라 이탈 시 런 계속, 아이템 드랍 처리 (03 문서)

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

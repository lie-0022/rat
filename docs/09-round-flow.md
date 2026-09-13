# 09. 런 흐름 (W6–7)

> **2026-09-13 개편**: 할당량·탈출 투표 삭제. 레포식 **"기지 → 스테이지 파밍 → 살아서 귀환"** 루프. 전멸하면 런 종료.

## 루프

```
기지 ──출발──▶ 스테이지(파밍) ──원할 때 쥐구멍에 전원 집합──▶ 귀환 정산 ──▶ 기지 ──▶ 다음 스테이지 …
                     전원 다운 = 전멸 → 런 종료 (처음부터)
```

- 목표 금액 없음. 언제 돌아갈지는 팀이 정한다 — 욕심(더 파밍) vs 생존(고양이)이 긴장의 축.
- 스테이지는 끝없이 이어진다. 런이 끝나는 건 전멸뿐.

## Run/RunManager.cs — 런의 심장 (호스트 권한, 런 씬 수명)

```csharp
public enum RunPhase { Ready, StageActive, Returning, Returned, Wiped }

public class RunManager : NetworkBehaviour
{
    public NetworkVariable<RunPhase> Phase;
    public NetworkVariable<int> StageNumber;         // 1부터
    public NetworkVariable<int> RunTotalValue;       // 기지로 가져온 런 누계
    public NetworkVariable<int> StashedValue;        // 이번 스테이지 쥐구멍 적립
    public NetworkVariable<int> ReturnReadyCount;    // 쥐구멍 위 인원
    public NetworkVariable<int> ReturnNeededCount;   // 귀환에 필요한 인원 (다운 제외)
    public NetworkVariable<double> ReturnAt;         // 귀환 카운트다운 끝 (ServerTime)
    public NetworkVariable<int> ResultCarriedValue;  // 귀환 시 들고 온 가치
    public NetworkVariable<double> ResultEndsAt;     // 결과 화면 끝 (ServerTime)

    public void ServerStartStage(int seed);          // 기지에서 출발
    public void ServerDeposit(int value, ulong by);  // DepositZone(쥐구멍)이 호출
    public void ServerRevive(ulong clientId);
}
```

## 페이즈 흐름

```
Ready        출발 대기 (기지 자리 — 기지 씬 전까지는 샌드박스에서 호스트 Enter)
StageActive  파밍. 쥐구멍에 내려놓은 물건은 즉시 StashedValue에 적립 (안전 보관)
             다운 안 된 전원이 쥐구멍 위 → Returning
Returning    returnCountdownSeconds 카운트다운. 한 명이라도 벗어나면 → StageActive (취소)
             카운트 끝 → Returned
Returned     귀환 정산: 이번 수확 = 쥐구멍 적립 + 귀환자가 손·주머니에 든 물건 가치 → 런 누계에 더함
             → 결과 화면(resultScreenSeconds) → 기지(지금은 런 씬 재로드) → 다음 스테이지 Ready
Wiped        StageActive/Returning 중 전원 Downed → 전멸 결과 화면 → 런 기록 초기화 → 새 런 Ready
```

- 출발 직후 쥐구멍 위에 선 채로 곧장 귀환되지 않게, **출발 후 누군가 한 번 쥐구멍을 벗어나야** 귀환 판정이 시작된다 (스테이지 시작 위치가 쥐구멍 근처여도 안전).
- 쓰러진 사람은 귀환 인원에서 빠진다 — 두고 갈 수 있다. 그 사람이 떨어뜨린 물건은 정산 안 됨.
- 전멸 시 이번 스테이지 적립분은 잃는다. 결과 화면에는 도달 스테이지·기지로 가져온 누계만 표시.
- 메타 보상(치즈코인) 규칙은 미정.
- 스테이지 간 기록(스테이지 번호·누계)은 `RunSession`(호스트 프로세스 static)에 둔다 — 귀환 시 런 씬을 다시 로드하므로 씬 오브젝트인 RunManager에 둘 수 없다. 플레이 시작·전멸 시 초기화.
- 스테이지가 오를수록 어려워지는 규칙(고양이 수·전리품)은 스테이지 생성(10 문서) 단계에서 정한다.

## 다운·부활·전멸

- EventBus.PlayerDowned → 다운 안 된 사람이 없으면 Wiped (런 종료).
- 부활(`ServerRevive`): 다운 몸이 쥐구멍 진입 시 (08 DepositZone 분기). 부활 후 Active, 스태미나 50.
- 솔로 플레이: 다운 = 즉시 Wiped (운반해줄 동료 없음). 대신 솔로 보정(고양이 시야 -20%)이 안전망.

## RunResult (구조체 — EventBus.RunEnded 페이로드, 전멸 시 발화)

```csharp
public struct RunResult
{
    public int ZonesCleared;       // 귀환에 성공한 스테이지 수
    public int TotalValue;         // 기지로 가져온 런 누계
    public int CoinsAwarded;       // 메타 보상 미정 — 0
    public Dictionary<ulong,int> DepositCounts;  // 결과 화면 "오늘의 일꾼" 표시용
    public List<string> NewCodexIds;
}
```

## 기지 복귀

- 호스트: NetworkSceneManager로 기지(Hub) 씬 로드 → GameState = Lobby → 출발 시 다음 스테이지 로드.
- 기지 씬 구현 전까지는 런 씬을 다시 로드해 Ready로 대기한다 (아래 임시 규칙).

## 수용 기준 (W7)

- [ ] 파밍 → 쥐구멍 적립 → 전원 집합 → 카운트다운 → 귀환 정산 → 다음 스테이지 (4인)
- [ ] 카운트다운 중 한 명 이탈 시 취소
- [ ] 쓰러진 동료를 두고 귀환 가능 (귀환 인원에서 제외)
- [ ] 전멸 → 런 종료 → 새 런은 스테이지 1·누계 0
- [ ] 다운→운반→부활 후 계속 플레이
- [ ] 런 도중 클라 이탈 시 런 계속, 아이템 드랍 처리 (03 문서)

## 샌드박스 단계 임시 규칙 (2026-09-13 — 기지 씬 구현 전)

- Ready에서 **호스트 Enter = 출발**.
- 귀환·전멸 결과 화면 `resultScreenSeconds` 뒤 **현재 런 씬을 다시 로드**(물건·RunManager 초기화). 귀환이면 `RunSession`이 다음 스테이지 번호·누계를 넘겨주고, 전멸이면 초기화된다. 기지 씬 구현 시 이 지점을 기지 씬 로드로 교체.
- 결과·진행 수치는 RunManager NetworkVariable로 복제 — `EventBus`는 호스트 로컬이라 클라 HUD용으로 쓸 수 없음.
- 씬 재로드 전 모든 플레이어 `ServerForceDrop` (손·주머니 참조가 다음 씬으로 새지 않게).
- 쥐구멍 위 판정: 쥐구멍 BoxCollider의 수평 범위 안 + 높이 여유 (트리거가 바닥에 얇게 깔려 있음).

| 수치 (BalanceConfigSO) | 값 |
|---|---|
| returnCountdownSeconds (전원 집합 후 귀환까지) | 3 s |
| resultScreenSeconds (결과 화면 표시) | 8 s |

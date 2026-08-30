# 09. 런 흐름 (W6–7)

## Run/RunManager.cs — 런의 심장 (호스트 권한 싱글톤, 런 씬 수명)

```csharp
public enum RunPhase { Loading, ZoneActive, ZoneCleared, Voting, Transition, Extracted, Wiped }

public class RunManager : NetworkBehaviour
{
    public NetworkVariable<int> CurrentZoneIndex;      // 0-base
    public NetworkVariable<int> CurrentQuota;
    public NetworkVariable<int> DepositedValue;        // 이번 존 정산 누계
    public NetworkVariable<RunPhase> Phase;
    public NetworkVariable<int> RunSeed;

    // 호스트 API
    public void ServerStartRun(ZoneDefinitionSO[] zones, int seed);
    public void ServerDeposit(int value);              // DepositZone이 호출
    public void ServerRevive(ulong clientId);
    public void ServerPlayerDowned(ulong clientId);    // 전멸 체크
    public void ServerBeginExtractionVote();
}
```

## 페이즈 흐름

```
Loading      호스트: 씬 로드 → ZoneGenerator(시드) → NavMesh 베이크 → 플레이어 스폰(쥐구멍) → ZoneActive
ZoneActive   플레이 본편. DepositedValue ≥ CurrentQuota → ZoneCleared
ZoneCleared  쥐구멍 개방 연출 + 10s 후 Voting
Voting       "더 간다 / 나간다" 전원 투표 (30s 제한, 미투표=나간다)
             과반 "더 간다" → Transition / 아니면 → Extracted
Transition   다음 존 생성(이어 붙이거나 교체 — 10 문서) → ZoneActive
Extracted    결과 정산 (08 공식) → RunResult 화면 → Hub 복귀
Wiped        전원 Downed 시 → 30% 정산 → RunResult → Hub
```

- 할당량: `quota(n) = 100 × 1.6^n` (00 문서), 솔로 ×0.55, 2인 ×0.75, 3인 ×0.9.
- 시간 제한 없음 (v1) — 대신 존이 길어지면 고양이 Patrol 범위가 서서히 넓어진다(soft pressure, catCount 유지).

## 다운·부활·전멸

- `ServerPlayerDowned`: EventBus.PlayerDowned + 전원 Downed 검사 → 전멸이면 Wiped.
- 부활(`ServerRevive`): 다운 몸이 쥐구멍 진입 시 (08 DepositZone 분기). 부활 후 Active, 스태미나 50.
- 솔로 플레이: 다운 = 즉시 Wiped (운반해줄 동료 없음). 대신 솔로 보정(고양이 시야 -20%)이 안전망.

## Run/ExtractionVote.cs

```csharp
public class ExtractionVote : NetworkBehaviour
{
    public NetworkList<VoteEntry> Votes;   // {clientId, bool goDeeper}
    [ServerRpc(RequireOwnership=false)] public void CastVoteServerRpc(bool goDeeper);
    // 전원 투표 or 30s → RunManager에 결과 통보. HUD는 실시간 표 집계 표시
}
```

- 투표 UI는 강제 모달 아님 — 화면 상단 배너 (플레이 지속 가능). "한 명이 몰래 더 가기 누르기" 같은 트롤 대화 유발.

## RunResult (구조체 — EventBus.RunEnded 페이로드)

```csharp
public struct RunResult
{
    public bool Extracted;         // false = 전멸
    public int ZonesCleared;
    public int TotalValue;
    public int CoinsAwarded;
    public Dictionary<ulong,int> DepositCounts;  // 결과 화면 "오늘의 일꾼" 표시용
    public List<string> NewCodexIds;
}
```

## 허브 복귀

- 호스트: NetworkSceneManager로 Hub 로드 → GameState = Lobby → MetaWallet 지급 + SaveService.Save().
- 결과 화면(12 문서)은 Hub 로드 후 오버레이로 표시 (씬 전환 대기 없음).

## 수용 기준 (W7)

- [ ] 그레이박스 존 2개로: 입장→정산→클리어→투표→다음 존→탈출 풀 런 4인 완주
- [ ] 전멸 → 30% 정산 → Hub 복귀
- [ ] 다운→운반→부활 후 계속 플레이
- [ ] 투표 타임아웃·과반 계산 엣지 케이스 (2인 1:1 → 나간다)
- [ ] 런 도중 클라 이탈 시 런 계속, 아이템 드랍 처리 (03 문서)

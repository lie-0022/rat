# 06. 소음 시스템 (W4) — 은신의 뼈대

## 설계

- 전역 정적 시스템. **호스트에서만 판정** (리스너가 고양이뿐이므로).
- 소음은 이벤트(발생 즉시 소비), 지속음 없음. 단순하게 유지.

## Noise/NoiseSystem.cs

```csharp
public static class NoiseSystem
{
    // 호스트에서만 호출. loudness: 0~100
    public static void Emit(Vector3 pos, float loudness, NoiseType type, ulong sourceClientId = 0);

    public static event Action<NoiseEvent> OnNoise;   // CatSenses가 구독
}

public enum NoiseType { Footstep, Impact, Break, Squeak, Trap, Item, PlayerMic }
public struct NoiseEvent { public Vector3 Pos; public float Loudness; public NoiseType Type; public ulong Source; }
```

- 전파 반경 = `loudness / 100 * maxNoiseRadius(14m)`. 반경 내 리스너에게 전달.
- 벽 감쇠: 리스너까지 Linecast(NoiseBlocker 레이어) — 벽 1장당 loudness 40% 감소 (레이 1회, 단순하게). 방 벽·문판은 NoiseBlocker 레이어에 둔다(시야도 막는다) — 데모 창고방 벽·문(고양이 45).
- 클라 시각화: loudness 40 이상이면 발생 지점에 파문 이펙트 ClientRpc (플레이어가 "들렸다"를 인지해야 긴장이 성립).
  - 구현 (2026-09-24, 고양이 45): 호스트 `NoiseSystem.Rippled` → `RunManager.NoiseRippleClientRpc` → `EventBus.NoiseRipple` → `Noise/NoiseRippleView`(씬마다 자동 생성)가 바닥에 전파 반경까지 0.7s 퍼지는 노란 고리(LineRenderer, 최대 8개 재사용).

## 소음원 기준표 (BalanceConfigSO)

| 소음원 | loudness | 발생 위치 |
|---|---|---|
| 걷기 | 8 (재질 하드플로어 ×1.5) | PlayerNoiseEmitter, 0.35s 간격 |
| 달리기 | 22 | 동일, 0.25s 간격 |
| 배관 안 달리기 (고양이 87) | 22 × `pipeEchoMultiplier` 1.6 = 35 | 동일 — 발 위 1.2m 안이 배관 천장이면(`World/PipeEcho`) 쇠관이 울림. 걷기·웅크림은 그대로 |
| 웅크려 이동 | 0 | — |
| 착지 (낙하 1m+) | 15 + 높이×5 | PlayerController |
| 스태미나 헐떡임 | 18 | PlayerStamina |
| Squeak | 30 | ClientRpc와 함께 |
| 아이템 충돌 | impact(m/s) × mass계수, 최대 70 | CarryableItem.OnCollisionEnter |
| 파손 | 60 | Fragile 파괴 |
| 쥐덫 격발 | 80 | TrapBase |
| Alarming 아이템 | 50 | 트레잇 |
| (P2) 마이크 입력 | 실측 dB 매핑 0~60, 옵션 기본 OFF | VoiceService — v1 구현 제외, 인터페이스만 |

- 아이템 충돌 mass계수: `Clamp(mass, 0.5, 3)`. 이미 유리병(fragile+중형)을 떨어뜨리면 loudness 50~70 = 고양이 직행.

## PlayerNoiseEmitter

- 소유 클라에서 발걸음 타이밍 계산 → 호스트로 묶어 보낼 필요 없이, **호스트가 각 플레이어의 속도·접지 상태로 직접 계산**
  (ClientNetworkTransform 위치는 호스트도 알고 있음). 클라 신뢰 불필요, RPC 절약.
- 바닥 재질: 발밑 레이캐스트로 PhysicMaterial 태그 확인 (hardfloor ×1.5, rug ×0.5).

## 고양이 연결 (07 문서와의 계약)

- CatSenses가 OnNoise 구독. `Loudness ≥ hearThreshold(10)`이면 의심 게이지 가산 (거리 반비례).
- NoiseType.Squeak/Break/Trap은 즉시 조사 상태 진입 트리거.

## 수용 기준 (W4)

- [ ] 회색 박스 씬에서 소음 반경 기즈모 시각화 (에디터 전용) — 코드 있음(`PlayerNoiseEmitter.OnDrawGizmos`, 최근 1.2s 소음 반경 와이어 구). Scene 뷰 눈 확인은 사람이.
- [x] 달리기 vs 웅크림으로 고양이 반응 차이 재현 (2026-09-24 고양이 44: 웅크림 게이지 0 / 달리기 1.4s에 의심)
- [x] 유리병 낙하 → 파문 이펙트 → 리스너 로그까지 전 체인 동작 (고양이 45: 유리병 대신 접시(깨짐 60) → 파문 1회·고리 표시·고양이 Suspicious, 발소리 22는 파문 0. 2인 클라 파문 로그)
- [x] 벽 뒤 소음 감쇠 확인 (고양이 45: 40 → 창고방 벽 너머 24, 열린 문 40, 닫힌 문 24)

## 냄새 자국 (2026-09-24, design/cat-ideas/06)

소리와 별개의 느린 채널. `Noise/ScentSystem` — NoiseSystem처럼 정적·호스트 전용 링 버퍼(32). 자국 = 위치·처음 강도·시각·남긴 쥐·순번, 강도는 선형 감쇠(-3/s).

| 원인 | 강도 | 간격 |
|---|---|---|
| 치즈류(Edible) 들기·주머니 | 60 | 1.5m (웅크리면 3m) |
| 웅덩이를 지나 젖음(20s) | 40 | 동일 — 치즈 없이도 (2026-09-24) |
| 고양이에게 찍힘(앙심) | 20 | 동일 |

리스너는 고양이 Track 상태(docs/07). 쥐 본인에게만 노란 점 표시.

## 소음 귀속 (2026-09-24)

`NoiseEvent.HasSource` 추가 — `Source`가 의미 있는지. 호스트 클라 id가 0이라 `Source == 0`만으로는 환경음과 구분이 안 됐다.
- `NoiseSystem.Emit(pos, loudness, type, ulong? source)`: 플레이어 소리(발소리·찍찍·착지·헐떡임·숨기)는 소유자 귀속, 환경음은 null.
- 물건 충돌·깨짐: `CarryableItem.AttributedClient` — 들고 있으면 첫 캐리어, 놓은·던진 지 `noiseAttributionSeconds`(3s) 안이면 마지막 캐리어, 그 밖 null.
- 쓰는 곳: 고양이 앙심(docs/07), 찍힌 쥐 청각 배율.

### 물그릇·웅덩이 (2026-09-24, `World/WaterBowl`)
- 쥐가 E 1s로 엎음(1회, 소음 30·엎은 쥐 귀속) → 반경 2.5m 웅덩이 60s. 웅덩이 안 쥐는 20s 젖음(자국 40), 웅덩이 안 냄새 자국은 매 프레임 지워진다(`ScentSystem.EraseInRadius`) — 양날의 도구.

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
- 벽 감쇠: 리스너까지 Linecast(NoiseBlocker 레이어) — 벽 1장당 loudness 40% 감소 (레이 1회, 단순하게).
- 클라 시각화: loudness 40 이상이면 발생 지점에 파문 이펙트 ClientRpc (플레이어가 "들렸다"를 인지해야 긴장이 성립).

## 소음원 기준표 (BalanceConfigSO)

| 소음원 | loudness | 발생 위치 |
|---|---|---|
| 걷기 | 8 (재질 하드플로어 ×1.5) | PlayerNoiseEmitter, 0.35s 간격 |
| 달리기 | 22 | 동일, 0.25s 간격 |
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

- [ ] 회색 박스 씬에서 소음 반경 기즈모 시각화 (에디터 전용)
- [ ] 달리기 vs 웅크림으로 고양이(임시 큐브 리스너) 반응 차이 재현
- [ ] 유리병 낙하 → 파문 이펙트 → 리스너 로그까지 전 체인 동작
- [ ] 벽 뒤 소음 감쇠 확인

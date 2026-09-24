# 고양이 7 — 장소 기억 + 앙심 (2026-09-24, 세트 2의 3단계)

## 목표
고양이가 런 안에서 **배운다**. 자주 들킨 자리로 순찰이 쏠리고(가서 킁킁), 자기를 골탕 먹인 쥐에게 앙심을 품는다. 학습은 반드시 보여야 한다 — 냄새 맡기 연출, "!!", 팀 칩 "찍힘". (design/cat-ideas/05)

## 설계
- `AI/CatBrain.Memory.cs` (partial, 호스트):
  - **장소 기억**: 2m 격자 열 지도. 쥐를 보는 동안 초당 +1, 들은 소음 ≥40이면 +loudness/40. 초당 -0.1 감쇠.
  - 순찰 다음 목적지를 고를 때 30% 확률로(연속 두 번 금지) 열 ≥3인 가장 뜨거운 칸으로 → 도착하면 킁킁 1.5s(`Sniffing` NV) → 그 칸 열 절반 → 다음 스팟.
  - **앙심**: 쥐별 0~100. 추격에서 놓침 +40, 들리는 곳에서 찍찍(Squeak) +15. 초당 -0.2 감쇠. 50 이상 중 최고 1명 = `GrudgeClientId`(+ `HasGrudge`) NV.
  - 효과(≥50): 그 쥐 시야 게인 ×1.5, 그 쥐 발소리·찍찍 청각 ×1.3, 여럿 보이면 그 쥐 우선(거리 ×0.6로 비교), 성격 추격 포기 시간 +2s.
  - 귀속이 분명한 사건만 센다: 파손·충돌 소음은 Source가 비고(0 = 환경) 호스트 id도 0이라 제외. 유인 속음(+20)·뇌물(-50)은 다음 단계.
- `CatSenses`: `TargetGainMultiplier(clientId)` 훅(시야 게인·타깃 선택·Footstep/Squeak 청각), `Heard` 이벤트(기억·앙심 입력).
- UI: SuspicionIndicator — 나를 쫓는데 내가 앙심 대상이면 "!!". TeamStatusRow — 멀쩡한데 찍혔으면 "찍힘" 칩(Danger). CatVisual — Sniffing이면 킁킁 모션.
- 런 종료 = 씬 전환으로 고양이가 새로 스폰 → 자동 초기화.
- 수치 (BalanceConfig "기억·앙심"): catMemoryCellSize 2 · catMemorySightPerSec 1 · catMemoryNoiseMin 40 · catMemoryDecayPerSec 0.1 · catMemoryPatrolChance 0.3 · catMemoryMinHeat 3 · catMemorySniffSeconds 1.5 · catGrudgeEscape 40 · catGrudgeSqueak 15 · catGrudgeDecayPerSec 0.2 · catGrudgeThreshold 50 · catGrudgeSightMul 1.5 · catGrudgeHearingMul 1.3 · catGrudgeGiveUpBonus 2.

## 검증
- 쥐를 한 자리에서 5s 보여 줌 → 그 칸 열 ≈5 → 순찰이 그 칸 방문·킁킁 로그(확률 30% — 강제로 확률 1 임시 설정해 확인).
- 추격 놓침 2회 → 앙심 80 → GrudgeClientId = 그 쥐, HUD "!!"·"찍힘". 시간이 지나면 50 아래로 → 해제.
- 앙심 쥐 시야 게인 비교(같은 거리에서 게이지 상승 속도 ×1.5).
- 2인: 클라 화면 "찍힘" 칩(NV 복제).

## 검증 결과 (2026-09-24)
- 1인(사냥꾼): 5m 앞 쥐 → 0.4s 만에 추격(그 사이 열 0.4). 순간이동으로 놓침 → 3.1s 뒤 "놓침" 앙심 40, Suspicious.
- 테스트 가산 +40 → 앙심 80 → HasGrudge·GrudgeClientId 0, 팀 칩 "찍힘", 다시 추격받자 SuspicionIndicator "!!".
- 기억 칸 (−8,−2)에 열 20 → 깨운 뒤 10.1s 만에 그 칸 도착·킁킁(Sniffing) → 끝나고 열 9.4(절반), Patrol 복귀. (확률 0.3 → 테스트 동안만 1)
- 감쇠(테스트 30/s) → 앙심 0·"앙심 풀림". 수치는 테스트 뒤 원복(0.3/0.2) 확인.
- 2인: 호스트가 클라(1)에게 앙심 80 → 클라 쪽으로 추격 → **클라 로그 "의심 표시: !! (Cat Chase)"**, 호스트 팀 목록에서 파랑 쥐(클라) "찍힘".
- 미검증: 시야 게인 ×1.5·청각 ×1.3·거리 ×0.6 우선 추적(코드 리뷰만), 찍찍 +15.

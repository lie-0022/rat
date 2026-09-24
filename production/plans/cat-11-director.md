# 고양이 11 — 경계도 디렉터 (2026-09-24, 세트 2의 7단계)

## 목표
런의 긴장 곡선을 조절하는 **보이지 않는 감독**. 너무 잘 풀리면 집이 예민해지고(Build-up), 너무 몰리면 잠깐 풀어 준다(Relief), 귀환 카운트다운엔 최고 경계(Finale). **고양이 감각 수치는 절대 안 건드린다** — 잠·머무름·순찰 목적지 같은 "관찰로 알 수 있는" 손잡이만 돌린다. (design/cat-ideas/12)

## 설계
- `Run/RunDirector` (호스트 전용 MonoBehaviour, 새 싱글톤 없음): RunManager가 서버 스폰 때 런타임에 붙인다(프리팹 수정 없음). CatBrain은 `RunManager.Instance.GetComponent<RunDirector>()`로 찾는다.
- **긴장 0~100**: 고양이 상태 전이(`CatBrain.ServerStateChanged` 정적 이벤트) — Suspicious +10, Chase +25, Toy +20 / 쥐 다운(EventBus) +40 / 큰 소음(≥40) +5. 초당 -1 감쇠. 추격·다운 = "위기" 시각 기록.
- 10s마다 판단:
  - Phase Returning → **Finale**
  - Relief 진행 중이면 유지(20~40s)
  - 긴장 ≥70 → **Relief**
  - 긴장 <25 이고 마지막 위기 뒤 60s → **Build-up**
  - 그 밖 → Calm
- 손잡이 (CatBrain이 반영, 모드는 NV로 클라에 안 보냄 — 디렉터는 안 보여야 한다):
  - Build-up·Finale: 잠 길이 ×0.7, Look 머무름 ×0.6, 기억 칸 방문 확률 +0.3, `Alert` NV → 꼬리 빠르게(예민해 보임).
  - Relief: 다음 스팟 고를 때 Groom·Sun·Bed 가중치 ×4, Look ×0.3, 루틴 머무름 ×1.5 → 고양이가 그루밍·햇볕으로 가서 조용.
  - Finale: 순찰 목적지 60%를 쥐구멍 4m 안 랜덤 지점으로, 호기심 무시.
- 수치 (BalanceConfig "디렉터") 20개 — 표는 docs/09·07.

## 검증
- 긴장 이벤트: 추격 → +25, 다운 → +40, 감쇠 확인.
- Build-up: 조용히 60s(테스트는 시간 단축) → 모드 Build-up, 고양이 Alert, Look 머무름·잠 단축 수치.
- Relief: 긴장 ≥70 → Relief, 다음 스팟 선택 분포가 Groom/Sun/Bed 쪽으로.
- Finale: Phase Returning → 순찰 목적지 쥐구멍 근처.

## 검증 결과 (2026-09-24)
- 디렉터 자동 부착(RunManager 서버 스폰) — 시작 Calm·긴장 0.
- 실제 추격 1회 → 긴장 34(의심 +10·추격 +25 − 감쇠), 위기 시각 갱신.
- Build-up(긴장 0 + 마지막 위기 100s 전 → Decide): Alert true, 잠 ×0.7, Look 머무름 ×0.6, 기억 확률 +0.3.
- Relief(긴장 90): Alert false, 루틴 머무름 ×1.5, 스팟 가중치 Bed 4.0 · Groom 3.2 · Sun 3.2 · Food 1.2 · Look 0.7(사냥꾼 1.5 × 0.3 × 원 1.5).
- Finale: Alert true, 호기심 무시, 쥐구멍 방문 64/100(편향 0.6).
- 2인: 빌드 클라 로그 "고양이 예민: True"(Alert 복제).
- 미검증: 실시간 60s 무위기 → 자동 Build-up(판단 경로는 Decide 직접 호출로 확인), Phase Returning → Finale 자동 전환(한 줄 조건 — 리뷰만).

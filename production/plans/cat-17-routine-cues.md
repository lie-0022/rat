# 고양이 17 — 루틴 예고 + 화장실·우다다 + 물 (2026-09-24, design/cat-ideas/02 중 빠진 조각)

## 목표
스팟 그래프(고양이 1)가 밥·햇볕·그루밍·잠자리 루틴을 이미 한다. 02에서 빠진 건 **읽히는 리듬**과 **깨지는 리듬**:
1. **예고**: 고양이가 루틴 스팟으로 출발할 때 들리는 소리(자막) — "배 꼬르륵(밥)", "모래 긁는 소리 — 곧 우다다!(화장실)", "창가 쪽 기지개(햇볕)", "하품(잠자리)", "할짝할짝(물)". 시야 밖에서 소리로 상태를 읽는다(필러 2). 고양이 18m 안의 쥐에게만.
2. **화장실 + 우다다(zoomies)**: Litter 스팟 12s(감각 0.3) → 10s 광란 — 속도 6.5(추격보다 빠름)로 반경 8m 랜덤 지점을 1.2s마다. 목적 없음 — 복도는 도박. 목격 전이는 그대로(코앞 쥐는 쫓음), 달리다 비누 밟으면 미끄러짐(고양이 10).
3. **물**: Water 스팟 8s(감각 정상).

## 설계
- `CatState.Zoomies`(enum 끝). Patrol ArriveAtSpot: Litter → Dwell(12, 0.3) + 머무름 끝나면 Zoomies, Water → Dwell(8, 1).
- 예고: `GoToNextSpot`에서 루틴 스팟(Food·Litter·Sun·Bed·Water)을 고르면 `CatCueClientRpc(kind, pos)` → `EventBus.CatCue` → ToastWidget(로컬 쥐가 고양이 18m 안일 때만, 같은 문구 연속 금지).
- 데모 레이아웃: Spot_Litter(-15,0,-15), Spot_Water(11,0,16).
- 수치: catLitterSeconds 12 · catLitterSense 0.3 · catZoomiesSeconds 10 · catZoomiesSpeed 6.5 · catZoomiesRadius 8 · catZoomiesStep 1.2 · catWaterSeconds 8 · catCueHearRange 18.

## 검증
- Litter 도착 → 12s → Zoomies 10s(속도 ≈6.5, 위치 변화) → Return.
- 예고: 고양이가 Food로 출발 → 18m 안 쥐 토스트 "배 꼬르륵", 멀면 없음.
- 2인: 클라 토스트(ClientRpc) + Zoomies 복제.

## 검증 결과 (2026-09-24)
- 화장실: Litter 스팟 도착 → Dwell·litterDone → 머무름 끝 → Zoomies 10.0s, 최고 속도 6.5, 시작점에서 최대 20.6m → Return.
- 예고(1인): 쥐가 고양이 8m 옆 → Food 예고 토스트 "(배 꼬르륵 — 고양이가 밥 먹으러)". 25m → Water 예고 안 뜸.
- 2인: 클라 쥐를 고양이 6m 옆으로 → Litter 예고 → 빌드 클라 로그 "(모래 긁는 소리… 곧 우다다!)".
- 미검증: 실제 순찰 중 자연 발생 빈도(스팟 가중치 Litter·Water 0.6), 우다다 중 비누 미끄러짐.

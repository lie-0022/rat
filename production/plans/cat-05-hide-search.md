# 고양이 5 — 숨을 곳 + 수색 (2026-09-24, 세트 2의 1단계)

## 목표
추격의 끝이 "잡힘 / 뿌리침" 둘뿐이 아니게. 쥐는 장화·빈 상자에 숨고, 고양이는 마지막 목격점 근처를 킁킁 수색한다. 숨기는 두 번째 생존 수단이지만 안전하지 않다: 확률로 들키고, 나올 때 소리가 난다. (design/cat-ideas/14)

## 설계
- `ConditionState.Hidden` (enum 끝에 추가). 이동·잡기 불가(기존 `!= Active` 판정 재사용), 고양이 시야 판정에서 제외(기존 `!= Active` skip).
- `World/HideSpot` (NetworkBehaviour + IInteractable, 방 모듈 콘텐츠):
  - 들어가기 E 홀드 0.3s, 나오기 E 즉시. 정원(장화 1, 빈 상자 2). 대형을 끌고 있으면 못 들어감.
  - 호스트가 occupant 목록 관리, `OccupantCount` NV 복제. 안 = 바닥 중앙, 출구 = 앞면 0.5m 밖 (앵커로 덮어쓰기 가능). 이동은 소유 클라 TeleportClientRpc.
  - 나올 때 소음 35 (Impact, 반경 ≈4.9m). 초안 10은 소음 척도(걷기 8)상 1.4m라 의미 없어서 올림.
  - `ServerEject()` — 고양이가 건드리면 안의 쥐 전부 출구로 튀어나옴.
- `CatState.Search` (`AI/CatBrain.Search.cs` partial — CatBrain 300줄 초과 분리):
  - 진입: Chase 중 시야 상실 3s → 마지막 목격점 반경 6m 안에 HideSpot이 있으면 Search, 없으면 기존 Suspicious. 타깃이 Hidden이 되면 Return이 아니라 시야 상실로 처리.
  - 가까운 순 최대 3곳 방문 → 출구 앞에서 킁킁 2s (여럿이 숨었으면 × 인원 × 1.5) → 30% 건드림 → 안에 있으면 발각 → Pounce 창 0.5s → Chase.
  - 25s 또는 스팟 소진 → Return. 외부 자극(목격·소음·미끼)이 수색을 끊는다(CheckEscalation).
- 연출: CatVisual Search = 연노랑 + 몸 낮춰 킁킁. HUD `HiddenOverlayWidget` (어두운 화면 55% + 심장 박동 — 가장 가까운 고양이 거리 2~12m → 1~3Hz, 가까우면 "숨죽여… 고양이가 코앞"), TeamStatus "숨음" 칩(Secondary).
- 수치 (BalanceConfig "숨을 곳·수색"): hideEnterSeconds 0.3 · hideExitNoise 35 · catSearchRadius 6 · catSearchSeconds 25 · catSearchMaxSpots 3 · catSniffSeconds 2 · catDisturbChance 0.3 · catPounceWindowSeconds 0.5 · catMultiHideSniffMul 1.5.
- 데모 레이아웃: Hide_Shoe "장화"(3,0.7,-8) 1.1×1.4×2.2 · Hide_Box "빈 상자"(-3.5,0.75,7) 1.8×1.5×1.8, 트리거 콜라이더 + NetworkObject.

## 검증 (에디터 호스트 1인)
- 추격 1.0s 만에 시작 → 장화에 숨기: 상태 Hidden, occ 1, 위치 = 장화 안, 시야 판정 false, 오버레이 표시·"고양이가 코앞"(3.4m), 프롬프트 "장화에서 나오기", 팀 칩 "숨음".
- 2.6s 뒤 Search 진입(대상 장화) → 3.7s 뒤 킁킁·건드림(DisturbChance 임시 1) → 쥐가 출구(1.4,0.65,-8)로 튀어나옴 → 0.8s 뒤 Chase.
- 숨은 화면 스크린샷 = 숨지 않은 화면보다 확실히 어두움.
- 나오기 소음: 초안 10은 2.9m 고양이에게 안 들림(게이지 0) → 35로 올림.
- 미검증: 대형 끌며 숨기 불가(코드 리뷰만), 2인(클라가 숨고 나오기 — Teleport RPC 대상이 소유자).

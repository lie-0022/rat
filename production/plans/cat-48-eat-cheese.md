# 고양이 48 — 치즈 먹기 (2026-09-24, docs/04 "먹기로 즉시 50 회복 — 가치 포기", docs/05 Edible)

## 발견
docs/04 수용 기준 "치즈 먹기 회복 동작"을 보려니 먹기가 없었다(PlayerStamina 주석: "치즈 아이템 생기는 태스크 1-7에서").

## 구현
- `World/EdibleItem` (IInteractable, Edible 전리품 프리팹에 — 치즈 조각·치즈 덩어리): 손에 든 소형만(`CarrierIds`에 나 + 주머니 아님 + 대형 아님). E `eatSeconds` 1s → 호스트: `PlayerCarryController.ServerConsumeCarried` → 디스폰, 먹은 쥐에게 `AteClientRpc`(대상 지정) → `PlayerStamina.Restore(50)`(스태미나는 소유 클라 값) + `EventBus.CheeseEaten` → `ToastWidget` "냠냠 — 스태미나 +50".
- 기존 상호작용 흐름(PlayerInteractor 탐지·홀드 게이지·서버 재검증) 그대로 — 손에 든 물건이 조준 앞에 있어 탐지된다.

## 검증
- 호스트가 치즈 조각을 들고 스태미나 20으로 → 실제 E 입력 1.01s → "먹기 … 가치 14 포기", 물건 디스폰, 손 빔, 스태미나 95.3(+50 + 재생), CheeseEaten 이벤트 1회.
- 미검증: 원격 클라가 먹기(빌드에 입력 불가 — 대상 지정 ClientRpc 경로), 치즈 덩어리(대형)는 손에 못 들어 먹기 불가(의도).

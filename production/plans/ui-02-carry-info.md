# 계획 2 — CarryInfo (든 물건 정보·무거움 경고)

> **완료 (2026-09-16)** — 계획대로 + 대형 자리가 다 차면 칩을 "함께 드는 중"(Warning)으로(다 찼는데 "같이 들자"가 어색). 금 간 판정은 `CarryableItem.IsCracked`로 꺼내 정산과 공유. 검증: 에디터 호스트 1인(치즈 조각·참치캔·치즈 덩어리·금 간 계란 — `Captures/carry_*.png`), 에디터 호스트 + macOS 빌드 클라 2인 대형 공동 운반(1/2 빨강 → 2/2 주황). 집기는 개발용 `DevGrabNearest`/`DevRemoteControl grab`(실제 집기와 같은 RPC).

## 목표
docs/12 HUD 표의 `CarryInfo (하단) — 든 아이템 아이콘·가치·"무거움!" 경고`. 지금은 인벤 슬롯(1~4 칸)과 집기 안내가 대신하고 있어 **손에 든 것의 가치와 무게 상태**가 화면에 없다.

## 사양 근거
- docs/12 HUD 구성 표 CarryInfo, 데이터 소스 `CarriedItemNetId`.
- docs/05: maxForce 고정 — 무거우면 끌린다(의도). 하중 기준은 BalanceConfig `SprintBlockLoad 3`, `JumpBlockLoad 4.5`, `_heavyThreshold`(속도 감쇠), 스태미나 `StaminaHeavyDrain 15/s`(sprint 차단 하중부터).
- docs/08: 가치 = `BaseValue × DurabilityMul` → `CarryableItem.EffectiveValue` (금 간 Fragile 50%).

## 현재 코드
- `PlayerCarryController`: `IsHolding`, `CarriedItem`(CarryableItem), `IsDraggingHeavy`, `CurrentLoadPerRat`(= mass / 캐리어 수), `GetSlotItem(i)`, `SelectedSlot`.
- `CarryableItem`: `Data`(LootItemSO), `EffectiveValue`, `Durability` NV, `CarrierIds`, `IsHeavy`, `CarrySlotCount`.
- `LootItemSO.Icon` 필드는 있지만 **에셋 전부 `_icon: {fileID: 0}`** — 아이콘 스프라이트 없음.
- 기존 HUD: `InventorySlotsWidget`(하단 중앙, 슬롯 이름 텍스트), `InteractPromptWidget`(조준점 아래, 놓기/던지기 안내), `ThrowGaugeWidget`.

## 설계
### 위치·형태
- **인벤 슬롯 바로 위**, 하단 중앙 작은 카드(Dim 배경, 폭은 글자에 맞춰 `ContentSizeFitter`). 손에 든 게 있을 때만 표시. 슬롯이 숨는 조건(결과 화면)과 같이 숨김 — `HudController._slotsGroup` 아래에 두면 자동.
- 한 줄: `[아이콘 자리] 이름  가치 N` + 오른쪽 상태 칩.
  - 아이콘: `Data.Icon`이 있으면 Image, 없으면 **아이콘 자리를 아예 숨기고** 이름만 (빈 사각형 금지). 아트가 스프라이트를 넣으면 자동으로 뜬다.
  - 가치: `EffectiveValue`. Fragile인데 금 갔으면(`Durability < 0.5`) 가치 옆에 "금 감" 소문구 + DangerText 색.
  - 상태 칩 (하나만, 우선순위 순):
    1. `IsDraggingHeavy` → "무거움! 같이 들자 (n/m)" Danger 배경 — 대형·특수 티어.
    2. `CurrentLoadPerRat >= SprintBlockLoad` → "무거움 — 못 달림" Warning 배경.
    3. `CurrentLoadPerRat >= JumpBlockLoad`면 2번 문구에 "·못 뜀" 추가.
    4. 그 외 칩 없음.
  - 팀 하중 표기: 여럿이 들면 "1인당 하중"이 내려가므로 칩이 자연히 사라진다 — "같이 들자"가 실제로 도움이 됨을 화면으로 확인시킴.
- 주머니(슬롯)에 있는 물건 정보는 **표시 안 함** — 슬롯 위젯이 이름을 보여주고, 손에 든 것만 가치·무게가 의미 있음.

### 데이터 흐름
- 전부 로컬 읽기: `PlayerCarryController`(Bind로 받음) → `CarriedItem` → `Data`, `EffectiveValue`, `CarrierIds.Count`. 새 NetworkVariable 없음 (docs/03 표 변경 없음).
- 매 프레임 `SetText` 동일 문자열이면 대입 안 함(캔버스 리빌드 방지). 가치·캐리어 수는 NV라 변하면 반영됨.

### 파일
- 신규 `UI/CarryInfoWidget.cs` — `Bind(PlayerCarryController)`, `[SerializeField] UiThemeSO _theme, BalanceConfigSO _balance, GameObject _card, Image _icon, TMP_Text _name, _value, _crackedTag, Image _chip, TMP_Text _chipText`.
- `HudController`: `_carryInfo` 필드 + `Bind`에서 넘김.
- Hud 프리팹 `SlotsArea` 아래 `CarryInfo` 카드 추가 (슬롯 위 y+70 근처, 슬롯 폭 기준 중앙).
- BalanceConfig 신규 수치 없음 (`SprintBlockLoad`, `JumpBlockLoad` 재사용).

### 하지 않는 것
- 아이콘 스프라이트 제작(아트 단계). 주머니 아이템 상세. 무게를 kg 숫자로 노출(플레이어에게는 "못 달림/못 뜀"이 정보).

## 구현 순서
1. `CarryInfoWidget` 작성 + `HudController` 연결.
2. 프리팹 카드 생성(execute_code) — 아이콘 Image는 비활성 기본.
3. 검증 → docs/12 HUD 표 CarryInfo 행을 구현 상태로 갱신.

## 에디터 체크리스트
- 없음. 아이콘은 나중에 `Data/Items/*.asset`의 `_icon`에 스프라이트 연결만 하면 표시.

## 테스트
- 에디터 호스트 1인 (Stage_Warehouse01 또는 Hub PracticeLoot):
  - Small 집기 → 카드 "이름 · 가치" 칩 없음. 놓기 → 카드 사라짐.
  - 참치캔(mass 3.0) 집기 → "무거움 — 못 달림" (SprintBlockLoad 3 경계값: `>=` 확인).
  - 대형(치즈 덩어리 mass 10) 잡기 → "무거움! 같이 들자 (1/m)".
  - 계란 던져 깨뜨린 뒤 집기 → 가치 20 + "금 감".
  - 스크린샷 4장.
- 2인(에디터 + 빌드): 대형을 둘이 들면 칩의 n이 2로 바뀌고, `CurrentLoadPerRat` 5 → SprintBlockLoad 이상이면 여전히 무거움 칩(대형은 1번 규칙이라 그대로) — 대형이 아닌 참치캔을 둘이 들 수 없으므로(Small/Tricky는 1인) 2인 검증은 대형 카운트만.
- 결과 화면 중 카드 숨김(슬롯 그룹과 함께).

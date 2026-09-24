# 고양이 16 — 헤어볼 (2026-09-24, design/cat-ideas/11 후속)

## 목표
그루밍을 마친 고양이가 가끔 "웩웩" 3s → 그 자리에 **헤어볼**. 쥐가 주워 가면 가치 5 + 도감 "고양이가 준 선물. 받기 싫었다." 그리고 헤어볼도 Slippery라 추격 경로에 깔면 고양이가 미끄러진다(고양이 10) — 무한 코미디 루프. 댕청 도감 텍스트의 정점.

## 설계
- 전리품 `loot_hairball` — 헤어볼 · Small · 가치 5 · 질량 0.3 · Slippery. LootTools 표에 한 줄 추가 → SO·프리팹·ItemDatabase(도감 포함). 스폰 테이블엔 안 넣는다(고양이만 만든다). docs/08 표에 추가.
- `CatBlunderKind.Hairball`(enum 끝): Patrol에서 Groom 스팟 머무름이 끝날 때 20% → Blunder(Hairball) 3s(멈춤, 몸 들썩) → 끝나면 정면 0.6m에 헤어볼 스폰(호스트 NetworkObject.Spawn) → Return.
- Cat 프리팹 `_hairballItem`(LootItemSO) 참조 — 프리팹은 SO의 `Prefab`.
- 수치: catHairballChance 0.2 · catHairballSeconds 3.

## 검증
- Groom 스팟 머무름 끝(확률 1 임시) → "웩웩" 로그 → 3s 뒤 헤어볼 1개 스폰(위치·Slippery·가치 5).
- 헤어볼 위를 추격 속도로 밟으면 미끄러짐(고양이 10 재사용).
- 2인: 클라에 헤어볼 오브젝트 스폰 복제.

## 검증 결과 (2026-09-24)
- Create Loot Assets(21종) → loot_hairball SO·프리팹 생성, DefaultNetworkPrefabs 자동 등록, ItemDatabase 21개. Cat 프리팹 `_hairballItem` 연결.
- Groom 머무름 끝(확률 1 임시) → 0.5s 뒤 Blunder(Hairball) "웩웩" → 3.0s 뒤 헤어볼 스폰(가치 5·Slippery, 고양이 정면 0.6m) → Return.
- 2인: 호스트 스폰 헤어볼 → 빌드 클라 스폰 에러 없음.
- 부수 수정: LootTools가 매번 CreateFolder("Loot")를 불러 "Loot 1"이 생길 뻔함 → IsValidFolder 가드. 도구가 loot_phone 질량을 표값 2.5로 되돌림 → 5kg은 의도(커밋 5c1f400)라 파일 복원 + 표·docs/08을 5로 정정.
- 관찰(무관): 빌드 클라 시작 직후 "Failed to create agent because there is no valid NavMesh" 1줄 — 접속 전 로그, 이번 변경과 무관. 확인 후보.
- 미검증: 헤어볼을 추격 경로에 깔아 미끄러뜨리기(미끄러짐은 고양이 10에서 비누로 검증), 도감 화면 표시.

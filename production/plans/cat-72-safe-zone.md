# 고양이 72 — 목적지 = 안전지대 (2026-09-25)

근거: 팀 대화 "최종 목적지가 안전 지대인거고 그 전까지의 맵들은 고양이가 돌아다님".

- `World/SafeZone`(정적 목록 + `Contains`), 목적지방 프리팹에 SafeZone 박스·NavMeshModifierVolume(Not Walkable)·따뜻한 점광원 (`Create Wall Rooms`).
- `CatSenses`: 안전지대 안 쥐는 시야 후보에서 빼고, 안전지대 안 소리는 무시. `CatBrain.TickChase`: 타깃이 안전지대면 포기.

| 확인 | 결과 |
|---|---|
| NavMesh | 목적지 가운데 1m 안 없음, 가장 가까운 NavMesh 5.5m(방 밖) |
| 추격 포기 | 강제 추격 → 목적지 안으로 → "안전지대로 도망침 — 포기" → Return/Patrol |
| 머묾 | 8초 동안 고양이 최소 거리 6.4m(방 반폭 5m) — 안 들어옴, 시야 없음·게이지 0 |
- 고양이가 자고 있어 자연 발각은 못 봤음 → 추격은 리플렉션으로 시작해 포기 분기를 확인.

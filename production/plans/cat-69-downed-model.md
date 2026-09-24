# 고양이 69 — 쓰러진 몸도 쥐 모델로 (2026-09-24)

- `RatModelTools.Apply`가 DownedBody 프리팹도 처리: 캡슐 렌더러·메시 제거, RatModel 자식(로컬 기본값 — 캡슐이 Z 90°로 누워 있어 옆으로 누운 쥐), `DownedBody.EditorSetupModel(털 렌더러, 칸)`.
- `DownedBody.ApplyColor`: 주인의 `PlayerVisual.CurrentBodyColor`(팔레트·스킨 반영)를 회색 쪽 40% — 없으면 팀 기본색.
- 찾은 것: 캡슐 렌더러를 **끄기만** 했더니 게임에서 다시 켜져 쥐를 덮음 — `CarryableItem`의 주머니 표시가 자식 렌더러를 전부 켠다. → 지우는 것으로.

| 확인 | 결과 |
|---|---|
| 미리보기 | 옆으로 누운 쥐(팔 벌림) |
| 색 | 주인 #59CCD9 → 몸 #68ADB5 (= Lerp 회색 0.4) |
| 게임 | 켜진 렌더러 4(모델), 루트 렌더러 없음 — `cat-69/downed_body.png` |
| 회귀 | 몸을 쥐구멍에 → 부활(Active), 몸 사라짐 |

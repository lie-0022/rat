# 고양이 115 — 배관 입구 매복 (2026-09-25)

> 근거: 매복(커튼 뒤, design/cat-ideas/09)은 창고 맵에만 스팟이 있어 벽 속에선 한 번도 안 나왔다. 쥐 구멍(배관 입구) 옆에서 기다리는 고양이는 벽 속에 딱 맞는 그림.

- `CatSpot.ServerSetup`(런타임 스팟), `GridZoneBuilder.AddPipeAmbushSpots`(NavMesh 굽고 채우기 전), `GridZoneSO.PipeAmbushWeight` 0.5.
- 확인: 배관 있는 맵 "배관 입구 매복 자리 2", 고양이 스팟 목록에 PipeAmbush 있음(26개 중), 그 자리로 보내니 "스팟 도착 PipeAmbush (Ambush)" → "매복 (60s)"·몸 숨김. 걸어 나오는 쥐는 발소리로 먼저 의심(Ambush → Suspicious) — 기존 매복 규칙(웅크려 나오면 2m 안에서 보여 덮침).

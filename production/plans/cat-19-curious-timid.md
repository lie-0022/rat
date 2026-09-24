# 고양이 19 — 성격 2종 추가: 호기심쟁이·겁쟁이 (2026-09-24, design/cat-ideas/01 나머지)

## 목표
성격이 4종이 되면 "이번 놈은 어떤 놈?"이 판마다 확실히 달라진다. 이제 호기심(03)·실패(11) 시스템이 있어서 두 성격에 **특이 행동**을 붙일 수 있다.
- **호기심쟁이**: 잠 적게(수면 ×0.4), 청각 1.2, 굴러가는 물건에 **더 쉽게** 끌림(반응 속도 기준 ×0.5 — 천천히 구르는 것도), **의심 중에도** 굴러가는 물건에 속는다(다른 성격은 순찰·복귀 중에만). 쥐: 던지기 = 원격 조종.
- **겁쟁이**: 청각 1.4, 추격 0.9, 큰 소리(원 loudness ≥60 — 깨짐·함정 등)를 들으면 **Flee 3s**: 소리 반대쪽으로 6m 도망(속도 5) → 그 뒤 소리 난 곳 조사(Suspicious). 자다가도 도망. 쥐: 접시 깨기가 "공격" — 가치를 버리는 트레이드.

## 설계
- `CatPersonalitySO` 필드 추가: `curiositySpeedMultiplier`(1), `curiousWhileSuspicious`(false), `fleeLoudness`(0 = 없음), `fleeSeconds`(3). EditorSetup 오버로드.
- `CatSenses.MotionTick`: 반응 속도 × `CuriositySpeedMul`(Brain이 성격 적용 때 설정).
- `CatBrain.TickSuspicious`: 성격이 허용하면 `CheckCuriosity()`.
- `CatBlunderKind.Flee`(enum 끝) — `AI/CatBrain.Personality.cs`: `Heard` 이벤트에서 원 loudness ≥ fleeLoudness이고 Chase·Capture·Toy·Away·Blunder·Fight 아니면 → Flee. 끝나면 Suspicious(조사 지점 = 소리).
- 에셋: Data/Cats/Personality_Curious(호기심쟁이, 밝은 주황), Personality_Timid(겁쟁이, 크림색). Cat 프리팹 `_personalities` 4종(스폰 시 랜덤).
- CatVisual: Flee = Startle처럼 몸 튐 + 꼬리 곤두섬(빠른 떨림).

## 검증
- 호기심쟁이: 1.0 m/s로 구르는 물건(기본 기준 1.5 미만)에 반응. 의심 중 굴러가는 물건 → Curious.
- 겁쟁이: 5m 옆 깨짐(60) → Flee → 소리 반대쪽 이동 → 3s 뒤 Suspicious. 잠자다 깨짐 → Flee. 다른 성격(사냥꾼)은 Flee 없음.
- 2인: Flee 연출 복제(BlunderKind).

## 검증 결과 (2026-09-24)
- 반응 속도: 사냥꾼 + 1.0 m/s로 구르는 치즈 → 반응 없음(Patrol). 호기심쟁이 + 같은 1.0 m/s → 0.2s 만에 Curious.
- 의심 중 호기심: 호기심쟁이 소음 90 → Suspicious → 시야 안에서 치즈 굴림 → **Suspicious → Curious**(로그). 첫 시도 실패는 테스트 배치 문제(소리 쪽으로 돌아서 치즈가 시야 밖).
- 겁쟁이: 5m 옆 깨짐 60 → Flee 3.0s, 소리에서 5m → 11m로 도망 → Suspicious 유지(0.6s 뒤 게이지 28). 자는 중 깨짐 65 → Flee.
- 버그 수정: 도망 3s 동안 게이지가 식어 Suspicious가 바로 Return으로 끝남 → Flee 끝에 `CatSenses.RaiseGaugeTo(의심 임계)`.
- 2인: 빌드 클라 로그 "고양이 실패 연출: Flee". (셋업 때 고양이가 스폰 근처 쥐와 Toy 중이라 Flee가 막힘 — 규칙대로. 풀린 뒤 발동.)
- 미검증: 호기심쟁이 수면 ×0.4 실시간, 성격 4종 랜덤 분포.

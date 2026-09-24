# 고양이 18 — 매복 + 상자 (2026-09-24, design/cat-ideas/09 중 맵 없이 되는 둘)

## 목표
고양이가 **어디서 나올지 모르게**. 문 밀기·선반은 맵 조각이 필요해서 뒤로, 지금은:
1. **매복**: 순찰 중 Ambush 스팟(커튼·소파 밑 — 그레이박스는 상자 B 옆)에 들어가 60s 숨는다. 몸은 안 보이고 **꼬리만 삐져나온다**(텔레그래프 타협 불가) + 10s마다 "(츄릅… 어디선가)" 자막. 2m 안 쥐 목격 → **덮치기**(추격 없이 바로 포획, 스윙 0.2s). 더 멀리서 보이면 평소 전이.
2. **상자**: 쥐의 숨을 곳 "빈 상자"(고양이 5) 입구에 Box 스팟. 고양이가 와서 **입구에 앉는다**(15~40s, 시야 정면 3m·반각 45°만 — 좁은 시야). 앉아 있는 동안 쥐는 그 상자에 들어가지도 나오지도 못한다(갇힘!). 도착했을 때 안에 쥐가 숨어 있으면 → 끄집어내 덮치기. "쥐도 같은 상자를 쓴다 — 누가 먼저?"

## 설계
- `CatState.Ambush`, `CatState.BoxSit`(enum 끝) — `AI/CatBrain.Lurk.cs`.
- `AmbushHidden` NV → CatVisual이 꼬리 빼고 렌더러 끔. 매복 예고 = `CatCueKind.Ambush`(18m 자막).
- 덮치기: `SetState(Capture)` 뒤 스윙 시간만 0.2s로(기존 Capture 판정 재사용).
- `CatSenses.ViewDistanceOverride/ViewHalfAngleOverride` — 상자에서 3m·45°.
- `HideSpot.CatBlocking` NV — 켜지면 CanInteract false(들어가기·나오기 둘 다).
- 스팟: 데모 레이아웃 Spot_Ambush(-7.6,0,9) 상자 B 옆, Spot_Box = 빈 상자 입구(-2.1,0,7) 바깥 향함. 가중치 0.5.
- 수치: catAmbushSeconds 60 · catAmbushPounceRange 2 · catAmbushCueInterval 10 · catPounceSwingSeconds 0.2 · catBoxSeconds (15,40) · catBoxViewDistance 3 · catBoxViewHalfAngle 45 · catBoxBlockRadius 2.

## 검증
- 매복: 도착 → AmbushHidden(꼬리만 렌더) → 쥐 1.5m → 0.2s 스윙 → Downed/Toy. 쥐 5m 정면 → 평소 목격 전이. 60s 뒤 Return.
- 상자: 도착 → BoxSit, 상자 CatBlocking → 쥐 CanInteract false. 시야 3m 밖 쥐 무시·3m 안 목격. 안에 쥐 있으면 발각·덮치기.
- 2인: 클라에서 꼬리만 보임(렌더러), CatBlocking 복제(클라 프롬프트 사라짐).

## 검증 결과 (2026-09-24)
- 상자: Box 스팟 도착 → BoxSit, 빈 상자 CatBlocking true·CanInteract false, 바깥(+x) 향함. 정면 4m 쥐 못 봄, 2.6m 봄(시야 3m·45°). 끝나면 CatBlocking false.
- 상자 선점 싸움: 쥐가 빈 상자에 먼저 숨음 → 고양이 도착 → "상자에 쥐가! 끄집어냄" → 0.21s 만에 Downed.
- 매복: 도착 → AmbushHidden, 렌더러 6개 중 **꼬리 1개만** 켜짐 → 정면 1.5m 쥐 → 0.30s 만에 덮치기 Downed. (셋업 중 고양이가 순찰로 스스로 매복하고 있던 것도 확인)
- 2인: 빌드 클라 로그 "고양이 모습: 꼬리만"(AmbushHidden 복제).
- 테스트 교훈: 쥐구멍 근처 스폰 쥐가 다운 → 쥐구멍 부활 → 재포획 루프에 빠지면 ServerWake가 무시돼(Chase/Capture) 셋업이 안 먹는다 → 쥐를 먼저 멀리 보내고 고양이가 진정된 뒤 셋업.
- 미검증: "츄릅" 자막 10s 주기, 60s 자연 종료, CatBlocking 클라 프롬프트.

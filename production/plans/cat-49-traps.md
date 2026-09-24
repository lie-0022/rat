# 고양이 49 — 함정 3종: 쥐덫·끈끈이·전기선 (2026-09-24, docs/10 함정 테이블·docs/04 상태이상·docs/06 쥐덫 80)

## 발견
docs/04 수용 기준 "Stunned/Trapped/Downed 전부 호스트 판정으로 재현, 구출 플로우"를 보니 **Trapped로 가는 길이 아예 없었다**(끈끈이·함정 미구현, docs/13 태스크 2-2). Roomba는 docs/13 컷 후보 + 로봇청소기(고양이 29)가 역할을 대신한다 → 3종만.

## 설계
- `World/TrapBase` (NetworkBehaviour, 추상): 호스트가 0.1s마다 **박스 안 쥐**(Active, 바닥 가까이)와 **박스 안 풀린 물건**을 폴링 → `OnRat`·`OnItem`. 트리거 대신 폴링 — 원격 쥐는 호스트에서 kinematic 사본이라 트리거가 흔들린다(물그릇과 같은 방식).
- `World/MouseTrap`: `Armed` NV. 쥐가 밟으면 Downed + 소음 80(NoiseType.Trap, 밟은 쥐 귀속) + 격발(1회성). 던지거나 떨어뜨린 물건이 박스에 들어오면(1s 안에 놓인 것) 헛격발 — 소음 80, 쥐는 무사. 연출: 막대가 탁 내려감.
- `World/GluePad`: 쥐가 밟으면 Trapped(동료 E 1.5s 구출 — PlayerCondition 기존). 구출된 쥐는 `glueGraceSeconds` 4s 동안 같은 끈끈이에 다시 안 붙음(발 떼고 나갈 시간). 연출: 붙은 동안 끈끈이 색 진하게는 생략 — 팀 칩·토스트가 알림(기존 "끈끈이에 붙었어요!").
- `World/WireShock`: `On` NV — `wireOnSeconds` 1.5 켜짐 / `wireOffSeconds` 1.5 꺼짐 반복. 켜진 동안 닿으면 Stunned `wireStunSeconds` 2 + 소음 30(Trap). 꺼진 틈에 지나가기. 연출: 켜지면 선이 밝게 번쩍.
- `PlayerCondition.ServerStun(seconds)` — 함정마다 기절 시간이 다를 수 있게.
- 본인 토스트: Trapped "끈끈이! 동료가 E로 구해 줘야 해요", Stunned "찌릿! 잠깐 못 움직여요".
- 데모 레이아웃: 쥐덫 = 창고방 쥐 구멍 바로 안쪽(17, -14.6)(좁은 통로), 끈끈이 = 치즈 근처, 전기선 = 창고방 문 앞 바깥(12.2, -17) 문틈을 가로질러.
- 수치: trapMouseLoudness 80 · glueGraceSeconds 4 · wireOnSeconds 1.5 · wireOffSeconds 1.5 · wireStunSeconds 2 · wireZapLoudness 30.

## 검증
- 쥐덫: 쥐 밟음 → Downed·소음 80·격발 / 다시 밟아도 무사. 새 쥐덫에 물건 떨어뜨림 → 헛격발·소음 80.
- 끈끈이: 밟음 → Trapped → 동료(빌드 클라)가 없으니 호스트가 클라 쥐를 구출(ServerInteract) → Active → 4s 동안 안 붙음 → 그 뒤 다시 붙음.
- 전기선: 켜진 때 → Stunned 2s → Active / 꺼진 때 → 무사. 주기 On 1.5 / Off 1.5.
- 2인: 클라 연출 로그(격발·전기 켜짐)·클라 쥐 Trapped/Stunned 판정·예외 0.

## 검증 결과 (2026-09-24)
- 1인: 쥐덫 밟음 → Downed·Trap 소음 80 1회·격발 / 격발된 덫 다시 밟음 → 무사 / 새 덫에 방금 놓은 치즈 떨어뜨림 → 헛격발(소음 1회)·쥐 무사.
- 끈끈이: 밟음 → Trapped → 구출 → 0.5s 뒤 Active(유예) → 4.7s 뒤(유예 4s 지남, 아직 위) 다시 Trapped.
- 전기선: 켜진 때 → Stunned → 2.1s 뒤 Active / 꺼진 때 → 무사. 켜짐·꺼짐 간격 1.5s 반복.
- 2인(에디터 호스트 + 빌드 클라): 클라 쥐 끈끈이 → Trapped → **호스트가 실제 E 1.53s로 구출** → Active. 클라 쥐 쥐덫 → Downed(클라 "쥐덫 연출: 탁!", "다운 몸 연출"). 클라 쥐 켜진 전기선 → Stunned. 클라 예외 0.
- 미검증: 본인 토스트("끈끈이!"·"찌릿!") 화면 확인(토스트는 로그를 안 남김 — 기존 Pinned 토스트와 같은 경로), 쥐덫에 실제로 던지기.

# 고양이 46 — 쓰러진 동료 운반 → 쥐구멍 부활 (2026-09-24, docs/04·05·09)

## 발견
docs/05 수용 기준 "다운 동료 운반→쥐구멍 부활 플로우"를 검증하려다 보니 **운반 자체가 구현 안 돼 있었다**. 다운된 몸은 잡을 수 없고(잡기는 Carryable 레이어의 CarryableItem만), 부활은 몸이 어쩌다 쥐구멍 트리거에 들어가야만 됐다. 협동(필러 3)의 핵심 장면.

## 설계
- 플레이어 몸은 소유 클라 권한(docs/03) — 호스트 조인트로 못 끈다. → **호스트 소유 대리 몸**을 띄운다.
- `World/DownedBody`(프리팹: 캡슐·Carryable 레이어·Rigidbody mass 3·NetworkTransform/Rigidbody·CarryableItem(데이터 `downed_body`: Large·mass 3·가치 0)): `Owner` NV, 주인 털빛을 칙칙하게.
- `Player/PlayerDownedBody`(Player 프리팹): Downed → 호스트가 몸 생성. 모든 클라에서 쥐 본체 콜라이더·렌더러 끔, 소유 클라 rb kinematic. 호스트가 0.1s마다 소유 클라 시점을 몸 위치로(TeleportClientRpc). Downed에서 벗어나면 몸 자리에 쥐를 세우고 몸 제거.
- `DepositZone`: 대리 몸이면(들고 있어도) 정산 대신 `ServerRevive(주인)`.
- 에디터 도구 `Tools/RatGame/Create Downed Body`(데이터·프리팹 생성, Player 프리팹 연결). 프리팹은 NGO 기본 네트워크 프리팹 목록에 자동 등록 확인.

## 검증 (2인 — 에디터 호스트 + 빌드 클라)
- 클라 다운 → 대리 몸 생성(0,0.3,8), 호스트 시점에서 클라 콜라이더 꺼짐.
- 호스트가 잡음 → 대형(자동 자리)으로 잡힘. 1.6s 동안 2.2m 끎, 클라 시점이 몸 0.4m 안에서 따라감.
- 몸을 쥐구멍에 → 0.01s 뒤 클라 Active, 몸 제거, 콜라이더 다시 켜짐, 클라가 쥐구멍(5,6)에 섬.
- 빌드 클라 "다운 몸 연출: client 1", 예외 0.
- 미검증: 관전 카메라(docs/04 — 지금은 시점이 몸을 따라가기만), 2인이 같이 끌 때 속도, 실제 입력으로 끌기 손맛.

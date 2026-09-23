# 고양이 4 — 예측 추격·오버슛·코너 감속 (2026-09-24, 세트 1 마무리)

## 목표
추격을 "직선 직행"에서 동물답게: 타깃이 가는 방향 앞을 노리고(예측), 시야를 잃으면 진행 방향으로 3m 더 가 보고(오버슛), 방향을 크게 틀 때는 느려진다(코너 감속 — 지그재그가 통하게). 직선 최고 속도 5.5 < 달리기 6은 유지. (design/cat-design/01-3 B, 01-6)

## 설계
- 타깃 속도 = 위치 차분(원격 플레이어는 kinematic이라 Rigidbody 속도 0).
- 예측: `MoveTo(Sample(pos + clamp(vel, 5.5) × 1s))`. NavMesh 밖이면 타깃 위치.
- 오버슛: 시야 상실 순간 `lastKnown + dir × 3m`(샘플), 도착하면 lastKnown. 3s 뒤 Suspicious(기존).
- 코너 감속: `CatMovement.SteeringAlignment`(몸 방향·다음 경로점 내적) → 추격 속도 × Lerp(0.55, 1).
- 수치: BalanceConfig `고양이 추격` 3개. 프리팹 Agent angularSpeed 360→240, acceleration 12→8.

## 검증
- 쥐가 직선으로 달릴 때 고양이 목적지가 쥐보다 앞(≈3.8m).
- 쥐가 상자 뒤로 돌아 사라지면 고양이가 마지막 목격점을 지나 진행 방향으로 더 감.
- 고양이가 90° 꺾을 때 agent.speed가 5.5 → ~3 미만으로 떨어짐.

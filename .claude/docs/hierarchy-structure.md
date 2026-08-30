# Hierarchy Structure — [TBD] (GamePlay 씬)

## 전체 구조

```
GamePlay (Scene)
│
├── ── Managers ──          빈 오브젝트 (그루핑)
│   ├── GameManager         GameManager.cs
│   ├── ObjectPool          ObjectPool.cs
│   ├── XPSystem            XPSystem.cs
│   ├── GoldSystem          GoldSystem.cs
│   └── WeaponSystem        WeaponSystem.cs
│
├── ── World ──             빈 오브젝트 (그루핑)
│   ├── Environment         빈 오브젝트 (지형 묶음)
│   │   └── Ground
│   └── SpawnPoints         빈 오브젝트 (스폰 위치 묶음)
│       ├── SpawnPoint_01   Tag: SpawnPoint
│       ├── SpawnPoint_02   Tag: SpawnPoint
│       └── ...
│
├── ── Gameplay ──          빈 오브젝트 (그루핑)
│   ├── Player              PlayerController.cs / Tag: Player / Layer: 8(Player)
│   └── WaveSpawner         WaveSpawner.cs
│
├── ── Camera ──            빈 오브젝트 (그루핑)
│   └── Main Camera         CameraController.cs / Tag: MainCamera
│
└── ── UI ──                빈 오브젝트 (그루핑)
    ├── Canvas              Canvas 컴포넌트
    │   ├── HUD             HUDController.cs
    │   └── SkillSelectionPanel  SkillSelectionUI.cs
    └── EventSystem         자동 생성 (Canvas 생성 시) — UI 입력 처리 필수
```

---

## 새 시스템 추가 시 어디에 넣는지

| 추가할 것 | 위치 | 비고 |
|----------|------|------|
| 싱글턴 시스템 스크립트 | `── Managers ──` 하위 빈 오브젝트 | ObjectPool, XPSystem 등 |
| 적 프리팹 | `── Gameplay ──` 하위 (런타임 생성) | ObjectPool이 동적 생성 |
| 스폰 위치 | `── World ── / SpawnPoints` 하위 | Tag: SpawnPoint 필수 |
| 환경 오브젝트 | `── World ── / Environment` 하위 | Layer: 13(Environment) |
| UI 화면 | `── UI ── / Canvas` 하위 | Canvas 컴포넌트 아래 |
| 투사체·VFX | 런타임 생성 (ObjectPool) | 씬에 직접 배치 안 함 |

---

## 레이어 할당 규칙

| 오브젝트 | Layer |
|---------|-------|
| Player | 8 — Player |
| 적 본체 | 9 — Enemy |
| 플레이어 투사체 | 10 — PlayerProjectile |
| 적 투사체 | 11 — EnemyProjectile |
| XP·골드 드롭 | 12 — Pickup |
| 지형·벽·바닥 | 13 — Environment |
| 근접 공격 판정 콜라이더 | 14 — HitBox |

---

## 태그 할당 규칙

| 오브젝트 | Tag |
|---------|-----|
| Player | Player |
| 적 | Enemy |
| XP·골드 드롭 | Pickup |
| 투사체 | Projectile |
| 스폰 위치 마커 | SpawnPoint |

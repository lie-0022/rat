# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3.10f1)
- **Language**: C# (.NET Standard 2.1)
- **Rendering**: Universal Render Pipeline (URP 17.3.0) — 3D Universal Renderer, Render Graph API (Compatibility Mode 사용 금지)
- **Physics**: Unity 3D Physics (PhysX)
- **Project Path**: `./`
- **템플릿 출처**: `com.unity.template.3d-cross-platform` 17.0.14

## Input & Platform

- **Target Platforms**: PC (macOS 개발 / Windows 배포 검토) — MVP 기준
- **Input Methods**: Mixed (Keyboard/Mouse + Gamepad)
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial (점진 도입)
- **Touch Support**: None
- **Platform Notes**: 3d-cross-platform 템플릿이라 모바일 확장 여지 있음. MVP는 PC 우선

## Naming Conventions

- **Root Namespace**: `Rat` — ⚠️ 임시 플레이스홀더. 코드 작성 전에 확정할 것
- **Classes**: PascalCase (e.g., `PlayerController`)
- **Public Fields/Properties**: PascalCase (e.g., `MoveSpeed`)
- **Private Fields**: _camelCase (e.g., `_moveSpeed`)
- **Methods**: PascalCase (e.g., `TakeDamage()`)
- **Signals/Events**: On + PascalCase (e.g., `OnPlayerDied`)
- **Files**: PascalCase matching class (e.g., `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase (e.g., `PlayerCharacter.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE (e.g., `MaxHealth` / `MAX_HEALTH`)

## Performance Budgets

- **Target Framerate**: 60fps (PC 기준)
- **Frame Budget**: 16.6ms @ 60fps
- **Draw Calls**: MVP 단계 미세 튜닝 보류 (실측 후 결정)
- **Memory Ceiling**: MVP 단계 미세 튜닝 보류 (실측 후 결정)

## Testing

- **Framework**: Unity Test Framework (NUnit 기반) — `com.unity.test-framework` 1.6.0
- **Minimum Coverage**: MVP 단계 — 핵심 로직(데미지 계산, 던전 생성 등) 필수 / 전체 비율은 추후 결정
- **Required Tests**: Balance formulas, gameplay systems

## Forbidden Patterns

- `Object.FindObjectsOfType<T>()` — 대신 `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` 사용
- `Object.FindObjectOfType<T>()` — 대신 `Object.FindFirstObjectByType<T>()` 또는 `FindAnyObjectByType<T>()` 사용
- URP Compatibility Mode (`ScriptableRenderPass` 구식 방식) — Render Graph API 사용
- `Entities.ForEach` (DOTS) — `IJobEntity` 또는 `SystemAPI.Query` 사용 (6.5에서 제거 예정)
- 런타임에서 `FindObjectsOfType` 반복 호출 — 캐싱 필수
- `public` 필드 직렬화 — `[SerializeField] private` 사용

## Allowed Libraries / Addons

- Unity Input System (`com.unity.inputsystem` 1.18.0) — 기본 Input 대신 사용
- Universal RP (`com.unity.render-pipelines.universal` 17.3.0)
- AI Navigation (`com.unity.ai.navigation` 2.0.10) — NavMesh
- Unity UI (uGUI 2.0.0)
- Timeline 1.8.10
- MCP For Unity (`com.coplaydev.unity-mcp`) — 에디터 자동화 브리지

## Engine Specialists

- **Primary**: `unity-csharp` (C# 스크립팅, MonoBehaviour, New Input System)
- **Architect**: `unity-architect` (폴더 구조, Asmdef, ScriptableObject 아키텍처)
- **Debugger**: `unity-debugger` (컴파일 에러, NullReferenceException, 성능)
- **보조**: `unity-specialist`, `unity-ui-specialist`, `unity-shader-specialist`, `unity-addressables-specialist`, `unity-dots-specialist`
- **Disabled**: `godot-*` (4개) · `unreal-specialist` · `ue-*` (4개) — `.claude/agents/_disabled/` 에 격리

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| `.cs` (게임 로직) | `unity-csharp` |
| `.cs` (Editor 폴더) | `unity-csharp` (Editor 컨텍스트 명시) |
| `.shader`, `.shadergraph` | `unity-shader-specialist` |
| `.uxml`, `.uss` | `unity-ui-specialist` |
| `.unity` (Scene), `.prefab` | `unity-architect` |
| `.asset` (ScriptableObject) | `unity-architect` |
| `.asmdef` | `unity-architect` |
| 컴파일/런타임 에러 | `unity-debugger` |
| 일반 아키텍처 검토 | `unity-architect` (Primary) |

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- ADR-001 (가칭): Engine = Unity 6.3 LTS + URP 3D — 프로젝트 생성 시 결정 (2026-08-22). 정식 ADR은 `/architecture-decision`으로 작성 권장.

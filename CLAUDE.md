# CLAUDE.md — Rat (가제)

<!-- 장르·핵심 재미·세계관은 /start 또는 /brainstorm 진행 후 채웁니다. -->

## 📄 기획 문서
- **게임 컨셉**: `design/gdd/game-concept.md` (미작성 — /brainstorm 후 작성)
- **미확정 질문**: `design/open-questions.md`
- **한 줄 요약**: (미정 — 브레인스토밍 후 기입)
- **프로젝트 현황**: `production/session-logs/session-log.md`

## 🧭 기술 스택
- **Unity 6.3 LTS** (6000.3.10f1) / **URP 17.3.0 — 3D Universal Renderer**
- Input System 1.18.0 · AI Navigation(NavMesh) 2.0.10 · Timeline · uGUI
- 상세: `.claude/docs/technical-preferences.md`
- ⚠️ 루트 네임스페이스 `Rat` 은 **임시값**(프로젝트 가제) — 게임명 확정 시 함께 재검토

## 🧭 아키텍처 규칙 (dokkaebi·hwatu·던던전전 과 동일 철학)
- `Assets/Rat/Core` — 순수 C#, 엔진 비의존. 로직은 여기 + EditMode 테스트로 검증.
- `Assets/Rat/Game` — MonoBehaviour(입력·물리·렌더).
- `Assets/Rat/Tests` — Core 대상 EditMode 테스트.
- 네임스페이스 `Rat.Core` / `Rat.Game` / `Rat.Tests`, asmdef 3분할 (Core는 noEngineReferences).

## 🔌 Unity MCP
- 브리지: **CoplayDev MCP For Unity** (`com.coplaydev.unity-mcp`), HTTP `127.0.0.1:8080/mcp`
- 등록: `.mcp.json` (프로젝트 스코프)
- 켜는 법: Unity 에디터 `Window > MCP For Unity > Connect > Start Server`
  → `Advanced > Auto-Start Server on Editor Load` 체크하면 이후 자동 기동
- 패키지: `Packages/manifest.json`에 등록됨 — Unity 처음 열 때 자동 임포트
- AI가 되는 일 / 안 되는 일 경계: `.claude/docs/mcp-capabilities.md`

## ⚙️ 에이전트 세팅
- 에이전트 42개 활성 (Tier1 디렉터 / Tier2 리드 / Tier3 스페셜리스트) + `_disabled/` (Godot·Unreal)
- 스킬 37개 — `/start`, `/brainstorm`, `/setup-engine`, `/prototype`, `/sprint-plan`, `/team-combat` 등
- 상세: `.claude/docs/quick-start.md`, `.claude/docs/agent-roster.md`

## ✅ 작업 규칙
- 로직은 Core에 순수 함수 + Tests 케이스. 씬/프리팹보다 코드-퍼스트(프로토타입 단계).
- 기획을 크게 바꿨으면 GDD가 오래됐다고 사용자에게 알릴 것.

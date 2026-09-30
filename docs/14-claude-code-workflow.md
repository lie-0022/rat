# 14. 클로드코드 구동 워크플로우

## 전제 — 클로드코드가 할 수 있는 것 / 없는 것

| 할 수 있음 | 할 수 없음 (사람이 함) |
|---|---|
| C# 스크립트 작성·수정·리팩토링 | 에디터 실행·플레이 테스트 |
| .asmdef, manifest.json 편집 | 프리팹 생성·인스펙터 와이어링 (기본) |
| SO 정의 코드 + 에디터 생성 스크립트 | 씬 편집, 라이팅, 애니메이터 그래프 |
| 컴파일 에러 해석·수정 (로그 붙여주면) | 스팀 업로드, 계정 작업 |

- **프리팹/씬 갭 메우기**: 클로드코드에게 항상 "이 작업 후 에디터에서 할 일 체크리스트를 출력해라"를 요구한다.
  더 좋은 방법 — 반복되는 세팅은 **에디터 스크립트**(RatGame.Editor에 MenuItem)로 자동화시킨다.
  예: "Tools/RatGame/Create Loot Assets" → 08 문서 표를 읽어 SO 20개+프리미티브 프리팹 자동 생성.
- Unity MCP(예: unity-mcp 계열)를 붙이면 클로드코드가 씬·프리팹 조작과 플레이모드 로그 확인까지 가능 — W1에 시도해보고, 불안정하면 체크리스트 방식으로 진행 (도구에 일정을 걸지 않는다).

## 레포 초기 구성

```
RatGame/                      ← git 루트 = Unity 프로젝트 루트
  CLAUDE.md                   ← 팩 동봉본을 루트에 복사
  docs/                       ← 이 문서 팩 전체 복사 (클로드코드가 참조하는 사양서)
  Assets/ ...                 ← 01 문서 구조
  .gitignore                  ← Unity 표준 (Library/, Temp/, Logs/, UserSettings/)
```

- Git LFS: 아트 들어오기 전(W1)에 설정 (*.fbx, *.png, *.wav 등).
- 커밋 규칙: 태스크 단위 커밋, 메시지 `[0-5] 잡기 RPC + 조인트 생성`. 주 마감 태그 w1~w12.

## 세션 프롬프트 템플릿 (매 태스크 공통)

```
docs/13-milestones.md의 태스크 {번호}를 구현해줘.
사양은 docs/{해당 문서}를 따라. 기존 코드와 컨벤션은 CLAUDE.md 참조.
작업 후:
1) 변경 파일 목록과 핵심 결정 요약
2) 에디터에서 내가 할 일 체크리스트 (프리팹·인스펙터·씬)
3) 테스트 방법 (MPPM 몇 인, 확인할 것)
사양과 다르게 구현해야 했다면 이유를 명시하고 docs 수정안을 제안해줘.
```

## 마일스톤별 킥오프 프롬프트 (복붙용)

**W1 첫 세션 (프로젝트 생성 직후):**
```
새 Unity 6000 LTS URP 프로젝트야. docs/01-project-setup.md와 docs/02-architecture.md대로
폴더 구조·asmdef·Input Actions·Boot 씬 부트스트랩·GameStateMachine·EventBus를 만들어줘.
패키지는 manifest.json을 직접 수정하고, 씬 생성처럼 에디터가 필요한 건 체크리스트로 줘.
```

**W1–2 넷코드 (가장 중요한 세션):**
```
docs/03-netcode.md를 구현하자. 순서: NetworkLauncher(UnityTransport 먼저) → 캡슐 플레이어
스폰·이동 동기화(docs/04 이동 사양) → CarryableItem 잡기 RPC·조인트(docs/05 조인트 사양).
Sandbox_Net 씬용 회색 박스 5종 생성 에디터 스크립트도 만들어줘.
Multiplayer Play Mode 4인으로 테스트할 거야. 물리 동기화 방식은 문서의 결정(호스트 시뮬,
클라 kinematic 보간)을 벗어나지 마.
```

**이후 각 주: 13 문서 태스크 번호로 공통 템플릿 사용.**

## 컴파일·테스트 루프

1. 클로드코드 작업 → 2. 에디터 포커스(컴파일) → 3. 에러면 Console 전체 복사 → 클로드코드에 붙여넣기
4. 통과 시 MPPM 실행 → 이상 동작은 **재현 절차 + Player.log**를 붙여서 전달
5. 통과 기준(각 문서 수용 기준) 체크 → 커밋

## 시험 도구 (2026-09-30 기준)

| 무엇 | 어디 | 시간 | 언제 |
|---|---|---|---|
| EditMode 로직 시험 23개 (번역·공식·관심 점수·합성음·맵 설계도 시드 200·데이터 무결성·빌드 씬·세이브 왕복·네트워크 프리팹 등록·사라진 스크립트·프리팹·코드·데이터 에셋 번역 글자·스팀 업로드 경로) | Window → General → Test Runner → EditMode (`Assets/_Project/Tests/EditMode`) | 2초 | 공식·데이터·생성기를 고친 뒤 |
| Run All — Quick (혼자 9) | Tools → RatGame → Test | 2분 | 게임 코드를 고친 뒤 |
| Run All — Full (혼자 9 + 2·4인·레이저·재접속·이탈 8 = 17) | 〃 (빌드 클라 `Builds/macOS/Rat.app` 필요 — 런타임 코드를 고쳤으면 먼저 빌드) | 8분 | 넷코드·운반을 고친 뒤 |
| Run Everything (Full 17 + 통째 혼자·영어 2인·4인 = 20) | 〃 | 18분 | 큰 묶음 뒤, 커밋 전 |
| 성능 | 개발 빌드 `-perflog -autohost -autostage 5` | 1분 | docs/13 성능 표 |

- 시험은 세이브·직전본·설정을 떠 두고 플레이를 멈춘 뒤에도 한 번 더 되돌린다. 7777이 막혀 있으면 7778로 돈다(`Editor/DevPort`). 플레이 중 컴파일은 `Editor/PlayModeReloadGuard`가 네트워크를 닫는다.
- 통째 시험을 혼자 돌리면 끝나도 플레이에 남는다(결과 화면 확인용) — 컴파일 전에 멈출 것.

## 빌드 (고양이 320, `Editor/BuildTools`)

| 메뉴 (Tools → RatGame → Build) | 나오는 곳 | 비고 |
|---|---|---|
| Windows x64 Demo (release) | `Builds/Release/Windows/Rat.exe` | 데모 목표. Windows Build Support 모듈 필요 — 없으면 이유를 말하고 안 만든다 |
| Windows x64 (dev) | `Builds/Windows/Rat.exe` | 원격 2대 시험용 |
| macOS (release) / (dev) | `Builds/Release/macOS/Rat.app` / `Builds/macOS/Rat.app` | dev 자리가 시험 도구 클라 — 릴리스로 덮지 않게 자리를 나눔 |

- 빌드 전 검사: 플레이·컴파일 중 아님, 모듈 있음, 빌드 씬 0번 = Boot. 릴리스는 Burst 디버그 기호(`*_BurstDebugInformation_DoNotShip`)를 `Builds/Symbols/{플랫폼}-{버전}`으로 옮겨 싣지 않는다(크래시 분석용으로 보관).
- 배치 모드: `Unity -batchmode -quit -projectPath . -executeMethod RatGame.EditorTools.BuildTools.BuildWindowsDemoCli` (실패하면 종료 코드 1).
- 스팀 업로드: `steam/`(앱·디포 VDF 틀 + README) — 숫자 채우기·업로드는 사람. 기본 `Preview 1`(올리지 않고 목록만), `SetLive` 빈칸(바로 공개 금지). EditMode가 업로드 폴더 = 윈도 릴리스 빌드 폴더, 디포 번호 일치를 검사.
- 확인 (09-30): macOS 릴리스 124MB·82초·경고 0, 켜서 메뉴까지 오류 0. Windows는 모듈이 없어 "모듈 없음"으로 멈춤 확인.

## 클로드코드에게 시킬 추가 자동화 (여유 시)

- `Tools/RatGame/Validate Balance`: BalanceConfigSO 값과 docs 표 diff 출력
- `Tools/RatGame/Room Module Checker`: RoomModule 프리팹 규약(소켓 방향·바운즈·스폰 개수) 검사
- 빌드 스크립트: 커맨드라인 `-batchmode` 빌드 (스팀 업로드 전 단계)
- (P2) 플레이 통계 로컬 CSV 덤프 — 밸런싱 근거용

## 사양 변경 규칙

플레이테스트에서 사양이 틀렸다고 판명되면: **docs를 먼저 고치고** 그 커밋에 코드 변경을 포함.
문서와 코드가 어긋난 채 진행하는 것이 2인 팀 최대의 부채다.

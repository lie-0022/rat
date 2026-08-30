# Unity MCP 능력 가이드

> AI가 Unity Editor에 대해 **직접 수행 가능한 작업**과 **여전히 본인이 해야 하는 작업** 의 명확한 경계.
> 이전 세션에서 검증된 실제 능력/한계만 기록한다.

---

## 1. AI 가 MCP로 직접 처리 (본인 손 불필요)

### 씬 / GameObject
- 씬 생성·로드·저장·검증·자동복구 (`manage_scene`)
- GameObject 생성·이름변경·복제·이동·삭제 (`manage_gameobject`)
  - **단**: 일부 삭제는 권한 거부 가능 (active scene에서 ID로 삭제 시 — 비활성화로 우회)
- 부모-자식 관계 설정
- Hierarchy 조회 (`get_hierarchy`)
- 씬 view 카메라 framing (`scene_view_frame`)

### 컴포넌트
- Add / Remove / 속성 설정 (`manage_components`)
- **중요 주의사항**:
  - GameObject 생성 시 `component_properties`로 한 번에 넘긴 *중첩 속성* (color, anchorMin/Max, padding 등)이 **자주 적용 안 됨**
  - 안전한 방법: 생성 후 `set_property` 로 다시 명시 적용
  - Vector2/Color 같은 복합 타입은 `{x,y}` / `{r,g,b,a}` 객체로 전달
  - SerializedProperty 명을 정확히 — 예: Canvas의 카메라 참조는 `worldCamera` 가 아니라 `m_Camera`

### 스크립트
- C# 스크립트 생성·수정·삭제 (`create_script`, `manage_script`, `script_apply_edits`)
- Validate (`validate_script`)
- 컴파일 후 콘솔 확인 (`read_console`)

### 에셋
- 머티리얼/텍스처/셰이더 생성·수정 (`manage_material`, `manage_texture`, `manage_shader`)
- ScriptableObject 인스턴스 생성·필드 수정 (`manage_scriptable_object`)
- 프리팹 생성·열기·저장 (`manage_prefabs`)
- 폴더 생성 (`manage_asset action=create_folder`)
- 에셋 검색·이동·복제·이름변경

### 빌드
- Build Settings 씬 등록·재정렬 (`manage_build action=scenes`)
- Player Settings 일부 (product_name, version 등) (`manage_build action=settings`)
- Build 실행 (`manage_build action=build`) — 결과물 검증은 본인 영역

### 에디터 제어
- Play 모드 시작·정지 (`manage_editor action=play|stop`)
- 메뉴 실행 (`execute_menu_item`) — 일부만
- 태그/레이어 추가·제거
- C# 코드 동적 실행 (`execute_code`) — 런타임 상태 조회/수정 가능

### 디버깅
- 콘솔 로그/에러/경고 조회·필터링·Clear (`read_console`)
- Missing script 자동 복구 (`manage_scene action=validate auto_repair=true`)

### 캡처
- Game View 스크린샷 (camera 지정 시 — Screen Space Overlay UI 미포함)
- ScreenCapture API (UI 포함하나 Editor 모드에서 빈 화면 자주 발생)
- Scene View 캡처 (3D 시점)
- **UI 캡처 워크어라운드**: Canvas를 ScreenSpaceCamera 모드로 일시 전환 → 캡처 → 다시 Overlay로 복귀

### Git
- commit / push (auto-commit 훅 + AI 명시적 commit 가능)
- 브랜치 작업 / squash / reset --soft

---

## 2. AI 가 MCP로 *어렵거나 불완전* (본인 손 권장)

| 항목 | 사유 |
|---|---|
| **Inspector 미세 픽셀 단위 조정** | RectTransform 시각 튜닝, Layout 미세 위치는 본인 눈이 빠름 |
| **Animator / Animation Clip 연결** | 그래프 시각 작업 — MCP API 제한적 |
| **복잡한 프리팹 variant 트리** | 부모-variant 관계 깊으면 작업 추적 어려움 |
| **시각 디자인 의도 판단** | "이게 예쁜가" — AI가 모름 |
| **TMP Importer 초기 셋업** | 처음 한 번 본인이 클릭 (영구 효과) |

---

## 3. AI 가 *할 수 없음* (본인 영역 확정)

| 항목 | 사유 |
|---|---|
| **외부 자산 임포트** (FBX/PNG/WAV/OGG 등) | 파일 자체를 만들지 못함 — 본인이 Project 창에 드래그 |
| **Play 모드 *체감* 검증** | 게임필/조작감/난이도 — 본인 플레이만 가능 |
| **Build 결과 .exe 동작 검증** | 실행 + 동작 확인 |
| **외부 시각 자산 디자인** | 아이콘/스프라이트/UI 디자인 |
| **사운드/음악 제작** | |
| **Pivot/Scale 모델 재수정** | DCC 툴 (Blender/Maya 등) 작업 |

---

## 4. MCP로 했을 때 *가끔 막히는 것* (워크어라운드)

| 막힘 | 우회 |
|---|---|
| Active scene 의 GameObject `delete by_id` 권한 거부 | `set_active=false` 비활성화로 우회. 본인이 Inspector에서 직접 삭제 |
| `component_properties` 의 nested 필드 적용 실패 | 생성 후 `set_property` 로 단일 호출 분해 |
| Play 모드 중 `manage_scene action=load` 실패 | `manage_editor action=stop` 으로 먼저 정지 |
| Play 모드 중 `set_property` 일부 실패 | 동일하게 정지 후 |
| ScreenSpaceOverlay Canvas 카메라 캡처 빈 화면 | 임시로 ScreenSpaceCamera 모드로 전환 → 캡처 → 복귀 |
| Component name "CameraController" 등이 `remove` 에서 안 잡힘 | 컴파일된 type 풀네임 사용 또는 enabled=false 로 우회 |

---

## 5. AI가 직접 처리할 때 권장 패턴

### A. 코드 + 씬 작업이 필요한 기능
1. AI 가 스크립트 작성 (`create_script`)
2. `refresh_unity` → 컴파일 확인 (`read_console`)
3. AI 가 씬에 GameObject 생성 + 컴포넌트 부착 (`manage_gameobject` + `manage_components`)
4. AI 가 속성 설정 (`set_property` — 안전한 단위로)
5. AI 가 씬 저장 (`manage_scene action=save`)
6. AI 가 commit (auto 또는 명시적)
7. 본인이 Play로 검증

### B. UI Canvas 빌드 시
- 생성 시 component_properties 는 *기본만 의지*
- 모든 색상, RectTransform anchor/sizeDelta, Layout 옵션은 **개별 set_property**
- Label/자식 텍스트는 부모 영역 채우려면 anchorMin{0,0}/anchorMax{1,1}/sizeDelta{0,0} 명시

### C. 디버깅
1. AI 가 Debug.Log 추가
2. AI 가 Play 시작 → 시뮬레이션 (`execute_code` 로 강제 데미지 등) → console 조회
3. AI 가 정지 → 분석
4. AI 가 수정 → 반복
5. 진단 끝나면 Debug.Log 제거 (commit)

---

## 6. 본인이 *반드시* 하는 일 (변경 없음)

- **게임 디자인 결정** (방향성, 컨셉)
- **AI 작업 결과 *최종 승인***
- **외부 자산 처리** (위 #3)
- **Play 모드 체감 테스트**
- **Inspector 시각 미세 조정** (필요 시)
- **빌드 결과 검증**

---

## 7. 변경 이력

- **2026-05-08**: 신규 작성. UnityMCP CoplayDev 도입 후 AI 능력 확장 반영.
  Editor handoff 문서는 *MCP로 안 되는 작업* 한정으로 의미 축소.
- **2026-06-21**: 화투 Editor 자동화 MCP로 **CoplayDev UnityMCP**(`com.coplaydev.unity-mcp` v9.7.3, HTTP `127.0.0.1:8080/mcp`) 채택. 본 문서의 CoplayDev 툴명(`manage_gameobject`/`execute_code`/`read_console` 등) 전제는 그대로 유효.
- **2026-06-23**: Editor 자동화는 **CoplayDev UnityMCP만** 사용한다. Unity Bridge는 `Window > MCP For Unity > Connect`의 `Start Server`로 켜고, `Advanced > Auto-Start Server on Editor Load` 체크로 Unity 시작 시 자동 기동.

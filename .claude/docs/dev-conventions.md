# 게임 개발 기초 규약 (Unity 6.3 / C#)

> 협업·유지보수를 위한 표준. 출처: Microsoft .NET Coding Conventions, Unity 공식 매뉴얼/블로그(6000.x), justinwasilenko Unity-Style-Guide, Unity Best Practices 커뮤니티 가이드.
> Unity 6 기준 최신 API 반영(`FindObjectsOfType` 등 deprecated 제외).
> **기존 프로젝트 규약(`naming-conventions.md`, `technical-preferences.md`)과 충돌 시 기존 규약 우선.**

---

## 1. C# 코딩 컨벤션

### 네이밍
- ✅ 클래스 / 메서드 / public 필드 / 프로퍼티 / 이벤트: **PascalCase**
- ✅ private·protected 필드: **`_camelCase`** (언더스코어 접두)
- ✅ static private: **`s_camelCase`**
- ✅ 지역 변수·파라미터: **camelCase**
- ✅ 상수: **PascalCase** (프로젝트 통일)
- ✅ 인터페이스: **`I` + PascalCase** (`IDamageable`)
- ✅ Boolean은 긍정형(`IsActive`), 컬렉션은 복수형(`Enemies`)
- ✅ 이벤트: **`On` + PascalCase** (`OnPlayerDied`)
- ❌ 타입을 변수명에 인코딩 금지 (`strName`, `intCount`)

### 접근제한자 & 필드
- ✅ 접근제한자 **항상 명시**
- ✅ 필드는 private 기본, 노출은 프로퍼티로
- ✅ Inspector 노출은 **`[SerializeField] private`** + 읽기전용 프로퍼티
- ❌ `public` 필드로 Inspector 노출 금지

### var / 타입 / 구조
- ✅ 우변 타입이 명확할 때만 `var`
- ✅ 내장 타입 키워드(`string`/`int`) 사용
- ✅ **파일당 1 public 클래스**, 파일명=클래스명
- ✅ `using`은 namespace 밖, 미사용 제거
- ✅ 들여쓰기 4스페이스, Allman 중괄호, 한 줄 한 문장
- ✅ public 멤버에 **XML 주석(`///`)**

---

## 2. Unity 스크립트 모범사례

- ✅ `GetComponent`/참조 조회는 **Awake/Start에서 1회 캐싱**, 불확실하면 `TryGetComponent`
- ❌ `Update`/`FixedUpdate`에서 `GetComponent`·`Find` 호출 금지
- ✅ 객체 탐색은 **`FindFirstObjectByType` / `FindObjectsByType(FindObjectsSortMode.None)`** (초기화 시점만, 결과 캐싱)
- ❌ `FindObjectOfType`/`FindObjectsOfType`(deprecated), 상시 `GameObject.Find` 금지
- ✅ 이벤트는 **OnEnable 구독 / OnDisable 해제** (쌍 일치, 누수 방지)
- ✅ 게임플레이 값은 **ScriptableObject로 외부화**, 하드코딩 금지
- ✅ 연출·타이머는 코루틴 또는 Unity6 `Awaitable`; I/O·백그라운드는 async/`Awaitable`
- ⚠️ `async void`는 이벤트 핸들러 외 금지
- ✅ 빈 `Update()` 정의 금지
- ✅ 기능/레이어 단위 Assembly Definition 분리(단방향 의존) — *도입 시 검토(현재 미사용)*

---

## 3. 에셋 / 모델링 파이프라인

### FBX 임포트
- ✅ **1 unit = 1m** 기준(큐브 1m 대조 검증), DCC export 단위 정렬
- ✅ 캐릭터 Rig: 리타게팅 필요 시 **Humanoid**, 비인간형·정밀 제어 **Generic**
- ✅ Root Motion 사용 시 Animator `Apply Root Motion` 체크 + 클립 `Bake Into Pose` 해제
- ✅ 미사용 데이터(Materials/Cameras/Lights) 임포트 해제, 정적 메시 Read/Write off
- ❌ 모델별 축/스케일 불일치 방치 금지

### 네이밍 프리픽스 (기존 `naming-conventions.md` 우선, 미정의분 보완)
| 종류 | 프리픽스 | 예 |
|---|---|---|
| Static Mesh | `SM_` | `SM_Rock_01` |
| Skeletal Mesh | `SK_` | `SK_Player` |
| Material | `MAT_` | `MAT_Player_Body` |
| Texture(+채널) | `T_` (`_D/_N/_R`) | `T_Player_D` |
| Animation Clip | `{대상}_{동작}` | `Player_Run` |
| VFX | `VFX_` | `VFX_HitSpark` |

- ✅ 텍스처 2의 거듭제곱(POT)·압축, URP SRP Batcher 호환 셰이더
- ✅ 재사용 GameObject는 반드시 프리팹화
- ✅ 폴더는 기능/도메인 단위, `.meta` 항상 버전관리

---

## 4. 씬 / 프리팹 구조
- ✅ **프리팹 우선** (머지 충돌 격리), 참조는 프리팹→프리팹
- ✅ 씬은 빈 부모로 그룹화(`---Managers---` 등)
- ✅ 매니저/싱글턴은 단일 배치(부트스트랩 or DontDestroyOnLoad)
- ❌ 깊은 프리팹 variant 중첩 금지, 상시 미사용 오브젝트 포함 금지

---

## 5. 버전관리 (Git)
- ✅ Unity `.gitignore` (`Library/`,`Temp/`,`Obj/`,`Build/`,`Logs/`, 빌드산출물)
- ✅ Asset Serialization = **Force Text**, **`.meta` 반드시 커밋**
- ✅ 대용량 바이너리는 **Git LFS** + File Locking
- ✅ 씬/프리팹 충돌은 UnityYAMLMerge(Smart Merge)
- ✅ 커밋은 작은 단위·단일 목적 (본 프로젝트는 trunk-based + auto-commit 훅)
- ❌ 동일 씬/프리팹 동시 편집 금지 → 프리팹 분리로 예방

---

## 6. 협업 일반
- ✅ 게임플레이 값 데이터 외부화, 도메인 소유권 명시
- ✅ public API 문서주석 + 리뷰 후 머지 (B타입: AI 작성→PM 승인)
- ✅ 주요 기술 결정은 **ADR**(`docs/architecture/`)
- ✅ 핵심 로직(밸런스/데미지)은 단위 테스트 우선, 싱글턴보다 DI 선호
- ❌ 단독 도메인 외 변경 금지 — 충돌 시 escalation, 최종 승인 PM

---

## 7. UI 제작 — uGUI vs UI Toolkit + MCP (`manage_ui`)

> 리서치 결과: `manage_ui`(MCP for Unity)는 **UI Toolkit(UXML/USS/UIDocument) 전용** — uGUI(Canvas)는 못 다룬다.
> 현재 프로젝트는 기존 패널(ShopPanel/DialogueUI/SettingsPanel 등)이 **코드형 uGUI**다.

### 채택 방침 (하이브리드)
- ✅ **기존 uGUI 패널은 유지** — 잘 도는 코드 재작성 비용 대비 이득 없음.
- ✅ **신규 정적 메뉴/패널(도감·설정·상점 레이아웃 등)은 UI Toolkit + `manage_ui`** — AI 생성 안정성·디자인/로직 분리 우위.
- ✅ **월드 공간 UI(머리 위 체력바·상호작용 프롬프트)는 uGUI** 유지 — UI Toolkit보다 단순/견고.
- ✅ **구조/스타일은 `manage_ui`(UXML/USS), 동작(콜백·데이터 채움)은 C# 스크립트** — `manage_ui`는 버튼 콜백/바인딩을 못 묶음. `root.Q<T>("name")` + `RegisterCallback<ClickEvent>` / `SetBinding`.

### `manage_ui` 실전 게이트 (검증된 함정 — Codex 구축 중 실측 2026-06-09)
- ⚠️ **`<ui:Style src="x.uss">`는 반드시 스타일을 적용할 VisualElement(예: 루트 컨테이너)의 *첫 자식*으로 둔다.** `<ui:UXML>` 직속에 두면 스타일시트가 트리에 안 붙는다(`styleSheets.count==0`).
- ⚠️ **그래도 `<ui:Style>` 링크는 불안정**(asset 생성 직후 import 타이밍에 따라 `styleSheets.count==0` 재발). **확실한 방법: 컨트롤러에 `[SerializeField] StyleSheet` 필드를 두고 `OnEnable`에서 `root.styleSheets.Add(_styleSheet)`로 런타임 부착**(빌드에서도 동작, asset import 타이밍 무관). 본 프로젝트 `CodexController`가 이 패턴.
- ⚠️ **한글 폰트**: `FontAsset.CreateFontAsset("Malgun Gothic","Regular")`(DynamicOS)를 USS `-unity-font-definition`에 물리면 **Play 종료 후 `MissingReferenceException: Texture2D` 다발**(런타임 아틀라스 파괴). → **프로젝트 기본 PanelTextSettings의 한글 폴백이 이미 동작**하므로 커스텀 폰트 불필요. 굳이 지정해야 하면 DynamicOS 대신 영구 아틀라스 폰트로.
- ⚠️ **전체화면 오버레이는 `position:absolute; left/top/right/bottom:0`** 로 채운다. UIDocument 루트(`{GO}-container`) 밑 요소의 `flex-grow:1`은 환경에 따라 부모 높이를 못 받아 콘텐츠 크기로 쪼그라들 수 있음.
- ⚠️ **`render_ui`·`worldBound`는 에디터 비포커스 Play에서 신뢰도 낮음**(레이아웃 패스 미실행 → stale/빈 화면). 기능 검증은 `styleSheets.count`·`Q` 조회·`childCount`로, **시각 검증은 Game 뷰 포커스 상태(본인 Play)**로.
- ✅ 절차: `create`(uxml)→`create`(uss)→PanelSettings(`ScaleWithScreenSize`,1920×1080)→`attach_ui_document`(`sort_order`)→`create_script` 컨트롤러(`[RequireComponent(UIDocument)]`, `OnEnable`에서 `rootVisualElement` 쿼리 + StyleSheet 런타임 부착).

### "보고 적용 결정" 미리보기 워크플로 (검증됨)
- UI Toolkit UI는 **에셋 파일(.uxml/.uss) + UIDocument**라 게임 로직과 분리됨 → **미적용 미리보기 가능**.
- 절차: uxml/uss 생성 → **임시 GO**에 `attach_ui_document` → Play → `render_ui`(2회) PNG 확인 → ① 채택: 실제 씬 GO+컨트롤러로 배선 / ② 폐기: 임시 GO·에셋 삭제(기존 게임 무영향).
- 여러 변형을 각각 렌더해 비교 선택 가능(`Preview_*.uxml` 네이밍 권장).

---

## 변경 이력
- 2026-06-09: 신규. 웹 리서치 기반 6영역 규약 정리(협업 기반).
- 2026-06-09: §7 추가 — UI Toolkit + `manage_ui` 하이브리드 방침 및 실전 함정(스타일 링크 위치/한글 폰트/전체화면/렌더 검증).

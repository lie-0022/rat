# Unity Editor 핸드오프 표준 양식

> **2026-05-08 업데이트**: Unity MCP 도입 후, **대부분의 Editor 작업은 AI가 직접 처리**한다.
> 본 문서의 핸드오프 양식은 이제 **MCP로 안 되거나 본인 손이 명백히 더 적합한 작업에 한정**해 사용한다.
>
> **MCP가 처리 가능한 일은 핸드오프하지 말 것** — `.claude/docs/mcp-capabilities.md` 참조.
>
> 핸드오프가 여전히 유효한 케이스:
> - 외부 자산 임포트 후 시각/청각 검증
> - Animator 그래프 작업 (MCP API 제한적)
> - Inspector 픽셀 단위 시각 튜닝
> - 빌드 출력물 실행 검증
> - Play 모드 *체감* 테스트 (게임필 판정)
>
> 그 외(GameObject 생성·컴포넌트 부착·속성 설정·씬 저장·머티리얼·SO 등)는 AI가 MCP로 직접 처리한다.
> **모든 핸드오프는 본 문서의 "표준 양식" 그대로 따른다.** 임의 변형 금지.

---

## 핵심 원칙 3개

1. **단계별 진행**: 핸드오프 전체를 한 번에 쏟지 않는다. **Step 1만 먼저 제시**하고, 본인이 "다음" / "OK" / "됐어" 신호를 줄 때까지 기다린다.
2. **경로 명시**: 모든 GameObject·Asset은 **절대 경로**로 표기한다.
   - Hierarchy: `Canvas/HUD/WeaponSlotsHUD/Slot0/Label`
   - Project: `Assets/Prefabs/Player_Swordsman.prefab`
   - "어디 있는지 알지" 가정 금지. 경로 한 줄로 명확히.
3. **베이스라인 존중**: 아래 "절대 설명하지 말 것" 항목은 1줄도 쓰지 않는다.

---

## 베이스라인 (전제 — 절대 설명하지 말 것)

본인은 Unity 6.3 LTS를 일상적으로 사용하는 개발자다.
다음은 **모두 안다고 가정하며, AI는 절대 설명하지 않는다**:

- 씬 열기 / 저장 / 전환
- Hierarchy에서 GameObject 생성·삭제·이름변경(F2)·우클릭 메뉴
- Ctrl+D 복제, 부모-자식 드래그
- Inspector에서 `Add Component` 버튼 위치
- RectTransform 다루기 (Anchor Preset, Pos/Width/Height)
- TMP Importer 첫 등장 시 `Import TMP Essentials` 누르는 것
- Play 모드 ▶ 버튼 위치
- Project 창에서 파일 찾기·드래그
- Prefab 저장 (Hierarchy → Project 드래그)
- Tag / Layer 메뉴 위치
- Console 창 위치 / 에러 색상
- Build Profile / Build 메뉴

> 이런 단계가 핸드오프에 1줄이라도 들어가면 양식 위반이다.

---

## 진행 흐름 (단계별)

```
AI: Step 1 제시 (단일 단계 + 그 단계의 검증 1줄)
   ↓
본인: 수행 후 "다음" 또는 문제 보고
   ↓
AI: Step 2 제시
   ↓
... 반복 ...
   ↓
AI: 마지막 Step → 전체 검증 시나리오
```

- Step 1개 = **단일 목적의 작업 단위** (예: "GameObject 생성 + 위치 지정", "컴포넌트 1개 부착", "Inspector 필드 1세트 연결")
- Step 안에서 표/트리는 사용 가능 (단일 단계의 정보 압축은 OK)
- **Step끼리 한꺼번에 묶어 제시 금지**
- 단순 작업(1~2 step)이면 합쳐도 됨 — 판단은 AI

---

## Step 양식 (각 Step마다 이 구조)

```markdown
## Step {N} — {짧은 제목}

### 위치
- **Scene/Prefab**: {Project 절대 경로}
- **부모 Hierarchy 경로**: {Scene 루트부터 절대 경로}

### 작업
{표 또는 트리 1개. 잡설 금지.}

### 이 Step의 검증
{1줄. "Hierarchy에 X가 보이면 OK" 식}
```

전체 작업이 끝났을 때만 **마지막에 "최종 검증" 섹션**을 별도로 붙인다.

---

## 경로 표기 규칙 (필수)

### Hierarchy 경로
- 항상 Scene 루트부터 슬래시로 구분: `Canvas/HUD/WeaponSlotsHUD/Slot0/Label`
- 부모 GameObject가 여럿일 수 있으면 **정확한 부모를 1개 지목**한다.
  - 나쁨: "Canvas 어딘가에"
  - 좋음: "`Canvas/HUD` 직하단" (이런 부모가 없으면 Step 1에서 만들어야 함)

### Project Asset 경로
- 항상 `Assets/...` 부터 표기: `Assets/Scenes/GamePlay.unity`, `Assets/Prefabs/UI/CardView.prefab`
- 스크립트 첨부 시: `Assets/Scripts/UI/WeaponSlotsHUD.cs`
- 데이터: `Assets/Data/Weapons/Weapon_Sword.asset`

### Inspector 필드 경로
- 코드의 `[SerializeField]` 이름 그대로: `_slots[0].Root`
- 자식 필드는 점 표기: `_cardView.WeaponNameText`

---

## 표 양식 (Step 안에서 사용)

### 컴포넌트 / RectTransform 표
| GameObject (Hierarchy 경로) | 컴포넌트 / 설정 |
|---|---|
| Canvas/HUD/WeaponSlotsHUD | RectTransform: Anchor=bottom-right, Pos(-20,20), Size(200,100). `WeaponSlotsHUD` 컴포넌트 |

### Inspector 연결 표
| 필드 | 연결 대상 (Hierarchy 경로 or Project 경로) |
|---|---|
| _slots[0].Root | Canvas/HUD/WeaponSlotsHUD/Slot0 |
| _data | Assets/Data/Weapons/Weapon_Sword.asset |

---

## 절대 쓰지 말 것

- "Unity 상단 메뉴: File > Open Scene"
- "Hierarchy 창에서 우클릭 → Create Empty"
- "F2 눌러서 이름 바꾸기"
- "Ctrl+D 두 번 눌러서 복제"
- "Inspector 맨 아래 Add Component 버튼 클릭"
- "검색창에 X 입력"
- "▶ Play 버튼"
- "Canvas 어딘가에" / "적당한 위치에" — 항상 정확한 절대 경로
- 여러 Step을 한 응답에 묶어 제시 (단순 1~2 step 제외)
- 친절한 어조 도입부 ("자, 이제 시작하자")
- 각 단계 끝의 "잘 됐으면 다음으로" 같은 멘트 (Step 검증 1줄로 대체)
- 이모지

---

## 본인 → AI 피드백 양식 (문제 발생 시)

```
[Step]: 어디서 막혔는지 (e.g. "Step 3, Inspector 연결 표 3행")
[증상]: 무엇이 안 보이거나 에러
[Console 에러]: (있으면 그대로 복사)
```

---

## 예시 (참고용)

### 좋은 예 — 단계별 + 경로 명시

```markdown
## Step 1 — HUD 루트 생성

### 위치
- **Scene**: Assets/Scenes/GamePlay.unity
- **부모 Hierarchy 경로**: Canvas

### 작업
| GameObject (Hierarchy 경로) | 컴포넌트 / 설정 |
|---|---|
| Canvas/WeaponSlotsHUD | RectTransform: Anchor=bottom-right, Pos(-20,20), Size(200,100). `Vertical Layout Group` (선택) |

### 이 Step의 검증
Hierarchy에 `Canvas/WeaponSlotsHUD` 노드가 보이고 Game 뷰 우하단에 빈 영역이 잡힘.
```

→ 본인 "다음"

```markdown
## Step 2 — 슬롯 3개 + Label 생성
... (이후 단계만 제시)
```

### 나쁜 예 (양식 위반)

> "Unity 켜고 GamePlay.unity 더블클릭으로 열어. Hierarchy에서 Canvas 우클릭 → Create Empty 하고 이름을 WeaponSlotsHUD로 바꿔(F2). 그다음 Slot0/1/2를 만들고 각각에 Label을 붙이고 컴포넌트도 붙이고 Inspector에 6개 필드 다 연결한 다음 Play 모드로..."

→ 베이스라인 위반(F2 등) + 단계별 진행 위반(전체 한꺼번에) + 경로 누락("Canvas 어딘가에").

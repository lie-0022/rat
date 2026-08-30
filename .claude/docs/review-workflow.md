# 리뷰 워크플로우 (B타입)

> 1인 개발자 + AI 협업이므로 모든 승인은 본인(PM)이 한다.
> CCGS 기본 협업 프로토콜(`docs/COLLABORATIVE-DESIGN-PRINCIPLE.md`)의 Question→Options→Decision→Draft→Approval 패턴을 따른다.

---

## 1. AI 작업물 → 본인 승인 흐름

### 1-1. 코드 (`.cs`)

```
1. AI 작성 (또는 수정)
2. 본인 검토 — 전체 흐름 위주, 디테일은 AI 신뢰
3. 의문 있으면 "이 부분 설명해줘" / "왜 이렇게?"
4. OK → AI 가 commit (auto-commit 훅)
5. Editor 적용 → Play 테스트
6. 문제 시 → 패턴 2 (버그 수정)로 복귀
```

### 1-2. 문서 (GDD, ADR, README, 등 `.md`)

```
1. AI 초안 (8섹션 GDD 룰 준수, 증분 작성)
2. 본인 검토 — 의도와 일치하는가, 빠진 게 있는가
3. 수정 요청 또는 승인
4. 섹션 단위 승인 시 AI가 즉시 파일에 기록
5. 전체 완료 시 commit
```

### 1-3. 큰 결정 (아키텍처, 게임 디자인 방향)

```
1. AI 옵션 제시 — 보통 A/B/C + 장단점 + 근거
2. 본인 선택
3. 선택 근거 ADR(`docs/architecture/adr-XXXX.md`)로 기록
   - AI가 작성, 본인이 승인
4. 코드/문서에 적용
```

---

## 2. 거부 / 수정 패턴

본인이 "이거 별로"라고 할 때:

- **무엇이 별로인지 한 줄로** 표현 (구체적이지 않아도 OK)
  - "복잡함" / "느낌이 다름" / "유지보수 힘들어 보임"
- **AI 응답 옵션**
  - 대안 즉시 제시
  - 또는 "왜 별로인지 더 자세히?" 역질문 (구체화 필요할 때)
- **본인 응답**
  - 대안 중 선택 또는 "다시 옵션 더 보여줘"

> 거부는 비용이 아니라 정보다. 거부 사유가 ADR로 남으면 더 좋다.

---

## 3. 영역별 책임 매트릭스 (B타입 + Unity MCP 갱신, 2026-05-08)

| 영역 | 작성 | 검토 | 승인 | 적용 |
|------|------|------|------|------|
| 코드 (`.cs`) | AI | 본인 | 본인 | AI (파일 기록) |
| GDD 문서 | AI | 본인 | 본인 | AI (파일 기록) |
| ADR | AI | 본인 | 본인 | AI (파일 기록) |
| 핸드오프 문서 | AI | 본인 | 본인 | 본인 (남은 영역만) |
| **Scene 구성** | AI | 본인 | 본인 | **AI (MCP)** |
| **GameObject / 컴포넌트** | AI | 본인 | 본인 | **AI (MCP)** |
| **Prefab CRUD** | AI | 본인 | 본인 | **AI (MCP)** |
| **Inspector 일반 값** | AI | 본인 | 본인 | **AI (MCP)** |
| **머티리얼/SO/Build Settings** | AI | 본인 | 본인 | **AI (MCP)** |
| Inspector 픽셀 미세 튜닝 | AI 1차 | 본인 | 본인 | 본인 (필요 시) |
| Animator 그래프 | (지시) AI | 본인 | 본인 | 본인 |
| 에셋 임포트 (FBX/PNG/WAV) | — | — | — | 본인 |
| Play 모드 체감 검증 | — | — | — | 본인 |
| Build 결과물 실행 검증 | — | — | — | 본인 |
| 커밋 | AI (auto + 명시적) | 본인 | (자동) | AI |
| Push | AI | 본인 | 본인 | AI / 본인 / cron |

핵심 변화: **Scene/GameObject/컴포넌트/Inspector 일반 값**은 이제 AI가 MCP로 직접 적용. 본인은 검토·승인·체감 검증에 집중.

---

## 4. 에이전트 위계와의 관계

CCGS의 48 에이전트 위계(`agent-coordination-map.md`)는 그대로 유지하되,
**최종 승인자는 항상 본인(Human Developer)**이다.

- `creative-director` / `technical-director` / `producer` 등 Tier 1 에이전트가
  의견을 내더라도 본인 승인 없이 파일 변경 금지 (CLAUDE.md 협업 프로토콜).
- 에이전트 간 충돌 시 위계 따라 escalation, 최종은 본인.

---

## 5. 빠른 참조

- 코드 변경 → AI 작성 → 본인 OK → commit
- 디자인 결정 → AI 옵션 → 본인 선택 → ADR
- 거부 → 한 줄 사유 → AI 대안
- Editor 작업 → AI 핸드오프 → 본인 적용 → Play 검증

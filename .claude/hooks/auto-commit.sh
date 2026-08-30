#!/usr/bin/env bash
# auto-commit.sh
# Stop 이벤트에서 변경사항 자동 커밋
# - 변경사항 없으면 skip
# - "auto:" prefix로 식별
# - push는 하지 않음 (별도 스케줄러에서)

set -euo pipefail

# 워크트리 경로: Claude이 제공한 $CLAUDE_PROJECT_DIR 우선, 없으면 현재 디렉토리
REPO_PATH="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# git 저장소가 아니면 조용히 종료
cd "$REPO_PATH" 2>/dev/null || exit 0
git rev-parse --git-dir >/dev/null 2>&1 || exit 0

# untracked + modified 모두 체크
CHANGES=$(git status --porcelain | wc -l)

if [ "$CHANGES" -eq 0 ]; then
    # 변경 없음, 조용히 종료
    exit 0
fi

# 변경된 파일 목록 (최대 5개만 메시지에 포함)
CHANGED_FILES=$(git status --porcelain | head -5 | awk '{print $2}' | tr '\n' ',' | sed 's/,$//')
if [ "$CHANGES" -gt 5 ]; then
    CHANGED_FILES="${CHANGED_FILES} (+$(($CHANGES - 5)) more)"
fi

# 타임스탬프
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S")

# 모든 변경 add
git add -A

# 커밋
git commit -m "auto: checkpoint ${TIMESTAMP}

Files changed: ${CHANGED_FILES}

[Auto-committed by Stop hook — squash before push]

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>" 2>&1 | tail -3

exit 0

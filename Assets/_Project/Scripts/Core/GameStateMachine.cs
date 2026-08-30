using System;
using UnityEngine;

namespace RatGame.Core
{
    public enum GameState { Boot, MainMenu, Lobby /*=Hub씬, 파티 대기*/, InRun, RunResult }

    /// <summary>
    /// 최상위 게임 상태 (docs/02). 전이 규칙: Boot→MainMenu→Lobby→InRun→RunResult→Lobby.
    /// InRun 내부 세부 흐름(구역 진행)은 RunManager 소유 (docs/09).
    /// 넷코드 붙는 태스크 0-3에서 호스트 전이 → NetworkVariable 복제로 확장한다. W1은 로컬 전용.
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        public static GameStateMachine Instance { get; private set; }

        public GameState Current { get; private set; } = GameState.Boot;
        public event Action<GameState, GameState> StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>상태 전이. 허용되지 않은 전이는 에러 로그 후 무시 (docs/02 전이 규칙).</summary>
        public bool TransitionTo(GameState next)
        {
            if (!IsValidTransition(Current, next))
            {
                Log.Error($"잘못된 상태 전이: {Current} → {next}");
                return false;
            }
            var prev = Current;
            Current = next;
            Log.Dev($"상태 전이: {prev} → {next}");
            StateChanged?.Invoke(prev, next);
            return true;
        }

        private static bool IsValidTransition(GameState from, GameState to) => (from, to) switch
        {
            (GameState.Boot, GameState.MainMenu) => true,
            (GameState.MainMenu, GameState.Lobby) => true,
            (GameState.Lobby, GameState.InRun) => true,
            (GameState.InRun, GameState.RunResult) => true,
            (GameState.RunResult, GameState.Lobby) => true,
            // 세션 종료·호스트 이탈 시 어디서든 메뉴/로비로 복귀 허용 (docs/03 접속 해제 처리)
            (GameState.Lobby, GameState.MainMenu) => true,
            (GameState.InRun, GameState.Lobby) => true,
            _ => false
        };
    }
}

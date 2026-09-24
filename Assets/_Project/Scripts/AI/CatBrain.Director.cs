using RatGame.Core;
using RatGame.Run;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 경계도 디렉터 손잡이 (design/cat-ideas/12, 2026-09-24). RunDirector의 모드를 읽어 **관찰로 알 수 있는 것만** 바꾼다:
    /// 잠 길이·머무름·기억 칸 방문 확률·다음 스팟 가중치·Finale의 쥐구멍 편향·호기심 무시. 감각 수치(시야·청각·게이지)는 그대로.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>호스트: 상태가 바뀔 때마다 (고양이, 이전, 다음). 디렉터의 긴장 계산 입력.</summary>
        public static event System.Action<CatBrain, CatState, CatState> ServerStateChanged;

        /// <summary>예민함(Build-up·Finale) — 클라 연출용(꼬리 빠르게). 디렉터 모드 자체는 복제하지 않는다.</summary>
        public NetworkVariable<bool> Alert = new NetworkVariable<bool>(false);

        private RunDirector _director;
        private DepositZone _hole;
        private float _nextDirectorLookup;

        private DirectorMode DirMode
        {
            get
            {
                if (Time.time >= _nextDirectorLookup)
                {
                    _nextDirectorLookup = Time.time + 2f;
                    _director = RunManager.Instance != null ? RunManager.Instance.GetComponent<RunDirector>() : null;
                    if (_hole == null) _hole = FindFirstObjectByType<DepositZone>();
                }
                return _director != null ? _director.Mode : DirectorMode.Calm;
            }
        }
        private bool DirAlert => DirMode is DirectorMode.Buildup or DirectorMode.Finale;

        private float DirSleepMul => DirAlert ? _balance.BuildupSleepMul : 1f;
        private float DirLookDwellMul => DirAlert ? _balance.BuildupDwellMul : 1f;
        private float DirRoutineDwellMul => DirMode == DirectorMode.Relief ? _balance.ReliefDwellMul : 1f;
        private float DirMemoryBonus => DirAlert ? _balance.BuildupMemoryBonus : 0f;
        private bool DirIgnoreCuriosity => DirMode == DirectorMode.Finale;

        private float DirSpotWeightMul(CatSpotType type)
        {
            if (DirMode != DirectorMode.Relief) return 1f;
            return type switch
            {
                CatSpotType.Groom or CatSpotType.Sun or CatSpotType.Bed => _balance.ReliefRoutineWeightMul,
                CatSpotType.Look => _balance.ReliefLookWeightMul,
                _ => 1f
            };
        }

        private void RaiseStateChanged(CatState prev, CatState next) => ServerStateChanged?.Invoke(this, prev, next);

        // Update에서 (호스트)
        private void TickDirectorHints()
        {
            bool alert = DirAlert;
            if (Alert.Value != alert) Alert.Value = alert;
        }

        // GoToNextSpot 맨 앞: Finale면 쥐구멍 근처를 자주 돈다 — 귀환 카운트다운의 마지막 긴장
        private bool TryFinaleHoleVisit()
        {
            if (DirMode != DirectorMode.Finale || _hole == null) return false;
            if (Random.value >= _balance.FinaleHoleBias) return false;
            Vector3 dest = _movement.RandomPointAround(_hole.transform.position, _balance.FinaleHoleRadius);
            // 기억 칸 방문 흐름을 재사용 — 도착하면 킁킁 1.5s 뒤 다음 스팟
            _memoryVisit = true;
            _memoryCell = Cell(dest);
            _sniffUntil2 = -1f;
            _movement.MoveTo(dest, _balance.CatPatrolSpeed);
            Log.Dev($"고양이 [{name}]: 마지막 경계 — 쥐구멍 쪽 {dest:F1}");
            return true;
        }
    }
}

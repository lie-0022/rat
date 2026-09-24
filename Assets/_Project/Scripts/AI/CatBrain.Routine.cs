using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 하루 루틴의 빠진 조각 (design/cat-ideas/02, 2026-09-24).
    ///  예고: 루틴 스팟(밥·화장실·햇볕·잠자리·물)으로 출발할 때 모든 클라에 신호 → 고양이 가까이 있는 쥐에게 소리 자막. 시야 밖에서 상태를 읽는다.
    ///  화장실 → 우다다(zoomies): 볼일 12s 뒤 10s 동안 추격보다 빠르게 아무 데나 뛴다. 목적 없음 — 복도는 도박.
    /// </summary>
    public partial class CatBrain
    {
        private float _zoomiesUntil;
        private float _nextZoomStep;
        private bool _litterDone;   // 화장실 머무름을 마쳤다 → 머무름 끝나면 우다다

        // GoToNextSpot 끝에서: 루틴 스팟으로 간다고 알린다
        private void AnnounceNextSpot(CatSpotType type)
        {
            CatCueKind? cue = type switch
            {
                CatSpotType.Food => CatCueKind.Food,
                CatSpotType.Litter => CatCueKind.Litter,
                CatSpotType.Sun => CatCueKind.Sun,
                CatSpotType.Bed => CatCueKind.Bed,
                CatSpotType.Water => CatCueKind.Water,
                _ => null
            };
            if (cue.HasValue) CatCueClientRpc((byte)cue.Value, transform.position);
        }

        [ClientRpc]
        private void CatCueClientRpc(byte kind, Vector3 catPos) => EventBus.RaiseCatCue((CatCueKind)kind, catPos);

        // ArriveAtSpot 머무름이 끝났을 때 (TickPatrol): 화장실 뒤면 우다다
        private bool TryStartZoomies()
        {
            if (!_litterDone) return false;
            _litterDone = false;
            SetState(CatState.Zoomies);
            return true;
        }

        private void EnterZoomiesState()
        {
            _zoomiesUntil = Time.time + _balance.CatZoomiesSeconds;
            _nextZoomStep = 0f;
            Log.Dev($"고양이 [{name}]: 우다다!");
        }

        private void TickZoomies()
        {
            if (CheckEscalation()) return; // 코앞 쥐는 쫓는다
            if (Time.time >= _zoomiesUntil) { Log.Dev($"고양이 [{name}]: 우다다 끝"); SetState(CatState.Return); return; }
            if (Time.time < _nextZoomStep && !_movement.Arrived) return;
            _nextZoomStep = Time.time + _balance.CatZoomiesStep;
            _movement.MoveTo(_movement.RandomPointAround(transform.position, _balance.CatZoomiesRadius), _balance.CatZoomiesSpeed);
        }
    }
}

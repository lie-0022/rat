using System.Collections.Generic;
using RatGame.Core;
using RatGame.World;
using UnityEngine;

namespace RatGame.Run
{
    /// <summary>
    /// 쥐덫 미끼 (고양이 129): 채우기가 끝난 뒤 쥐덫마다 확률로, 같은 방(벽에 안 막힌 가까운) 작은 음식 하나를 판 위로 옮긴다.
    /// 새로 만들지 않고 옮기기만 해서 맵 식량 총량은 그대로 (판단 부탁 7과 안 엮이게). 호스트가 맵을 지을 때 한 번.
    /// </summary>
    public partial class GridZoneBuilder
    {
        private void MaybeBaitTraps(System.Random rng)
        {
            if (_zone.TrapBaitChance <= 0f) return;
            var food = new List<CarryableItem>();
            foreach (var it in FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
                if (it.IsSpawned && !it.IsHeavy && it.EffectiveValue > 0 && it.CarrierIds.Count == 0) food.Add(it);
            int wallMask = LayerMask.GetMask("RoomStatic");
            int baited = 0, traps = 0;
            foreach (var trap in FindObjectsByType<MouseTrap>(FindObjectsSortMode.None))
            {
                if (!trap.IsSpawned) continue;
                traps++;
                if (rng.NextDouble() >= _zone.TrapBaitChance) continue;
                CarryableItem best = null; float bestD = _zone.TrapBaitSearchMeters;
                foreach (var it in food)
                {
                    float d = Vector3.Distance(it.transform.position, trap.transform.position);
                    if (d >= bestD) continue;
                    if (Physics.Linecast(trap.transform.position + Vector3.up * 0.3f, it.transform.position + Vector3.up * 0.1f, wallMask, QueryTriggerInteraction.Ignore)) continue; // 벽 너머 = 다른 방
                    best = it; bestD = d;
                }
                if (best == null) continue;
                food.Remove(best);
                trap.ServerSetBait(best);
                baited++;
            }
            if (traps > 0) Log.Dev($"벽 속: 쥐덫 미끼 {baited}/{traps}");
        }
    }
}

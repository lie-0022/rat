using System.Collections.Generic;
using RatGame.Core;
using RatGame.Data;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.World
{
    /// <summary>운반 물건 — 대형 물건의 잡는 자리 자동 계산(콜라이더 긴 변 양쪽). CarryableItem.cs에서 옮김 (고양이 205).</summary>
    public partial class CarryableItem
    {
        // ---- 자동 대형 ----

        // 콜라이더(로컬) 기준: 긴 수평축을 따라 잡도록 짧은 축 방향 양쪽 면의 윗면 중앙에 자리.
        // stand 거리는 월드 미터라 로컬로 환산(스케일 나눔). 아이템 모양이 바뀌어도 자동으로 따라감
        private void EnsureCarrySlots()
        {
            if (_carrySlots != null) return;
            var box = GetComponent<BoxCollider>();
            Vector3 center = box != null ? box.center : Vector3.zero;
            Vector3 half = box != null ? box.size * 0.5f : Vector3.one * 0.5f;
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f), sz = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
            bool longIsZ = half.z * sz >= half.x * sx;
            float stand = _balance.CarrySlotStandDistance;

            _carrySlots = new CarrySlot[HeavySlotCount];
            for (int i = 0; i < HeavySlotCount; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                Vector3 anchor = longIsZ
                    ? new Vector3(center.x + sign * (half.x + stand / sx), center.y + half.y, center.z)
                    : new Vector3(center.x, center.y + half.y, center.z + sign * (half.z + stand / sz));
                _carrySlots[i] = new CarrySlot { LocalAnchor = anchor };
            }
        }

        private int ChooseCarrySlot(Vector3 ratPos)
        {
            EnsureCarrySlots();
            int best = -1;
            float bestScore = float.MaxValue;
            bool anyTaken = _gripOwners.Count > 0;
            for (int i = 0; i < _carrySlots.Length; i++)
            {
                if (_gripOwners.ContainsKey(i)) continue;
                Vector3 stand = transform.TransformPoint(_carrySlots[i].LocalAnchor);
                float score;
                if (anyTaken)
                {
                    float nearestTaken = float.MaxValue;
                    foreach (int taken in _gripOwners.Keys)
                        nearestTaken = Mathf.Min(nearestTaken,
                            Vector3.Distance(stand, transform.TransformPoint(_carrySlots[taken].LocalAnchor)));
                    score = -nearestTaken; // 점유 자리에서 멀수록 좋음 (반대편)
                }
                else
                {
                    score = Vector3.Distance(stand, ratPos); // 첫 사람은 가까운 자리
                }
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}

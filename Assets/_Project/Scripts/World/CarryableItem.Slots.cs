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

        // 콜라이더(로컬) 기준: 긴 수평축을 따라 잡도록 짧은 축 방향 양쪽 면에 자리 (높이는 쥐가 잡는 높이 — 고양이 213).
        // stand 거리는 월드 미터라 로컬로 환산(스케일 나눔). 아이템 모양이 바뀌어도 자동으로 따라감
        private void EnsureCarrySlots()
        {
            if (_carrySlots != null) return;
            var box = GetComponent<BoxCollider>();
            Vector3 center = box != null ? box.center : Vector3.zero;
            Vector3 half = box != null ? box.size * 0.5f : Vector3.one * 0.5f;
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f), sz = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
            float sy = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            // 높이: 윗면이 아니라 바닥 + 쥐가 잡는 높이 (윗면이 더 낮으면 윗면) — 고양이 213
            float gripY = center.y - half.y + Mathf.Min(_balance.CarryGripHeight / sy, half.y * 2f);
            bool longIsZ = half.z * sz >= half.x * sx;
            float stand = _balance.CarrySlotStandDistance;

            int count = HeavySlotCount;
            _carrySlots = new CarrySlot[count];
            // 2자리: 긴 변 양쪽 한가운데. 4자리(특대): 긴 변 양쪽에 둘씩 — 긴 축 1/4·3/4 지점 (docs/05 "특대 grip 4", 고양이 212).
            // 순서는 양쪽 번갈아(0 = +쪽, 1 = −쪽, …) — 두 번째 사람이 반대편에 서는 기존 규칙(ChooseCarrySlot)과 맞게
            for (int i = 0; i < count; i++)
            {
                float sign = i % 2 == 0 ? 1f : -1f;
                float along = count <= 2 ? 0f : (i < 2 ? 0.5f : -0.5f); // 긴 축 반길이 비율
                Vector3 anchor = longIsZ
                    ? new Vector3(center.x + sign * (half.x + stand / sx), gripY, center.z + along * half.z)
                    : new Vector3(center.x + along * half.x, gripY, center.z + sign * (half.z + stand / sz));
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

using System.Collections.Generic;
using NUnit.Framework;

namespace RatGame.Tests
{
    /// <summary>
    /// 벽 속 맵을 실제로 깔아 본다 (고양이 339) — MapPlanTests는 설계도(칸 그래프)만 본다. 여기선 방 프리팹을 놓고 NavMesh를 구워
    /// 방 겹침 0 · 출발 → 목적지(쥐 전용 배관 말고 걸어갈 문) 닿음 · 모든 방 닿음. 큰 물건은 배관을 못 지나가서 목적지가 배관으로만 이어지면 판을 못 깬다.
    /// (500시드 확인 — 전엔 검사기가 목적지 첫 문만 봐서 배관 쪽이면 "막힘"으로 셌다)
    /// </summary>
    public class WallLayoutTests
    {
        [Test]
        public void 벽_속_맵_100시드_겹침없고_목적지와_모든_방에_닿음()
        {
            var bad = new List<string>();
            for (int i = 1; i <= 100; i++)
            {
                string r = RatGame.Editor.GridZoneTools.PreviewInActiveScene(i * 104729, "Temp/grid_test.png");
                string first = r.Split('\n')[0];
                if (!first.Contains("방 겹침 0") || !first.Contains("출발→목적지 OK") || !first.Contains("못 가는 방 0")) bad.Add(r);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}

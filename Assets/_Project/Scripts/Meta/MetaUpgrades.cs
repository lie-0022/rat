using RatGame.Core;
using RatGame.Data;

namespace RatGame.Meta
{
    /// <summary>
    /// 팀 업그레이드 레벨 (docs/11 상점). 누계와 같이 호스트 저장 파일에만 있다 — 세션 주인 기준.
    /// 구매는 VendingMachine(호스트)이 하고, 각 플레이어에는 PlayerUpgrades NetworkVariable로 뿌린다.
    /// </summary>
    public static class MetaUpgrades
    {
        public static UpgradeLevels Levels => SaveService.Data.Upgrades;

        public static int GetLevel(UpgradeEffect effect) => Levels.Get(effect);

        /// <summary>레벨을 올리고 바로 저장. 누계 차감은 호출자가 같은 저장에 묶어서 한다.</summary>
        public static void SetLevel(UpgradeEffect effect, int level)
        {
            var levels = SaveService.Data.Upgrades;
            levels.Set(effect, level);
            SaveService.Data.Upgrades = levels;
            SaveService.Save();
        }
    }
}

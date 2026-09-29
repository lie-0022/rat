using System.Collections.Generic;

namespace RatGame.EditorTools
{
    /// <summary>임시 영어 번역 2 — 결과 화면·로딩 팁부터 (고양이 195). 목록 1이 300줄에 닿아 나눔. 둘 다 LocalizeMenus가 표에 넣는다.</summary>
    public static partial class LocalizationTools
    {
        private static readonly Dictionary<string, string> DummyEn2 = new()
        {
            // 결과 화면
            ["다음 맵으로"] = "to the next map",
            ["기지로"] = "to base",
            ["굶었다..."] = "Starved...",
            ["스테이지 {0}에서 전원 쓰러짐 — 처음부터 다시"] = "Everyone went down on stage {0} — back to the start",
            ["배불리 겨울을 났다!"] = "A well-fed winter!",
            ["스테이지 {0}개를 모두 넘어 가족이 굶지 않았어요 — 엔딩\n이번 런: 모은 식량 {1} · 상점에서 산 물건 {2}개"] = "You cleared all {0} stages and the family didn't starve — ending\nThis run: food gathered {1} · items bought {2}",
            ["스테이지 {0} 클리어!"] = "Stage {0} clear!",
            ["가족이 {0}만큼 먹었어요 — 남은 식량 {1}"] = "The family ate {0} — food left {1}",
            ["무사히 이사 완료!"] = "Made it home safe!",
            ["고양이 밥이 되었다..."] = "Became cat food...",
            ["전원 쥐구멍 집합 — 오늘 수확을 기지로 옮겼어요"] = "Everyone made it to the mousehole — today's haul is at base",
            ["전원 다운 — 이번 적립을 잃었어요 (누계는 그대로)"] = "Everyone went down — this run's stash is lost (total unchanged)",
            ["창고 적립"] = "Stashed in storeroom",
            ["쥐구멍 적립"] = "Stashed in mousehole",
            ["이번 수확"] = "This haul",
            ["잃은 적립"] = "Stash lost",
            [" · 구조 {0}"] = " · rescues {0}",
            ["{0:0}초 뒤 {1}"] = "{1} in {0:0}s",
            ["{0} {1} ({2}개)"] = "{0} {1} ({2})",
            ["들고 옴 {0}"] = "Carried {0}",
            ["이번에 새로 채운 도감은 없어요"] = "No new codex entries this time",
            // 로딩 팁 (LoadingTipsSO)
            ["웅크리면 발소리가 나지 않아요. 고양이 근처에선 기어가세요."] = "Crouching makes no footsteps. Crawl near cats.",
            ["계란은 던지면 깨져요. 굴려서 쥐구멍에 넣으세요."] = "Eggs break when thrown. Roll them into the mousehole.",
            ["쥐구멍에 넣은 물건은 안전해요. 전멸하면 이번 적립은 잃어요."] = "Items in the mousehole are safe. If everyone goes down, this run's stash is lost.",
            ["무거운 건 여럿이 같이 들면 움직여요."] = "Heavy things move when several rats lift together.",
            ["어두운 곳에선 고양이 시야가 절반이 돼요."] = "In the dark, cats see only half as far.",
            ["쓰러진 동료를 쥐구멍까지 옮기면 다시 일어나요."] = "Carry a downed teammate to the mousehole and they get back up.",
            ["끈끈이에 붙은 동료는 [E]를 길게 눌러 구출하세요."] = "Hold [E] to free a teammate stuck on a glue trap.",
            ["곧게 달리면 고양이보다 빨라요. 막다른 길만 피하세요."] = "Running straight outpaces cats. Just avoid dead ends.",
        };
    }
}

using RatGame.Core;
using RatGame.World;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 기지 패널 호스트 (규칙 3 "시스템→UI 직접 호출 금지" — 2026-09-24 고양이 52, docs/12 수용 기준 "UI 직접 참조 0건").
    /// 자판기·거울·도감은 EventBus로 "열어 달라"만 알리고, 패널 프리팹·생성·닫기는 여기(Hud 소속, 클라마다)가 맡는다.
    /// </summary>
    public class WorldPanelHost : MonoBehaviour
    {
        [SerializeField] private ShopPanel _shopPrefab;
        [SerializeField] private MirrorPanel _mirrorPrefab;
        [SerializeField] private CodexPanel _codexPrefab;
        [SerializeField] private StageShopPanel _stageShopPrefab; // 새 루프 목적지 상점 (고양이 65)

        private ShopPanel _shop;
        private MirrorPanel _mirror;
        private CodexPanel _codex;
        private StageShopPanel _stageShop;
        private Object _shopSource, _mirrorSource, _codexSource, _stageShopSource;

        private void OnEnable()
        {
            EventBus.WorldPanelRequested += OnRequested;
            EventBus.WorldPanelSourceGone += OnSourceGone;
            EventBus.ShopPurchaseResult += OnShopResult;
        }

        private void OnDisable()
        {
            EventBus.WorldPanelRequested -= OnRequested;
            EventBus.WorldPanelSourceGone -= OnSourceGone;
            EventBus.ShopPurchaseResult -= OnShopResult;
        }

        private void OnRequested(WorldPanelKind kind, Object source)
        {
            switch (kind)
            {
                case WorldPanelKind.Shop:
                    if (_shopPrefab == null || source is not VendingMachine machine) return;
                    if (_shop == null) _shop = Instantiate(_shopPrefab);
                    _shopSource = source;
                    _shop.Open(machine);
                    break;
                case WorldPanelKind.Mirror:
                    if (_mirrorPrefab == null) return;
                    if (_mirror == null) _mirror = Instantiate(_mirrorPrefab);
                    _mirrorSource = source;
                    _mirror.Open();
                    break;
                case WorldPanelKind.Codex:
                    if (_codexPrefab == null || source is not CodexBook book) return;
                    if (_codex == null) _codex = Instantiate(_codexPrefab);
                    _codexSource = source;
                    _codex.Open(book.Database);
                    break;
                case WorldPanelKind.StageShop:
                    if (_stageShopPrefab == null || source is not StageShopCounter counter) return;
                    if (_stageShop == null) _stageShop = Instantiate(_stageShopPrefab);
                    _stageShopSource = source;
                    _stageShop.Open(counter);
                    break;
            }
            Log.Dev($"패널 연출: {kind} 열림"); // 검증용
        }

        // 연 오브젝트가 사라지면(씬 전환) 그 패널도 없앤다 — 예전엔 오브젝트가 직접 Destroy했다
        private void OnSourceGone(Object source)
        {
            if (source == _shopSource && _shop != null) { Destroy(_shop.gameObject); _shop = null; }
            if (source == _mirrorSource && _mirror != null) { Destroy(_mirror.gameObject); _mirror = null; }
            if (source == _codexSource && _codex != null) { Destroy(_codex.gameObject); _codex = null; }
            if (source == _stageShopSource && _stageShop != null) { Destroy(_stageShop.gameObject); _stageShop = null; }
        }

        private void OnShopResult(bool ok, string message)
        {
            if (_shop != null && _shop.IsOpen) _shop.ShowMessage(ok, message);
            if (_stageShop != null && _stageShop.IsOpen) _stageShop.ShowMessage(ok, message);
        }

#if UNITY_EDITOR
        public void EditorSetup(ShopPanel shop, MirrorPanel mirror, CodexPanel codex) { _shopPrefab = shop; _mirrorPrefab = mirror; _codexPrefab = codex; }
        public void EditorSetupStageShop(StageShopPanel panel) => _stageShopPrefab = panel;
#endif
    }
}

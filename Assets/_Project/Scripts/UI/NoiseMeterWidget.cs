using RatGame.Core;
using RatGame.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 내 발소리 크기 (고양이 170, docs/06). 호스트의 발소리 판단(PlayerNoiseEmitter)을 내 클라에서 같은 수치로 다시 계산해 보여 줄 뿐 — 동기화 없음.
    /// 조용(웅크림·멈춤·공중) / 발소리 1m(걷기) / 쿵쿵 3m(달리기) / 울림 5m(배관 속 달리기, 입구에서).
    /// </summary>
    public class NoiseMeterWidget : MonoBehaviour
    {
        [SerializeField] private BalanceConfigSO _balance;
        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _box;
        [SerializeField] private TMP_Text _scentLabel;   // 위 줄 "냄새 · 치즈" (고양이 174)
        [SerializeField] private GameObject _scentBox;

        private const float ScentShowSeconds = 2f;
        private float _scentUntil;
        private string _scentText = "";

        private void OnEnable() => EventBus.ScentLeft += OnScentLeft;
        private void OnDisable() => EventBus.ScentLeft -= OnScentLeft;

        // 강도로 이유를 가른다 — 치즈 60·젖은 발 40·찍힘 20 (PlayerScent)
        private void OnScentLeft(float strength)
        {
            _scentUntil = Time.unscaledTime + ScentShowSeconds;
            string text = Loc.T(_balance == null ? "냄새" : strength >= _balance.ScentEdible - 0.5f ? "냄새 · 치즈" : strength >= _balance.ScentWet - 0.5f ? "냄새 · 젖은 발" : "냄새 · 찍힘");
            if (text != _scentText) { _scentText = text; if (_scentLabel != null) _scentLabel.text = text; Log.Dev($"냄새 표시: {text}"); }
        }

        private enum Level { None, Quiet, Walk, Run, Echo }
        private Level _shown = Level.None;
        private Vector3 _lastPos;
        private float _speed;
        private float _baseScaleY = -1f;

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            var me = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (me == null || _balance == null) { if (_box.activeSelf) _box.SetActive(false); if (_scentBox != null && _scentBox.activeSelf) _scentBox.SetActive(false); _shown = Level.None; return; }
            if (!_box.activeSelf) _box.SetActive(true);
            bool scent = Time.unscaledTime < _scentUntil;
            if (_scentBox != null && _scentBox.activeSelf != scent)
            {
                _scentBox.SetActive(scent);
                if (scent && _theme != null) _scentLabel.color = _theme.GetColor(UiColorRole.Warning); // 켠 뒤에 — 테마 초기화가 덮지 않게
                if (!scent) { _scentText = ""; Log.Dev("냄새 표시: 끝"); }
            }

            Transform t = me.transform;
            if (_baseScaleY < 0f) { _baseScaleY = t.localScale.y; _lastPos = t.position; }
            Vector3 d = t.position - _lastPos; _lastPos = t.position;
            float inst = Time.deltaTime > 0f ? new Vector3(d.x, 0f, d.z).magnitude / Time.deltaTime : 0f;
            if (inst > 20f) inst = 0f; // 순간이동
            _speed = Mathf.Lerp(_speed, inst, 1f - Mathf.Exp(-10f * Time.deltaTime));

            // 호스트 발소리와 같은 판단 (PlayerNoiseEmitter.FixedUpdate)
            bool grounded = Physics.Raycast(t.position, Vector3.down, 1f * t.localScale.y + 0.35f, ~LayerMask.GetMask("Player", "Ragdoll"), QueryTriggerInteraction.Ignore);
            bool crouching = t.localScale.y < _baseScaleY * 0.75f;
            Level level;
            if (!grounded || crouching || _speed < 1f) level = Level.Quiet;
            else if (_speed > (_balance.WalkSpeed + _balance.SprintSpeed) * 0.5f) level = World.PipeEcho.Above(t.position) != null ? Level.Echo : Level.Run;
            else level = Level.Walk;
            if (level == _shown) return;
            _shown = level;

            float loud = level == Level.Walk ? _balance.FootstepWalkLoudness : level == Level.Run ? _balance.FootstepRunLoudness : level == Level.Echo ? _balance.FootstepRunLoudness * _balance.PipeEchoMultiplier : 0f;
            int meters = Mathf.RoundToInt(loud / 100f * _balance.MaxNoiseRadius);
            _label.text = level switch
            {
                Level.Quiet => Loc.T("소리 · 조용"),
                Level.Walk => Loc.F("소리 · 발소리 {0}m", Mathf.Max(1, meters)),
                Level.Run => Loc.F("소리 · 쿵쿵 {0}m", meters),
                _ => Loc.F("소리 · 울림 {0}m", meters),
            };
            if (_theme != null) _label.color = _theme.GetColor(level == Level.Quiet ? UiColorRole.TextMuted : level == Level.Walk ? UiColorRole.Text : UiColorRole.Warning);
            Log.Dev($"소리 표시: {_label.text}"); // 검증용
        }

#if UNITY_EDITOR
        public void EditorSetup(BalanceConfigSO balance, UiThemeSO theme, TMP_Text label, GameObject box, TMP_Text scentLabel, GameObject scentBox)
        { _balance = balance; _theme = theme; _label = label; _box = box; _scentLabel = scentLabel; _scentBox = scentBox; }
#endif
    }
}

using RatGame.Core;
using RatGame.AI;
using RatGame.Data;
using RatGame.Player;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 숨은 동안의 화면 (design/cat-ideas/14, docs/12): 어두운 테두리 + "숨는 중 · [E] 나가기" + 심장 박동.
    /// 박동은 가장 가까운 고양이 거리에 비례 — 고양이가 코앞이면 빠르고 크게. 화면 펄스와 같은 박자로 심장 소리(고양이 252, 2D·효과음 볼륨).
    /// 내 PlayerCondition·CatBrain 위치만 읽는다 (규칙 3). 결과 화면과 무관(숨은 채로 결과가 뜨면 결과가 위).
    /// </summary>
    public class HiddenOverlayWidget : MonoBehaviour
    {
        private const float PollSeconds = 0.25f;
        private const float FarDistance = 12f;   // 이 밖이면 느린 박동
        private const float NearDistance = 2f;   // 이 안이면 최대

        [SerializeField] private UiThemeSO _theme;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _heart;
        [SerializeField] private TMP_Text _text;

        private PlayerCondition _condition;
        private CatBrain[] _cats;
        private float _nextPoll;
        private float _beatPhase;
        private float _nearest = float.MaxValue;
        private GrayboxAudioSO _audio;
        private AudioSource _heartSource;
        private int _lastBeat = int.MinValue;

        public bool IsShowing => _group != null && _group.alpha > 0.01f;
        public float NearestCatDistance => _nearest;
        private int _beats; // 시험용 — 숨은 동안 뛴 횟수

        public void Bind(PlayerCondition condition) => _condition = condition;

        private void Awake()
        {
            if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false; }
            _audio = Resources.Load<GrayboxAudioSO>("GrayboxAudio");
            _heartSource = gameObject.AddComponent<AudioSource>();
            _heartSource.playOnAwake = false;
            _heartSource.spatialBlend = 0f;
        }

        private void Update()
        {
            bool hidden = _condition != null && _condition.State.Value == ConditionState.Hidden;
            if (_group != null)
            {
                float target = hidden ? 1f : 0f;
                _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * (hidden ? 4f : 6f));
            }
            if (!hidden) { _lastBeat = int.MinValue; return; }

            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollSeconds;
                if (_cats == null || _cats.Length == 0) _cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None);
                _nearest = float.MaxValue;
                foreach (var cat in _cats)
                    if (cat != null) _nearest = Mathf.Min(_nearest, Vector3.Distance(cat.transform.position, _condition.transform.position));
            }

            // 거리 → 박동수 1~3Hz, 크기 1.05~1.35
            float t = 1f - Mathf.InverseLerp(NearDistance, FarDistance, _nearest);
            float bpm = Mathf.Lerp(1f, 3f, t);
            _beatPhase += Time.unscaledDeltaTime * bpm;
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_beatPhase * Mathf.PI * 2f)), 6f);
            // 펄스 꼭대기(한 바퀴의 0.25) 조금 앞에서 소리 — 화면이 부풀 때 "쿵"
            int beatIndex = Mathf.FloorToInt(_beatPhase - 0.2f);
            if (beatIndex != _lastBeat)
            {
                if (_lastBeat != int.MinValue && _audio != null)
                    _heartSource.PlayOneShot(_audio.HeartbeatClip, _audio.HeartbeatVolume(t) * Mathf.Clamp01(SettingsService.Current.SfxVolume));
                _lastBeat = beatIndex;
                _beats++;
            }
            if (_heart != null) _heart.localScale = Vector3.one * Mathf.Lerp(1f, Mathf.Lerp(1.05f, 1.35f, t), beat);
            if (_text != null)
            {
                string s = Loc.T(t > 0.7f ? "숨죽여… 고양이가 코앞" : "숨는 중 · [E] 나가기 (소리 남)");
                if (_text.text != s) _text.text = s;
                if (_theme != null)
                {
                    Color c = _theme.GetColor(t > 0.7f ? UiColorRole.DangerText : UiColorRole.Text);
                    if (_text.color != c) _text.color = c;
                }
            }
        }
    }
}

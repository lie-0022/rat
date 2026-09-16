using UnityEngine;

namespace RatGame.Data
{
    /// <summary>로딩 화면 팁 문구 (docs/12 화면 플로우 "로딩(팁 문구)"). 번역 작업 때 LocalizationTable로 옮긴다.</summary>
    [CreateAssetMenu(fileName = "LoadingTips", menuName = "RatGame/UI/Loading Tips")]
    public class LoadingTipsSO : ScriptableObject
    {
        [SerializeField, TextArea] private string[] _tips = System.Array.Empty<string>();

        public int Count => _tips.Length;

        /// <summary>직전과 다른 팁을 고른다 (2개 이상일 때). 없으면 -1.</summary>
        public int PickIndex(int avoid)
        {
            if (_tips.Length == 0) return -1;
            if (_tips.Length == 1) return 0;
            int i = Random.Range(0, _tips.Length - 1);
            return i >= avoid && avoid >= 0 ? i + 1 : i;
        }

        public string Get(int index) => index >= 0 && index < _tips.Length ? _tips[index] : "";

#if UNITY_EDITOR
        public void EditorSetTips(string[] tips) => _tips = tips;
#endif
    }
}

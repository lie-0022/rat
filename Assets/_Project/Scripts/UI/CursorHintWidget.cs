using RatGame.Core;
using TMPro;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 커서가 풀려 있고 메뉴 패널도 없을 때 "클릭하면 다시 조작" 안내 (HUD).
    /// 에디터 Game 뷰는 Esc에 커서 잠금을 풀고 클릭 전엔 코드로 다시 잠글 수 없다 — 멈춘 것처럼 보이지 않게 알려준다.
    /// 재잠금 자체는 PlayerCameraRig의 화면 클릭 처리.
    /// </summary>
    public class CursorHintWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _box;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private string _message = "화면을 클릭하면 다시 조작";

        private void Update()
        {
            bool show = Cursor.lockState != CursorLockMode.Locked && !InputFocus.IsUiOpen && Application.isFocused;
            if (_box.activeSelf != show) _box.SetActive(show);
            if (show && _text.text != _message) _text.text = _message;
        }
    }
}

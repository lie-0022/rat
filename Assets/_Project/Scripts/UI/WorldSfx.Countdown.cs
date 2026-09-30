using RatGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 출발·귀환 카운트다운 소리 (고양이 276) — RunBar가 보여 주는 같은 NV(DeparturePad.DepartAt·RunManager.ReturnAt, 서버 시각)를 읽어
    /// 남은 초가 줄 때마다 삑, 0이 되면 삐익. 화면을 안 봐도 "곧 떠난다"를 안다. 취소되면(값 0) 조용히 멈춘다.
    /// </summary>
    public partial class WorldSfx
    {
        private World.DeparturePad _pad;
        private float _nextPadScan;
        private int _departLeft = -1, _returnLeft = -1;

        private void UpdateCountdowns()
        {
            var nm = NetworkManager.Singleton;
            if (_audio == null || nm == null || !nm.IsListening) { _departLeft = _returnLeft = -1; return; }
            if (Time.unscaledTime >= _nextPadScan) { _nextPadScan = Time.unscaledTime + 1f; _pad = FindAnyObjectByType<World.DeparturePad>(); }
            double now = nm.ServerTime.Time;
            _departLeft = Tick(_pad != null && _pad.IsSpawned ? _pad.DepartAt.Value : 0, now, _departLeft, "출발");
            var run = Run.RunManager.Instance;
            _returnLeft = Tick(run != null && run.IsSpawned ? run.ReturnAt.Value : 0, now, _returnLeft, "귀환");
        }

        // 남은 초(올림)가 바뀔 때마다 — 끝나는 순간엔 한 번 높게
        private int Tick(double endsAt, double now, int last, string what)
        {
            if (endsAt <= 0) return -1;
            double left = endsAt - now;
            int secs = left > 0 ? Mathf.CeilToInt((float)left) : 0;
            if (secs == last) return last;
            if (last < 0 && secs == 0) return 0; // 늦게 봤는데 이미 끝남 — 조용히
            bool go = secs == 0;
            _ui.PlayOneShot(go ? _audio.CountdownGo : _audio.CountdownTick, _audio.CountdownVolume * Sfx);
            if (go) Log.Dev($"카운트다운 소리: {what} 삐익");
            return secs;
        }
    }
}

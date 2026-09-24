using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace RatGame.Run
{
    /// <summary>
    /// "지금 접속해 있는 전원이 씬 로드를 끝냈다"를 알린다 (고양이 121). NGO OnLoadEventCompleted는 로드를 시작할 때 접속해 있던
    /// 클라를 기다려서, 반쯤 붙었다 끊긴 유령 연결이 있으면 LoadSceneTimeOut(120초)까지 멈춘다. 여기선 클라별 로드 완료를 모으고
    /// 누가 끊길 때마다 다시 보므로, 유령이 정리되는(유령 정리 20초·전송 끊김 30초) 즉시 넘어간다. 둘 중 먼저 오는 쪽으로 한 번만.
    /// </summary>
    public sealed class SceneLoadGate
    {
        private readonly NetworkManager _nm;
        private readonly string _scene;
        private readonly Action _onReady;
        private readonly HashSet<ulong> _loaded = new();
        private bool _done;

        public SceneLoadGate(NetworkManager nm, string sceneName, Action onReady)
        {
            _nm = nm; _scene = sceneName; _onReady = onReady;
            _nm.SceneManager.OnLoadComplete += OnLoadComplete;
            _nm.SceneManager.OnLoadEventCompleted += OnEventCompleted;
            _nm.OnClientDisconnectCallback += OnDisconnect;
        }

        public void Cancel()
        {
            if (_done) return;
            _done = true;
            if (_nm == null) return;
            if (_nm.SceneManager != null)
            {
                _nm.SceneManager.OnLoadComplete -= OnLoadComplete;
                _nm.SceneManager.OnLoadEventCompleted -= OnEventCompleted;
            }
            _nm.OnClientDisconnectCallback -= OnDisconnect;
        }

        private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
        {
            if (sceneName != _scene) return;
            _loaded.Add(clientId);
            Check();
        }

        private void OnEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == _scene) Fire();
        }

        private void OnDisconnect(ulong clientId) => Check();

        private void Check()
        {
            if (_done) return;
            foreach (var id in _nm.ConnectedClientsIds) if (!_loaded.Contains(id)) return;
            Fire();
        }

        private void Fire()
        {
            if (_done) return;
            Cancel();
            _onReady();
        }
    }
}

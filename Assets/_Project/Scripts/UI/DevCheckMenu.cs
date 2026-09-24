using System;
using System.Collections.Generic;
using RatGame.AI;
using RatGame.Core;
using RatGame.Player;
using RatGame.Run;
using RatGame.World;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.UI
{
    /// <summary>
    /// 개발용 확인 메뉴 (F4, 호스트 전용, 개발 빌드·에디터만 — 2026-09-24). 사용자가 production/user-checklist.md를 직접 확인할 때
    /// 무작위로 오는 사건을 기다리거나 맵을 헤매지 않게: 집주인 이벤트 바로 켜기 · 기능 자리로 순간이동 · 고양이 불러오기 · 나 쓰러뜨리기/살리기.
    /// 씬·프리팹을 건드리지 않게 스스로 생긴다. 판정은 전부 기존 호스트 API를 부른다(새 동기화 없음).
    /// </summary>
    public class DevCheckMenu : MonoBehaviour
    {
        private bool _open;
        private Vector2 _scroll;
        private GUIStyle _box;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("DevCheckMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCheckMenu>();
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f4Key.wasPressedThisFrame) SetOpen(!_open);
            else if (_open && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (open) InputFocus.PanelOpened(); else InputFocus.PanelClosed(false); // 커서를 풀어 버튼을 누르게
        }

        private void OnDisable() { if (_open) SetOpen(false); }

        private void OnGUI()
        {
            if (!_open) return;
            var nm = NetworkManager.Singleton;
            const float w = 320f;
            var area = new Rect(Screen.width - w - 12f, 12f, w, Screen.height - 24f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 8f, w - 16f, area.height - 16f));
            GUILayout.Label("<b>개발용 확인 메뉴</b> (F4 / Esc 닫기)", Rich());
            if (nm == null || !nm.IsListening || !nm.IsHost) { GUILayout.Label("호스트에서만 쓸 수 있어요."); GUILayout.EndArea(); return; }
            _scroll = GUILayout.BeginScrollView(_scroll);

            var run = RunManager.Instance;
            var house = run != null ? run.GetComponent<HouseEventDirector>() : null;
            GUILayout.Label("<b>집주인 이벤트</b>" + (house == null ? " — 스테이지에서만" : ""), Rich());
            if (house != null)
                foreach (HouseEventKind kind in Enum.GetValues(typeof(HouseEventKind)))
                    if (GUILayout.Button(KindName(kind))) house.ServerTrigger(kind);

            GUILayout.Space(6);
            GUILayout.Label("<b>고양이</b>", Rich());
            var cats = FindObjectsByType<CatBrain>(FindObjectsSortMode.None);
            if (cats.Length == 0) GUILayout.Label("이 씬엔 고양이 없음");
            foreach (var cat in cats)
                if (GUILayout.Button($"{cat.name} ({cat.State.Value}) → 내 앞 4m")) CatToMe(cat);

            GUILayout.Space(6);
            GUILayout.Label("<b>나</b>", Rich());
            var me = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (me != null)
            {
                var cond = me.GetComponent<PlayerCondition>();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("쓰러지기")) cond.ServerSetState(ConditionState.Downed);
                if (GUILayout.Button("살리기") && run != null) run.ServerRevive(me.OwnerClientId);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>여기로 가기</b>", Rich());
            foreach (var (label, target) in Targets())
                if (GUILayout.Button(label)) GoTo(target);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private GUIStyle Rich()
        {
            if (_box == null) _box = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return _box;
        }

        private static string KindName(HouseEventKind kind) => kind switch
        {
            HouseEventKind.CallAway => "부르기 (고양이 나감)",
            HouseEventKind.Feeding => "밥 시간",
            HouseEventKind.Doorbell => "초인종",
            HouseEventKind.Vacuum => "로봇청소기",
            HouseEventKind.TV => "TV 켜기",
            HouseEventKind.LightOn => "불 켜기 (어둠 구역)",
            HouseEventKind.Window => "창문 바람",
            _ => kind.ToString(),
        };

        // 씬에 있는 기능 자리 — 있는 것만 버튼으로
        private static IEnumerable<(string, Transform)> Targets()
        {
            foreach (var (label, t) in new (string, Type)[]
                     {
                         ("쥐구멍", typeof(DepositZone)), ("TV", typeof(TvSet)), ("어둠 구역", typeof(LightZone)), ("창문", typeof(WindowWind)),
                         ("흔들리는 끈", typeof(DanglingString)), ("후추통", typeof(PepperShaker)), ("레이저 포인터", typeof(LaserPointer)),
                         ("창고방 문", typeof(RoomDoor)), ("초인종", typeof(Doorbell)), ("물그릇", typeof(WaterBowl)), ("치즈(먹기)", typeof(EdibleItem)),
                         ("숨을 곳", typeof(HideSpot)), ("함정", typeof(TrapBase)), ("목적지 게시판", typeof(StageBoard)), ("거울", typeof(Mirror)),
                     })
            {
                var found = FindAnyObjectByType(t) as Component;
                if (found != null) yield return (label, found.transform);
            }
            foreach (var spot in FindObjectsByType<CatSpot>(FindObjectsSortMode.None))
                if (spot.Type == CatSpotType.Perch || spot.Type == CatSpotType.Bed)
                    yield return ((spot.Type == CatSpotType.Perch ? "선반(고양이 관찰대) " : "고양이 침대 ") + spot.name, spot.transform);
        }

        private static void GoTo(Transform target)
        {
            var nm = NetworkManager.Singleton;
            var me = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (me == null) return;
            Vector3 away = me.transform.position - target.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            Vector3 pos = target.position + away.normalized * 1.6f;
            pos.y = 0.7f; // 바닥 위 (맵이 전부 y=0 바닥)
            Vector3 look = target.position - pos; look.y = 0f;
            var rpc = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { me.OwnerClientId } } };
            var pc = me.GetComponent<PlayerController>();
            pc.TeleportClientRpc(pos, Quaternion.LookRotation(look.normalized), rpc);
            pc.FaceClientRpc(Quaternion.LookRotation(look.normalized).eulerAngles.y, rpc); // 대상을 보고 서게
        }

        private static void CatToMe(CatBrain cat)
        {
            var me = NetworkManager.Singleton.LocalClient.PlayerObject;
            Vector3 f = me.transform.forward; f.y = 0f;
            Vector3 p = me.transform.position + f.normalized * 4f;
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) p = hit.position;
            var agent = cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(p);
            Log.Dev($"개발 메뉴: {cat.name} → {p:F1}");
        }
    }
}

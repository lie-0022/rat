using System.Collections.Generic;
using RatGame.Player;
using RatGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RatGame.EditorTools
{
    /// <summary>
    /// 끈끈이 구출 — 실제 E 키 홀드 (고양이 226, 고양이 설계 요약의 오랜 "미검증: 실제 E 키 홀드 입력").
    /// 2인 창고: 클라를 끈끈이(Trapped)로 → 호스트가 옆에서 바라보고 ① E를 짧게(0.4초) — 풀리면 안 됨 ② E를 끝까지 누르고 있기 — 풀려야 함.
    /// **유니티 창에 초점이 있어야 통과** — 초점이 없으면 키보드 입력이 액션까지 안 간다(마우스 입력 시험은 됨).
    /// </summary>
    public static partial class PlayTests
    {
        [MenuItem("Tools/RatGame/Test/Glue Rescue E Hold 2P (build client)")]
        private static void ArmGlue() => Arm("glue2p");

        private static bool _glueDevInput;

        private static void PressE(bool down)
        {
            if (_glueDevInput) PlayerInteractor.DevForcedInteract = down;
            else InputSystem.QueueStateEvent(Keyboard.current, down ? new KeyboardState(Key.E) : new KeyboardState());
        }

        private static List<Step> GlueSteps()
        {
            var steps = new List<Step>(ClientWarehouse("glue-client.log"));
            steps.AddRange(new List<Step>
            {
                new Step { Name = "끈끈이", Wait = 3f, Act = () =>
                {
                    // 유니티 창에 초점이 없으면 키보드가 입력 액션까지 안 간다("E키 True · 액션 False", 고양이 226).
                    // 226에선 두 설정(게임 창 초점 규칙 + 배경 동작)을 같이 바꿔 키 상태까지 막혔다 — 게임 창 초점 규칙 하나만 시험 동안 바꾼다 (고양이 235).
                    // 에셋 없는 메모리 설정이라 끝나면 되돌림
                    var settings = InputSystem.settings;
                    var behavior = settings.editorInputBehaviorInPlayMode;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    var before = OnDone;
                    OnDone = () => { settings.editorInputBehaviorInPlayMode = behavior; before?.Invoke(); };
                    var zone = Object.FindFirstObjectByType<DepositZone>().Area.bounds;
                    TeleportClient(new Vector3(zone.center.x, zone.min.y + 0.6f, zone.center.z - 6f), 0f);
                } },
                new Step { Name = "끈끈이 붙음", Wait = 1f, Act = () => _client.GetComponent<PlayerCondition>().ServerSetState(ConditionState.Trapped),
                    Check = () => null },
                // 동료 앞 0.9m에서 바라보기 — 클라 순간이동이 늦게 도착하면 옛 자리 앞에 서게 된다(첫 판 실패).
                // E 대상 미리 보기(고양이 187)가 "구출하기"가 될 때까지 지금 자리 기준으로 다시 선다
                new Step { Name = "옆에 서기", Wait = 0.5f, Ready = () =>
                {
                    var focus = typeof(PlayerInteractor).GetProperty("FocusPromptText").GetValue(Me().GetComponent<PlayerInteractor>()) as string;
                    if (focus != null && focus.Contains(RatGame.Core.Loc.T("구출하기 — {0}").Split('{')[0].Trim())) return true;
                    if (EditorApplication.timeSinceStartup - _stepAt > 6) return true;
                    Put(_client.transform.position + new Vector3(0f, 0f, -0.9f));
                    Me().GetComponent<PlayerCameraRig>().SnapYaw(0f);
                    return false;
                }, Check = () =>
                {
                    var focus = typeof(PlayerInteractor).GetProperty("FocusPromptText").GetValue(Me().GetComponent<PlayerInteractor>()) as string;
                    Report.Append($" | 대상 안내 \"{focus}\"");
                    return focus != null ? null : "동료를 대상으로 못 잡음";
                } },
                // 창 초점이 없으면 가짜 키가 버려진다(고양이 239) — 그땐 개발 입력(PlayerInteractor.DevForcedInteract)으로 눌러 구출 규칙은 그대로 본다 (고양이 332)
                new Step { Name = "짧게 누름", Wait = 0.5f, Act = () =>
                {
                    _glueDevInput = !UnityEditorInternal.InternalEditorUtility.isApplicationActive;
                    var before = OnDone;
                    OnDone = () => { PlayerInteractor.DevForcedInteract = false; before?.Invoke(); };
                    PressE(true);
                } },
                new Step { Name = "짧게 누름", Wait = 0.4f, Act = () => PressE(false),
                    Check = () => _client.GetComponent<PlayerCondition>().State.Value == ConditionState.Trapped ? null : "짧게 눌렀는데 풀림" },
                new Step { Name = "길게 누름", Wait = 0.5f, Act = () => PressE(true) },
                new Step { Name = "입력 상태", Wait = 0.3f, Check = () =>
                {
                    var pi = Me().GetComponent<PlayerInteractor>();
                    var ia = typeof(PlayerInteractor).GetField("_interactAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pi) as InputAction;
                    Report.Append($" | E키 {Keyboard.current.eKey.isPressed} · 액션 눌림 {ia?.IsPressed()} · 액션 켜짐 {ia?.enabled} · 창 초점 {UnityEditorInternal.InternalEditorUtility.isApplicationActive}");
                    if (_glueDevInput) { Report.Append(" · 창 초점 없음 → 개발 입력으로"); return null; }
                    // 초점 없는 에디터는 가상 키 입력을 버릴 때가 있다(Run All Full에서 3연속, 고양이 239) — 게임 탓이 아니니 건너뜀으로 보고
                    if (!Keyboard.current.eKey.isPressed && ia != null && !ia.IsPressed() && !UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                        _skip = "유니티 창 초점 없음 — 키 입력이 버려짐, 창을 누르고 다시";
                    return null;
                } },
                new Step { Name = "풀림", Ready = () => _skip != null ||
                    _client.GetComponent<PlayerCondition>().State.Value != ConditionState.Trapped || EditorApplication.timeSinceStartup - _stepAt > 5,
                    Check = () =>
                    {
                        float held = (float)(EditorApplication.timeSinceStartup - _stepAt);
                        PressE(false);
                        var st = _client.GetComponent<PlayerCondition>().State.Value;
                        if (_skip != null) return null;
                        Report.Append($" | 누른 시간 {held:0.0}초 → 클라 {st}");
                        return st == ConditionState.Active ? null : "5초 눌러도 안 풀림";
                    } },
            });
            return steps;
        }
    }
}

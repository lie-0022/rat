using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RatGame.Tests
{
    /// <summary>
    /// UI 프리팹 연결 (고양이 338) — 설정 "소리 자막" 줄이 Hud 안 일시정지의 덮어쓰기로만 들어가 **메인 메뉴 설정엔 없었다**(258 뒤로 줄곧).
    /// UI 프리팹의 게임 스크립트 필드는 모두 연결돼 있어야 한다(선택 필드가 없음). 비면 그 버튼·줄이 조용히 사라진다.
    /// 방·물건 프리팹은 선택 필드가 많아(CatSpawn·DarkZone…) 여기서 안 본다.
    /// </summary>
    public class UiWiringTests
    {
        [Test]
        public void UI_프리팹의_스크립트_필드가_모두_연결됨()
        {
            var empty = new List<string>();
            int fields = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("RatGame")) continue;
                    var it = new SerializedObject(mb).GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                        fields++;
                        if (it.objectReferenceValue == null)
                            empty.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} / {mb.name} / {mb.GetType().Name}.{it.name}");
                    }
                }
            }
            Assert.IsEmpty(empty, "비어 있는 UI 연결:\n" + string.Join("\n", empty));
            Assert.Greater(fields, 100);
        }
    }
}

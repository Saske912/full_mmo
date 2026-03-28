#if UNITY_EDITOR
using Mmo.Client.Unity;
using UnityEditor;
using UnityEngine;

namespace Mmo.Client.Editor
{
    static class MmoPlaygroundMenu
    {
        [MenuItem("GameObject/MMO/Playground Bootstrap", false, 10)]
        static void CreatePlaygroundBootstrap()
        {
            var go = new GameObject("MmoPlayground");
            go.AddComponent<MmoGameBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Create Mmo Playground");
            Selection.activeGameObject = go;
        }
    }
}
#endif

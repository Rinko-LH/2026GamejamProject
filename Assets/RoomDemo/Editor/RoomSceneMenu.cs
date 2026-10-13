using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomDemo.EditorTools
{
    public static class RoomSceneMenu
    {
        [MenuItem("Tools/Room/Build Scene In Current Scene")]
        static void BuildInCurrentScene()
        {
            RoomSceneBuilder.BuildScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Room/Build New Scene Asset")]
        static void BuildNewSceneAsset()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = RoomSceneBuilder.BuildScene();

            // 立刻按命名规则重刷一遍绘制层再存盘。
            // 否则 Unity 可能在序列化时改写 sortingOrder，导致地板盖住家具。
            root.GetComponent<RoomSortingFix>().Refresh();

            System.IO.Directory.CreateDirectory("Assets/RoomDemo/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/RoomDemo/Scenes/RoomDemo.unity");
            Debug.Log("[RoomDemo] 已生成场景 Assets/RoomDemo/Scenes/RoomDemo.unity");
        }
    }
}
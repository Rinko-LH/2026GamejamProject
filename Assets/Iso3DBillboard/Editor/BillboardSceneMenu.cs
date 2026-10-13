using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Iso3DBillboard.EditorTools
{
    public static class BillboardSceneMenu
    {
        [MenuItem("Tools/Billboard Test/Build 2.5D Scene")]
        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BillboardSceneBuilder.Build();
            System.IO.Directory.CreateDirectory("Assets/Iso3DBillboard/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Iso3DBillboard/Scenes/BillboardTest.unity");
            Debug.Log("[Billboard] 已生成 Assets/Iso3DBillboard/Scenes/BillboardTest.unity");
        }
    }
}
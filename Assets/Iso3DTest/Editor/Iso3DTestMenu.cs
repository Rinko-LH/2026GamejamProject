using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Iso3DTest.EditorTools
{
    public static class Iso3DTestMenu
    {
        [MenuItem("Tools/Iso3D Test/Build Verification Scene")]
        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Iso3DTestBuilder.Build();
            System.IO.Directory.CreateDirectory("Assets/Iso3DTest/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Iso3DTest/Scenes/Iso3DTest.unity");
            Debug.Log("[Iso3DTest] 已生成 Assets/Iso3DTest/Scenes/Iso3DTest.unity");
        }
    }
}
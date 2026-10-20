using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RoomDemo.EditorTools
{
    /// <summary>Integration check invoked across a real Play/Edit transition; uses its own additive scene.</summary>
    public static class RuntimeComponentParametersValidation
    {
        const string Folder = "Assets/Editor/RuntimeParameterKeeperTestData";
        const string ScenePath = Folder + "/Validation.unity";
        const string OriginalSceneKey = "RoomDemo.ParameterValidation.OriginalScene";

        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            if (RuntimeComponentParameters.Pending.Count != 0) throw new InvalidOperationException("Pending user snapshots must be applied first.");
            if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Test folder already exists; refusing to overwrite.");
            SessionState.SetString(OriginalSceneKey, SceneManager.GetActiveScene().path);
            AssetDatabase.CreateFolder("Assets/Editor", "RuntimeParameterKeeperTestData");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("ParameterKeeperValidation");
            SceneManager.MoveGameObjectToScene(root, scene);
            var target = new GameObject("ParameterKeeperTarget");
            target.transform.SetParent(root.transform);
            target.AddComponent<BoxCollider>();
            target.AddComponent<BoxCollider>();
            target.AddComponent<Rigidbody>().isKinematic = true;
            var joint = target.AddComponent<FixedJoint>();
            var a = new GameObject("ReferenceA");
            a.transform.SetParent(root.transform);
            var b = new GameObject("ReferenceB");
            b.transform.SetParent(root.transform);
            a.AddComponent<Rigidbody>().isKinematic = true;
            b.AddComponent<Rigidbody>().isKinematic = true;
            joint.connectedBody = a.GetComponent<Rigidbody>();
            var source = new GameObject("ParameterKeeperPrefab");
            source.AddComponent<BoxCollider>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/Test.prefab");
            Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "ParameterKeeperPrefab";
            instance.transform.SetParent(root.transform);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save fixture.");
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(SessionState.GetString(OriginalSceneKey, "")));
            Debug.Log("[运行时参数验证] 测试场景已创建，用户场景未修改。");
        }

        public static void CaptureRuntime()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
            var target = GameObject.Find("ParameterKeeperTarget");
            var colliders = target.GetComponents<BoxCollider>();
            colliders[0].size = new Vector3(2, 3, 4);
            Require(RuntimeComponentParameters.Capture(colliders[0]), "capture first collider");
            colliders[0].size = new Vector3(5, 6, 7);
            Require(RuntimeComponentParameters.Capture(colliders[0]), "replace snapshot");
            colliders[1].center = new Vector3(8, 9, 10);
            Require(RuntimeComponentParameters.Capture(colliders[1]), "capture second same-type component");
            target.GetComponent<Rigidbody>().mass = 7.5f;
            Require(RuntimeComponentParameters.Capture(target.GetComponent<Rigidbody>()), "capture native component");
            target.GetComponent<FixedJoint>().connectedBody = GameObject.Find("ReferenceB").GetComponent<Rigidbody>();
            Require(RuntimeComponentParameters.Capture(target.GetComponent<FixedJoint>()), "capture scene reference");
            target.name = "RenamedDuringPlay";
            target.transform.localPosition = new Vector3(3, 4, 5);
            Require(RuntimeComponentParameters.Capture(target.transform), "capture after rename");
            var prefab = GameObject.Find("ParameterKeeperPrefab").GetComponent<BoxCollider>();
            prefab.size = Vector3.one * 9;
            Require(RuntimeComponentParameters.Capture(prefab), "capture prefab instance");
            var temporary = new GameObject("TemporaryParameterKeeperTarget").AddComponent<BoxCollider>();
            Require(!RuntimeComponentParameters.Capture(temporary), "reject temporary component");
            Object.Destroy(temporary.gameObject);
            Require(RuntimeComponentParameters.Pending.Count == 6, "snapshot overwrite count");
            Debug.Log("[运行时参数验证] 已保存 6 个组件，等待退出运行自动恢复。");
        }

        public static void VerifyAndCleanup()
        {
            if (EditorApplication.isPlaying) throw new Exception("Requires Edit Mode.");
            var target = GameObject.Find("ParameterKeeperTarget");
            Require(target != null, "owner identity unchanged");
            var colliders = target.GetComponents<BoxCollider>();
            Require(colliders[0].size == new Vector3(5, 6, 7), "last saved snapshot wins");
            Require(colliders[1].center == new Vector3(8, 9, 10), "second same-type component");
            Require(Mathf.Approximately(target.GetComponent<Rigidbody>().mass, 7.5f), "native parameters");
            Require(target.GetComponent<FixedJoint>().connectedBody == GameObject.Find("ReferenceB").GetComponent<Rigidbody>(), "scene object reference");
            Require(target.transform.localPosition == new Vector3(3, 4, 5), "Transform parameters");
            Require(target.transform.parent.name == "ParameterKeeperValidation", "hierarchy unchanged");
            var prefab = GameObject.Find("ParameterKeeperPrefab").GetComponent<BoxCollider>();
            Require(prefab.size == Vector3.one * 9 && PrefabUtility.HasPrefabInstanceAnyOverrides(prefab.gameObject, false), "prefab instance override");
            Require(RuntimeComponentParameters.Pending.Count == 0, "applied snapshots cleared");
            Undo.PerformUndo();
            Require(colliders[0].size == Vector3.one, "Undo restores original parameters");
            Require(target.GetComponent<FixedJoint>().connectedBody == GameObject.Find("ReferenceA").GetComponent<Rigidbody>(), "Undo restores original reference");
            Cleanup();
            Debug.Log("[运行时参数验证] PASS：多组件、覆盖快照、场景引用、改名、Transform、Prefab 覆盖和 Undo 均通过，测试资产已清理。");
        }

        public static void Cleanup()
        {
            if (EditorApplication.isPlaying) throw new Exception("Requires Edit Mode.");
            foreach (var snapshot in RuntimeComponentParameters.Pending.Where(x =>
                x.label.StartsWith("ParameterKeeperTarget /") || x.label.StartsWith("RenamedDuringPlay /") ||
                x.label.StartsWith("ParameterKeeperPrefab /"))) RuntimeComponentParameters.Discard(snapshot.id);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(Folder);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("[运行时参数验证] FAIL: " + message);
        }

    }
}

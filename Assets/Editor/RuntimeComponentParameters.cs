using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RoomDemo.EditorTools
{
    /// <summary>Explicit, per-component Play Mode snapshots, restored with Undo on returning to Edit Mode.</summary>
    [InitializeOnLoad]
    public static class RuntimeComponentParameters
    {
        const string StateKey = "RoomDemo.RuntimeComponentParameters.v1";
        const string BaselineKey = StateKey + ".baseline";
        const string RuntimeKey = StateKey + ".runtime";
        const string AutoKey = StateKey + ".auto";
        const string ApplyQueuedKey = StateKey + ".applyQueued";
        static readonly HashSet<string> Infrastructure = new HashSet<string>
        {
            "m_GameObject", "m_Script", "m_CorrespondingSourceObject", "m_PrefabInstance",
            "m_PrefabAsset", "m_ObjectHideFlags", "m_EditorHideFlags"
        };

        [Serializable] public class Reference
        {
            public string path;
            public string id;
            public bool wasNull;
            public bool temporary;
        }
        [Serializable] public class Snapshot
        {
            public string id;
            public string type;
            public string label;
            public string json;
            public List<Reference> references = new List<Reference>();
        }
        [Serializable] class Snapshots { public List<Snapshot> items = new List<Snapshot>(); }
        [Serializable] class Identity { public string key; public string id; }
        [Serializable] class Identities { public List<Identity> items = new List<Identity>(); }

        public static bool AutoApply
        {
            get => SessionState.GetBool(AutoKey, true);
            set => SessionState.SetBool(AutoKey, value);
        }
        public static List<Snapshot> Pending => Load<Snapshots>(StateKey).items;

        static RuntimeComponentParameters()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += ApplyWhenReady;
        }

        static void ApplyWhenReady()
        {
            if (!SessionState.GetBool(ApplyQueuedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            SessionState.SetBool(ApplyQueuedKey, false);
            ApplyAll();
        }

        static T Load<T>(string key) where T : new()
        {
            string json = SessionState.GetString(key, "");
            return string.IsNullOrEmpty(json) ? new T() : JsonUtility.FromJson<T>(json);
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Capture saved-scene identities before Play Mode can rename or reparent objects.
                var baseline = new Identities();
                foreach (var obj in SceneObjects())
                {
                    string id = PersistentId(obj);
                    if (id != null) baseline.items.Add(new Identity { key = RuntimeIdentity(id), id = id });
                }
                SessionState.SetString(BaselineKey, JsonUtility.ToJson(baseline));
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var baseline = Load<Identities>(BaselineKey).items.ToDictionary(x => x.key, x => x.id);
                var runtime = new Identities();
                foreach (var obj in SceneObjects())
                {
                    string id = PersistentId(obj);
                    if (id != null && baseline.TryGetValue(RuntimeIdentity(id), out string originalId))
                        runtime.items.Add(new Identity { key = obj.GetInstanceID().ToString(), id = originalId });
                }
                SessionState.SetString(RuntimeKey, JsonUtility.ToJson(runtime));
            }
            else if (state == PlayModeStateChange.EnteredEditMode && AutoApply)
            {
                // SessionState survives an immediate script reload after leaving Play Mode.
                SessionState.SetBool(ApplyQueuedKey, true);
            }
        }

        static IEnumerable<Object> SceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return tr.gameObject;
                    foreach (var component in tr.GetComponents<Component>())
                        if (component != null) yield return component;
                }
            }
        }

        static string PersistentId(Object obj)
        {
            if (obj == null) return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return id.identifierType == 0 || id.targetObjectId == 0 ? null : id.ToString();
        }

        static string RuntimeIdentity(string text)
        {
            // Unity unpacks scene prefab instances for Play Mode. Their local file ID is
            // the source object's file ID XOR the prefab instance ID, with the sign bit cleared.
            if (!GlobalObjectId.TryParse(text, out var id) || id.targetPrefabId == 0) return text;
            ulong localId = (id.targetObjectId ^ id.targetPrefabId) & 0x7fffffffffffffffUL;
            return $"GlobalObjectId_V1-{id.identifierType}-{id.assetGUID}-{localId}-0";
        }

        static string SavedId(Object obj, Dictionary<string, string> runtime)
        {
            if (obj == null) return null;
            if (EditorUtility.IsPersistent(obj)) return PersistentId(obj);
            return runtime.TryGetValue(obj.GetInstanceID().ToString(), out string id) ? id : null;
        }

        static Object Resolve(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return GlobalObjectId.TryParse(text, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
        }

        static bool Excluded(string path, Component component)
        {
            string root = path.Split('.')[0];
            return Infrastructure.Contains(root) || (component is Transform &&
                (root == "m_Children" || root == "m_Father" || root == "m_RootOrder"));
        }

        [MenuItem("CONTEXT/Component/保存运行时参数（退出后保留）")]
        static void SaveMenu(MenuCommand command) => Capture(command.context as Component);

        [MenuItem("CONTEXT/Component/保存运行时参数（退出后保留）", true)]
        static bool CanSaveMenu(MenuCommand command) => EditorApplication.isPlaying && command.context is Component;

        [MenuItem("CONTEXT/Component/应用已保存参数")]
        static void ApplyMenu(MenuCommand command)
        {
            string id = PersistentId(command.context);
            var snapshot = Pending.Find(x => x.id == id);
            if (snapshot != null) Apply(snapshot);
        }

        [MenuItem("CONTEXT/Component/应用已保存参数", true)]
        static bool CanApplyMenu(MenuCommand command) => !EditorApplication.isPlayingOrWillChangePlaymode &&
            command.context is Component && Pending.Any(x => x.id == PersistentId(command.context));

        public static bool Capture(Component component)
        {
            if (!EditorApplication.isPlaying || component == null) return false;
            var runtime = Load<Identities>(RuntimeKey).items.ToDictionary(x => x.key, x => x.id);
            string id = SavedId(component, runtime);
            if (id == null || EditorUtility.IsPersistent(component))
            {
                Debug.LogWarning("[运行时参数] 只能保留进入运行前已存在于已保存场景中的组件。临时对象、新增组件和未保存场景不支持自动恢复。", component);
                return false;
            }
            try
            {
                var json = JObject.Parse(EditorJsonUtility.ToJson(component));
                foreach (var name in Infrastructure) json.Remove(name);
                if (component is Transform)
                {
                    json.Remove("m_Children");
                    json.Remove("m_Father");
                    json.Remove("m_RootOrder");
                }
                var snapshot = new Snapshot
                {
                    id = id, type = component.GetType().AssemblyQualifiedName,
                    label = component.gameObject.name + " / " + component.GetType().Name,
                    json = json.ToString(Newtonsoft.Json.Formatting.None)
                };
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (Excluded(property.propertyPath, component) || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue;
                        string referenceId = SavedId(value, runtime);
                        snapshot.references.Add(new Reference
                        {
                            path = property.propertyPath, id = referenceId,
                            wasNull = value == null, temporary = value != null && referenceId == null
                        });
                    }
                }
                var pending = Pending;
                pending.RemoveAll(x => x.id == id);
                pending.Add(snapshot);
                Store(pending);
                int temporary = snapshot.references.Count(x => x.temporary);
                Debug.Log($"[运行时参数] 已保存 {snapshot.label} 的当前快照。退出运行后{(AutoApply ? "自动恢复" : "可手动应用")}。" +
                    (temporary > 0 ? $" {temporary} 个临时对象引用无法保留，将保留编辑模式的原引用。" : ""), component);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[运行时参数] 保存失败：" + e.Message, component);
                return false;
            }
        }

        static void Store(List<Snapshot> snapshots) => SessionState.SetString(StateKey, JsonUtility.ToJson(new Snapshots { items = snapshots }));
        public static void Discard(string id) => Store(Pending.Where(x => x.id != id).ToList());

        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("恢复运行时组件参数");
            foreach (var snapshot in Pending) Apply(snapshot);
            Undo.CollapseUndoOperations(group);
        }

        public static bool Apply(Snapshot snapshot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            var component = Resolve(snapshot.id) as Component;
            if (component == null || component.GetType().AssemblyQualifiedName != snapshot.type)
            {
                Debug.LogWarning("[运行时参数] 找不到原组件，请打开原场景后重试：" + snapshot.label);
                return false;
            }
            string backup = EditorJsonUtility.ToJson(component);
            try
            {
                var originalReferences = new Dictionary<string, Object>();
                using (var serialized = new SerializedObject(component))
                    foreach (var reference in snapshot.references)
                        originalReferences[reference.path] = serialized.FindProperty(reference.path)?.objectReferenceValue;
                Undo.RegisterCompleteObjectUndo(component, "恢复运行时组件参数");
                EditorJsonUtility.FromJsonOverwrite(snapshot.json, component);
                using (var serialized = new SerializedObject(component))
                {
                    foreach (var reference in snapshot.references)
                    {
                        var property = serialized.FindProperty(reference.path);
                        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object value = reference.wasNull ? null : Resolve(reference.id);
                        if (reference.temporary || (!reference.wasNull && value == null))
                        {
                            originalReferences.TryGetValue(reference.path, out value);
                            Debug.LogWarning($"[运行时参数] {snapshot.label}.{reference.path} 的引用无法恢复，已保留原引用。", component);
                        }
                        property.objectReferenceValue = value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                Discard(snapshot.id);
                Debug.Log("[运行时参数] 已恢复 " + snapshot.label + "。可 Undo 撤销；请 Ctrl+S 保存场景。", component);
                return true;
            }
            catch (Exception e)
            {
                EditorJsonUtility.FromJsonOverwrite(backup, component);
                Debug.LogError("[运行时参数] 应用失败，已还原组件；快照保留以便重试：" + e.Message, component);
                return false;
            }
        }
    }

    public class RuntimeComponentParametersWindow : EditorWindow
    {
        Vector2 _scroll;
        [MenuItem("Tools/运行时组件参数")]
        public static void Open() => GetWindow<RuntimeComponentParametersWindow>("运行时组件参数");
        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            EditorGUILayout.HelpBox("运行中：组件标题栏右键 → 保存运行时参数（退出后保留）。\n保存的是点击当时的快照；再次保存会覆盖。支持 Unity 可序列化参数。", MessageType.Info);
            RuntimeComponentParameters.AutoApply = EditorGUILayout.ToggleLeft("退出运行后自动应用", RuntimeComponentParameters.AutoApply);
            EditorGUILayout.HelpBox("应用支持 Undo，随后请 Ctrl+S 保存场景。快照仅在本次编辑器会话内保留（支持脚本重载）。临时对象和运行时新增组件无法恢复。", MessageType.None);
            var pending = RuntimeComponentParameters.Pending;
            EditorGUILayout.LabelField("待恢复组件：" + pending.Count, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || pending.Count == 0))
                if (GUILayout.Button("应用全部已保存参数")) RuntimeComponentParameters.ApplyAll();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var snapshot in pending)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(snapshot.label);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("应用", GUILayout.Width(48))) RuntimeComponentParameters.Apply(snapshot);
                if (GUILayout.Button("丢弃", GUILayout.Width(48))) RuntimeComponentParameters.Discard(snapshot.id);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

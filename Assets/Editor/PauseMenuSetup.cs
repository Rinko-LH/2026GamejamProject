using System;
using System.Linq;
using RoomDemo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RoomDemo.EditorTools
{
    public static class PauseMenuSetup
    {
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Room Demo/创建暂停菜单")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var mainScene = SceneManager.GetSceneByPath("Assets/Scenes/MainScene.unity");
            if (!mainScene.isLoaded) throw new InvalidOperationException("请先打开 MainScene。");
            var uiRoot = mainScene.GetRootGameObjects().First(x => x.name == "UIRoot");
            if (uiRoot.transform.Find("PauseMenu") != null) throw new InvalidOperationException("暂停菜单已经存在，不会重复创建。");
            var originalActive = SceneManager.GetActiveScene();
            var menuScene = SceneManager.GetSceneByPath(MainMenuPath);
            bool openedMenu = !menuScene.isLoaded;
            GameObject root = null;
            try
            {
                if (openedMenu) menuScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive);
                var template = menuScene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Button>(true)).First(x => x.name == "StartButton");
                root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(PauseMenuController));
                root.transform.SetParent(uiRoot.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "创建暂停菜单");
                Stretch((RectTransform)root.transform);
                var canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
                var panel = new GameObject("Overlay", typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);
                Stretch((RectTransform)panel.transform);
                var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
                dimmer.transform.SetParent(panel.transform, false);
                Stretch((RectTransform)dimmer.transform);
                dimmer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
                dimmer.GetComponent<Image>().raycastTarget = true;
                var controller = root.GetComponent<PauseMenuController>();
                var resume = CloneButton(template, panel.transform, "ResumeButton", "返回游戏", 70f);
                var mainMenu = CloneButton(template, panel.transform, "MainMenuButton", "返回主菜单", -70f);
                UnityEventTools.AddPersistentListener(resume.onClick, controller.ResumeGame);
                UnityEventTools.AddPersistentListener(mainMenu.onClick, controller.ReturnToMainMenu);
                var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = mainMenu, selectOnDown = mainMenu };
                resume.navigation = nav;
                nav.selectOnUp = nav.selectOnDown = resume;
                mainMenu.navigation = nav;
                using (var serialized = new SerializedObject(controller))
                {
                    serialized.FindProperty("panel").objectReferenceValue = panel;
                    serialized.FindProperty("resumeButton").objectReferenceValue = resume;
                    serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                // 场景中保留完整 UI，调整布局时可手动启用 Overlay 预览。
                panel.SetActive(false);
                EditorSceneManager.MarkSceneDirty(mainScene);
                Selection.activeGameObject = root;
                Debug.Log("[暂停菜单] 已按主菜单按钮样式创建。请保存 MainScene。", root);
            }
            catch
            {
                if (root != null) Undo.DestroyObjectImmediate(root);
                throw;
            }
            finally
            {
                if (openedMenu && menuScene.IsValid()) EditorSceneManager.CloseScene(menuScene, true);
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            }
        }

        static Button CloneButton(Button template, Transform parent, string name, string text, float y)
        {
            var clone = Object.Instantiate(template.gameObject, parent, false);
            clone.name = name;
            var rect = (RectTransform)clone.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label == null) throw new InvalidOperationException("主菜单按钮缺少 TMP 文本。");
            label.text = text;
            label.raycastTarget = false;
            clone.SetActive(true);
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}

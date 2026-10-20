using System;
using System.Linq;
using RoomDemo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoomDemo.EditorTools
{
    public static class PaintingNavigationSetup
    {
        [MenuItem("Tools/Room Demo/创建画板翻页按钮")]
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/MainScene.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("请先打开 MainScene。");
            var roots = scene.GetRootGameObjects();
            var uiRoot = roots.First(x => x.name == "UIRoot");
            if (uiRoot.transform.Find("PaintingNavigation") != null)
                throw new InvalidOperationException("画板翻页按钮已存在，不会重复创建。");
            var board = roots.SelectMany(x => x.GetComponentsInChildren<DrawingBoardInteract>(true)).First();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/AlibabaPuHuiTi/AlibabaPuHuiTi-3-55-Regular SDF.asset");
            if (font == null) throw new InvalidOperationException("未找到中文字体。");
            var root = new GameObject("PaintingNavigation", typeof(RectTransform), typeof(Canvas),
                typeof(GraphicRaycaster), typeof(PaintingNavigationUI));
            root.transform.SetParent(uiRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "创建画板翻页按钮");
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1100;
            var controls = new GameObject("Controls", typeof(RectTransform));
            controls.transform.SetParent(root.transform, false);
            var rect = (RectTransform)controls.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(520f, 56f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            var previous = CreateButton(controls.transform, "PreviousButton", "上一张", -140f, font, board.ShowPreviousBoard);
            var next = CreateButton(controls.transform, "NextButton", "下一张", 140f, font, board.ShowNextBoard);
            var submit = CreateButton(controls.transform, "SubmitButton", "提交", 140f, font, board.SubmitPaintings);
            var ui = root.GetComponent<PaintingNavigationUI>();
            ui.board = board;
            ui.controls = controls;
            ui.previousButton = previous;
            ui.nextButton = next;
            ui.submitButton = submit;
            ui.Refresh();
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "已创建 UIRoot/PaintingNavigation，按钮位于画布下方。";
        }

        static Button CreateButton(Transform parent, string name, string text, float x, TMP_FontAsset font, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 56f);
            rect.anchoredPosition = new Vector2(x, 0f);
            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = new Color(0.90f, 0.83f, 0.69f);
            colors.highlightedColor = new Color(1f, 0.95f, 0.82f);
            colors.pressedColor = new Color(0.70f, 0.61f, 0.47f);
            colors.disabledColor = new Color(0.55f, 0.54f, 0.51f, 0.65f);
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.18f, 0.20f, 0.22f);
            tmp.raycastTarget = false;
            return button;
        }
    }
}

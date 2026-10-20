using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoomDemo.EditorTools
{
    /// <summary>将菜单按钮换成项目里的手绘素材，保留按钮对象与事件绑定。</summary>
    public static class MenuButtonArtworkSetup
    {
        static readonly string[] Labels = { "开始游戏", "玩法说明", "制作人员", "退出游戏", "返回游戏", "返回主菜单" };
        static readonly string[] ScenePaths = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/MainScene.unity" };

        [MenuItem("Tools/Room Demo/应用菜单按钮素材")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var sprites = Labels.ToDictionary(x => x, ImportButtonSprite);
            var originalScene = SceneManager.GetActiveScene();
            var report = new List<string>();
            foreach (string path in ScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var button in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Button>(true)))
                    {
                        var label = button.GetComponentInChildren<TMP_Text>(true);
                        string text = label != null ? label.text.Trim() : "";
                        if (!sprites.TryGetValue(text, out var sprite)) continue;
                        var image = button.GetComponent<Image>();
                        if (image == null) throw new InvalidOperationException(button.name + " 缺少 Image。");
                        Undo.RecordObjects(new UnityEngine.Object[] { image, button, label }, "替换菜单按钮素材");
                        image.sprite = sprite;
                        image.overrideSprite = null;
                        image.type = Image.Type.Simple;
                        image.preserveAspect = true;
                        image.color = Color.white;
                        image.raycastTarget = true;
                        button.targetGraphic = image;
                        button.transition = Selectable.Transition.ColorTint;
                        var colors = button.colors;
                        colors.normalColor = Color.white;
                        colors.highlightedColor = new Color(0.80f, 0.90f, 1f);
                        colors.pressedColor = new Color(0.62f, 0.72f, 0.82f);
                        colors.selectedColor = colors.highlightedColor;
                        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
                        colors.colorMultiplier = 1f;
                        colors.fadeDuration = 0.1f;
                        button.colors = colors;
                        label.enabled = false;
                        label.raycastTarget = false;
                        EditorUtility.SetDirty(image);
                        EditorUtility.SetDirty(button);
                        EditorUtility.SetDirty(label);
                        report.Add(scene.name + "/" + button.name + " → " + text);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            return string.Join("\n", report);
        }

        static Sprite ImportButtonSprite(string label)
        {
            string path = "Assets/UI/" + label + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少按钮素材：" + path);
            Rect rect = FindArtworkRect(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects().FirstOrDefault(x => x.name == label);
            var id = old != null ? old.spriteID : GUID.Generate();
            provider.SetSpriteRects(new[] { new SpriteRect
            {
                name = label, rect = rect, alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f), spriteID = id
            }});
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(label, id) });
            provider.Apply();
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("切片未生成：" + path);
            return sprite;
        }

        static Rect FindArtworkRect(string path)
        {
            // 只读取 PNG 计算切片；原图不改动。忽略孤立的透明边缘杂点。
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("无法读取 " + path);
                int width = texture.width, height = texture.height;
                var pixels = texture.GetPixels32();
                var rows = new int[height];
                var columns = new int[width];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (pixels[y * width + x].a > 24) { rows[y]++; columns[x]++; }
                int rowThreshold = Mathf.Max(4, rows.Max() / 100);
                int columnThreshold = Mathf.Max(4, columns.Max() / 100);
                int minX = Array.FindIndex(columns, n => n >= columnThreshold);
                int maxX = Array.FindLastIndex(columns, n => n >= columnThreshold);
                int minY = Array.FindIndex(rows, n => n >= rowThreshold);
                int maxY = Array.FindLastIndex(rows, n => n >= rowThreshold);
                if (minX < 0 || minY < 0) throw new InvalidOperationException("素材为空：" + path);
                minX = Mathf.Max(0, minX - 3); minY = Mathf.Max(0, minY - 3);
                maxX = Mathf.Min(width - 1, maxX + 3); maxY = Mathf.Min(height - 1, maxY + 3);
                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // 在运行时导出检查图，暂时把 Overlay UI 交给相机渲染，结束后恢复。
        public static string CapturePreview(string filename)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("请在运行模式检查。");
            var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera == null) throw new InvalidOperationException("未找到相机。");
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>()
                .Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = canvases.Select(x => x.worldCamera).ToArray();
            var distances = canvases.Select(x => x.planeDistance).ToArray();
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                texture.Apply();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/", Path.GetFileName(filename) + ".png"));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                }
                Canvas.ForceUpdateCanvases();
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}

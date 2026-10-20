using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoomDemo.EditorTools
{
    /// <summary>只修复已加载场景中用于 Sliced/Tiled 的 Sprite 导入网格。</summary>
    public static class FullRectSpriteRepair
    {
        [MenuItem("Tools/Room Demo/修复平铺精灵网格")]
        public static void RepairMenu() => RepairLoadedScene();

        public static int RepairLoadedScene()
        {
            var paths = new HashSet<string>();
            foreach (var renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded ||
                    renderer.drawMode == SpriteDrawMode.Simple || renderer.sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(renderer.sprite.texture);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }

            int changed = 0;
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite) continue;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.FullRect) continue;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
                Debug.Log("[Sprite 网格修复] 已设为 Full Rect：" + path, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            return changed;
        }
    }
}

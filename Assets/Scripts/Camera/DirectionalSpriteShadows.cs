using UnityEngine;

namespace RoomDemo
{
    /// <summary>统一控制手绘场景中精灵的模拟地面投影。</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DirectionalSpriteShadows : MonoBehaviour
    {
        [Header("统一投影")]
        [Tooltip("影子延伸的世界方向，与光线来源相反。例如 (1,-1) 表示光从左上照来，影子向右下延伸。")]
        public Vector2 shadowDirection = new Vector2(1f, -1f);

        [Tooltip("每单位物品高度对应的投影长度。值越大，影子越长。")]
        [Min(0f)] public float shadowLength = 0.5f;

        [Tooltip("保留多少原图的纵向高度，用于模拟地面透视。")]
        [Range(0.01f, 1f)] public float heightCompression = 0.18f;

        [Tooltip("统一的阴影颜色及透明度。建议使用低透明度的深灰或深褐色。")]
        public Color shadowColor = new Color(0.18f, 0.15f, 0.2f, 0.2f);

        [Tooltip("阴影的模糊半径，单位为世界单位。越大越柔和，细小轮廓也会被淡化；设为 0 恢复清晰投影。")]
        [Range(0f, 1f)] public float softness = 0.35f;

        [Tooltip("影子的层内排序。应高于地面背景、低于家具；当前背景为 -100，默认影子为 -50。排序图层跟随各自物品。")]
        public int sortingOrder = -50;

        Material _material;
        internal Material ShadowMaterial
        {
            get
            {
                if (_material != null) return _material;
                var shader = Resources.Load<Shader>("SpriteProjectedShadow");
                if (shader != null)
                    _material = new Material(shader) { name = "Sprite Projected Shadow", hideFlags = HideFlags.HideAndDontSave };
                return _material;
            }
        }

        void OnDisable()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }
    }
}

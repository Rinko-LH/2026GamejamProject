using UnityEngine;

namespace RoomDemo
{
    /// <summary>Creates an enlarged, tinted copy behind each sprite to show selection.</summary>
    public sealed class InteractionOutline : MonoBehaviour
    {
        static Material _outlineMaterial;
        static readonly Color OutlineColor = new Color(1f, 0.68f, 0.08f, 1f);
        const float OutlineScale = 1.06f;

        SpriteRenderer[] _sources;
        SpriteRenderer[] _outlineRenderers;
        bool _outlined;

        void Awake() => Initialize();

        public void Initialize()
        {
            if (_sources != null) return;
            _sources = GetComponentsInChildren<SpriteRenderer>(true);
            _outlineRenderers = new SpriteRenderer[_sources.Length];

            // Shader.Find 只按名称查找，若项目里没有材质或场景直接引用它，
            // Unity 构建时可能会把 Shader 剔除。放在 Resources 中并显式加载，
            // 确保打包版本也会包含描边 Shader。
            var shader = Resources.Load<Shader>("SpriteSelectionOutline");
            if (shader == null)
                shader = Shader.Find("RoomDemo/Sprite Selection Outline");
            if (shader == null)
            {
                Debug.LogError("Selection outline shader not found. Expected Resources/SpriteSelectionOutline.shader or RoomDemo/Sprite Selection Outline.", this);
                enabled = false;
                return;
            }

            if (_outlineMaterial == null)
                _outlineMaterial = new Material(shader) { name = "Runtime Interaction Outline" };

            for (int i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                if (source == null) continue;

                var outlineObject = new GameObject("SelectionOutline");
                outlineObject.transform.SetParent(source.transform, false);
                outlineObject.transform.localScale = Vector3.one * OutlineScale;

                var outline = outlineObject.AddComponent<SpriteRenderer>();
                outline.sprite = source.sprite;
                outline.color = OutlineColor;
                outline.sharedMaterial = _outlineMaterial;
                outline.sortingLayerID = source.sortingLayerID;
                outline.sortingOrder = source.sortingOrder - 1;
                outline.flipX = source.flipX;
                outline.flipY = source.flipY;
                outline.maskInteraction = source.maskInteraction;
                outline.drawMode = source.drawMode;
                outline.size = source.size;
                outline.enabled = false;
                _outlineRenderers[i] = outline;
            }
        }

        void LateUpdate()
        {
            if (_sources == null || _outlineRenderers == null) return;
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                var outline = _outlineRenderers[i];
                if (source == null || outline == null) continue;

                if (outline.sprite != source.sprite) outline.sprite = source.sprite;
                if (outline.sortingLayerID != source.sortingLayerID) outline.sortingLayerID = source.sortingLayerID;
                if (outline.sortingOrder != source.sortingOrder - 1) outline.sortingOrder = source.sortingOrder - 1;
                outline.enabled = _outlined && source.enabled && source.sprite != null;
            }
        }

        public void SetOutlined(bool outlined)
        {
            _outlined = outlined;
            if (_outlineRenderers == null) return;
            foreach (var outline in _outlineRenderers)
                if (outline != null) outline.enabled = outlined;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace RoomDemo
{
    /// <summary>从精灵的透明轮廓生成可在编辑器预览的地面投影。</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class SpriteShadowCaster : MonoBehaviour
    {
        [Tooltip("统一投影设置。留空时自动查找场景中的 DirectionalSpriteShadows。")]
        public DirectionalSpriteShadows lighting;
        [Tooltip("投影来源。留空使用当前物体上的 SpriteRenderer。不会收集子物体，避免把高亮、提示图也投影出去。")]
        public SpriteRenderer source;
        [Tooltip("可选的落地锚点。只使用其世界坐标 Y；留空时使用精灵可见边界的底边。")]
        public Transform groundAnchor;
        [Tooltip("此物品的投影长度倍率，用来模拟不同的高度。")]
        [Min(0f)] public float lengthMultiplier = 1f;

        GameObject _shadowObject;
        MeshRenderer _renderer;
        Mesh _mesh;
        Sprite _sprite;
        Vector2[] _sourceVertices;
        Vector3[] _projectedVertices;
        Vector2[] _sourceUVs;
        ushort[] _sourceTriangles;
        Vector4 _uvBounds;
        readonly Vector3[] _quadVertices = new Vector3[4];
        readonly Vector2[] _quadUVs = new Vector2[4];
        MaterialPropertyBlock _properties;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BlurUVX = Shader.PropertyToID("_BlurUVX");
        static readonly int BlurUVY = Shader.PropertyToID("_BlurUVY");
        static readonly int UVBounds = Shader.PropertyToID("_UVBounds");

        void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
#endif
        }

#if UNITY_EDITOR
        void UpdateEditorPreview()
        {
            if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                && !UnityEditor.EditorApplication.isCompiling)
                LateUpdate();
        }
#endif

        void LateUpdate()
        {
            if (source == null) source = GetComponent<SpriteRenderer>();
            if (lighting == null) lighting = FindObjectOfType<DirectionalSpriteShadows>();
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy
                || source.sprite == null || source.drawMode != SpriteDrawMode.Simple
                || lighting == null || !lighting.isActiveAndEnabled)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }

            if (_shadowObject == null)
            {
                // 放在控制器下，避免进入物品的交互描边和自动排序收集范围。
                _shadowObject = new GameObject(name + "_投影") { hideFlags = HideFlags.HideAndDontSave };
                _shadowObject.transform.SetParent(lighting.transform, false);
                _mesh = new Mesh { name = name + " Shadow Mesh", hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
                _mesh.vertices = _quadVertices;
                _mesh.uv = _quadUVs;
                _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _shadowObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = _shadowObject.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _properties = new MaterialPropertyBlock();
            }

            if (_sprite != source.sprite)
            {
                _sprite = source.sprite;
                _sourceVertices = _sprite.vertices;
                _projectedVertices = new Vector3[_sourceVertices.Length];
                _sourceUVs = _sprite.uv;
                _sourceTriangles = _sprite.triangles;
                Vector2 uvMin = _sourceUVs[0], uvMax = _sourceUVs[0];
                foreach (var uv in _sourceUVs)
                {
                    uvMin = Vector2.Min(uvMin, uv);
                    uvMax = Vector2.Max(uvMax, uv);
                }
                _uvBounds = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y);
            }

            float groundY = groundAnchor != null ? groundAnchor.position.y : source.bounds.min.y;
            Vector2 direction = lighting.shadowDirection.sqrMagnitude > 0.0001f
                ? lighting.shadowDirection.normalized : Vector2.zero;
            float length = Mathf.Max(0f, lighting.shadowLength) * Mathf.Max(0f, lengthMultiplier);
            float compression = Mathf.Clamp(lighting.heightCompression, 0.01f, 1f);
            for (int i = 0; i < _sourceVertices.Length; i++)
            {
                Vector2 vertex = _sourceVertices[i];
                if (source.flipX) vertex.x = -vertex.x;
                if (source.flipY) vertex.y = -vertex.y;
                Vector3 world = source.transform.TransformPoint(vertex);
                float height = world.y - groundY;
                world.x += direction.x * height * length;
                world.y = groundY + height * compression + direction.y * height * length;
                _projectedVertices[i] = world;
            }

            // 用带留白的矩形承载模糊，避免原精灵的紧密网格裁掉柔化后的外缘。
            int a = _sourceTriangles[0], b = _sourceTriangles[1], c = _sourceTriangles[2];
            Vector2 origin = _projectedVertices[a];
            Vector2 ab = (Vector2)_projectedVertices[b] - origin;
            Vector2 ac = (Vector2)_projectedVertices[c] - origin;
            float determinant = ab.x * ac.y - ac.x * ab.y;
            if (Mathf.Abs(determinant) < 0.000001f)
            {
                _renderer.enabled = false;
                return;
            }
            Vector2 uvAB = _sourceUVs[b] - _sourceUVs[a];
            Vector2 uvAC = _sourceUVs[c] - _sourceUVs[a];
            Vector2 uvPerWorldX = (uvAB * ac.y - uvAC * ab.y) / determinant;
            Vector2 uvPerWorldY = (uvAC * ab.x - uvAB * ac.x) / determinant;
            Vector2 min = _projectedVertices[0], max = _projectedVertices[0];
            foreach (var point in _projectedVertices)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            float softness = Mathf.Max(0f, lighting.softness);
            min -= Vector2.one * softness;
            max += Vector2.one * softness;
            for (int i = 0; i < 4; i++)
            {
                var world = new Vector3(i == 1 || i == 2 ? max.x : min.x,
                    i >= 2 ? max.y : min.y, _projectedVertices[a].z);
                _quadVertices[i] = _shadowObject.transform.InverseTransformPoint(world);
                _quadUVs[i] = _sourceUVs[a] + uvPerWorldX * (world.x - origin.x)
                    + uvPerWorldY * (world.y - origin.y);
            }
            _mesh.vertices = _quadVertices;
            _mesh.uv = _quadUVs;
            _mesh.RecalculateBounds();
            _shadowObject.layer = source.gameObject.layer;
            _renderer.sortingLayerID = source.sortingLayerID;
            _renderer.sortingOrder = lighting.sortingOrder;
            _renderer.sharedMaterial = lighting.ShadowMaterial;
            Color color = lighting.shadowColor;
            color.a *= source.color.a;
            _properties.SetTexture(MainTex, _sprite.texture);
            _properties.SetColor(ColorId, color);
            _properties.SetVector(BlurUVX, new Vector4(uvPerWorldX.x * softness, uvPerWorldX.y * softness, 0f, 0f));
            _properties.SetVector(BlurUVY, new Vector4(uvPerWorldY.x * softness, uvPerWorldY.y * softness, 0f, 0f));
            _properties.SetVector(UVBounds, _uvBounds);
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = _renderer.sharedMaterial != null && color.a > 0f;
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
            if (_shadowObject != null)
            {
                if (Application.isPlaying) Destroy(_shadowObject);
                else DestroyImmediate(_shadowObject);
            }
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            _sprite = null;
        }
    }
}

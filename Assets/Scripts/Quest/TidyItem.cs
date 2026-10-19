using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>单个杂物的选择、整体高亮、最短路径转正与区域判定。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TidyItem : MonoBehaviour
    {
        public Collider2D hitCollider;
        [Tooltip("放置锚点进入此区域且旋转归零后，左键才可放下。")]
        public Collider2D targetArea;
        [Tooltip("可选的共享精确落点。设置后所有杂物可吸附到同一个 Transform。")]
        public Transform placementPoint;
        [Min(0f)] public float placementPointTolerance = 0.75f;
        [Tooltip("拿起时点亮对应目标位置的提示图，可留空。")]
        public SpriteRenderer targetHint;
        [Tooltip("留空使用物体中心；可指定子物体作为放置判定点。")]
        public Transform placementAnchor;
        [Min(1f)] public float rotationSpeed = 360f;
        [Range(0f, 10f)] public float rotationTolerance = 1f;
        public bool IsPlaced { get; private set; }

        public struct State
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool placed;
        }

        State _initial;
        Color _targetColor;
        bool _initialized, _held, _straightening;
        InteractionOutline _outline;
        SpriteRenderer[] _sprites, _fills;
        int[] _orders;
        static Material _fillMaterial;

        void Reset()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
        }

        void Awake() => Initialize();

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
            _initial = Capture();
            if (targetHint != null) _targetColor = targetHint.color;
            var sources = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.name != "SelectionOutline" && sprite.name != "HeldHighlight") sources.Add(sprite);
            _sprites = sources.ToArray();
            _orders = new int[_sprites.Length];
            _fills = new SpriteRenderer[_sprites.Length];
            _outline = GetComponent<InteractionOutline>();
            if (_outline == null) _outline = gameObject.AddComponent<InteractionOutline>();
            _outline.Initialize();
            var shader = Resources.Load<Shader>("SpriteSelectionOutline");
            if (_fillMaterial == null && shader != null) _fillMaterial = new Material(shader);
            for (int i = 0; i < _sprites.Length; i++)
            {
                var source = _sprites[i];
                _orders[i] = source.sortingOrder;
                var overlay = new GameObject("HeldHighlight").AddComponent<SpriteRenderer>();
                overlay.transform.SetParent(source.transform, false);
                overlay.sprite = source.sprite;
                if (_fillMaterial != null) overlay.sharedMaterial = _fillMaterial;
                overlay.flipX = source.flipX;
                overlay.flipY = source.flipY;
                overlay.drawMode = source.drawMode;
                overlay.size = source.size;
                overlay.maskInteraction = source.maskInteraction;
                overlay.sortingLayerID = source.sortingLayerID;
                overlay.sortingOrder = source.sortingOrder + 1;
                overlay.enabled = false;
                _fills[i] = overlay;
            }
        }

        public bool Contains(Vector2 point) => hitCollider != null && hitCollider.enabled && hitCollider.OverlapPoint(point);

        public bool DrawsAbove(TidyItem other)
        {
            if (_sprites.Length == 0 || other._sprites.Length == 0) return true;
            var a = _sprites[0];
            var b = other._sprites[0];
            int layerA = SortingLayer.GetLayerValueFromID(a.sortingLayerID);
            int layerB = SortingLayer.GetLayerValueFromID(b.sortingLayerID);
            return layerA != layerB ? layerA > layerB : a.sortingOrder >= b.sortingOrder;
        }

        public void SetHovered(bool value) { if (_outline != null) _outline.SetOutlined(value && !IsPlaced); }

        public void SetHeld(bool held)
        {
            _held = held;
            _straightening = false;
            SetHovered(false);
            if (targetHint != null)
                targetHint.color = held ? new Color(1f, 0.85f, 0.25f, 0.55f) : _targetColor;
            for (int i = 0; i < _sprites.Length; i++)
            {
                _sprites[i].sortingOrder = _orders[i] + (held ? 1000 : 0);
                _fills[i].sortingOrder = _sprites[i].sortingOrder + 1;
                _fills[i].enabled = held && _sprites[i].enabled;
            }
            SetHeldFeedback(false);
        }

        public void SetHeldFeedback(bool valid)
        {
            var color = valid ? new Color(0.3f, 1f, 0.5f, 0.45f) : new Color(1f, 0.85f, 0.25f, 0.4f);
            foreach (var fill in _fills) fill.color = color;
        }

        public void BeginStraighten() { if (_held) _straightening = true; }

        public void StepRotation(float deltaTime)
        {
            if (!_held || !_straightening) return;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity,
                Mathf.Max(1f, rotationSpeed) * deltaTime);
        }

        public bool CanPlace()
        {
            if (targetArea == null || !targetArea.enabled || !targetArea.gameObject.activeInHierarchy) return false;
            var point = placementAnchor != null ? placementAnchor.position : transform.position;
            bool insideTarget = targetArea.OverlapPoint(point);
            bool nearSharedPoint = placementPoint == null
                || Vector2.Distance(point, placementPoint.position) <= placementPointTolerance;
            return insideTarget && nearSharedPoint
                && Quaternion.Angle(transform.localRotation, Quaternion.identity) <= rotationTolerance;
        }

        public void Place()
        {
            if (placementPoint != null)
            {
                var anchorPosition = placementAnchor != null ? placementAnchor.position : transform.position;
                transform.position += placementPoint.position - anchorPosition;
            }
            transform.localRotation = Quaternion.identity;
            IsPlaced = true;
            SetHeld(false);
        }

        public State Capture() => new State
        {
            position = transform.localPosition, rotation = transform.localRotation, placed = IsPlaced
        };

        public void Restore(State state)
        {
            Initialize();
            SetHeld(false);
            transform.localPosition = state.position;
            transform.localRotation = state.rotation;
            IsPlaced = state.placed;
            SetHovered(false);
        }

        public void ResetItem() { Initialize(); Restore(_initial); }
    }
}


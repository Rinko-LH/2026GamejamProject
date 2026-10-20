using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>以 Player 根坐标为支点摆动美术子节点，不旋转 Player 的物理和交互组件。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Simple2DMovement))]
    public sealed class PlayerWalkSway : MonoBehaviour
    {
        [Header("角色美术")]
        [Tooltip("只绑定美术子节点，默认使用 Body；围绕 Player 根物体坐标摆动。不能绑定 Player 根物体。")]
        public Transform body;

        [Header("行走摆动")]
        [Tooltip("向两侧倾斜的最大角度。")]
        [Range(0f, 20f)] public float swayAngle = 6f;
        [Tooltip("每秒完成几次左右摆动。")]
        [Min(0f)] public float swayFrequency = 2.5f;
        [Tooltip("追随摆动和停下回正的速度，单位为度/秒。")]
        [Min(1f)] public float rotationSpeed = 180f;
        [Tooltip("低于这个实际移动速度视为静止，避免碰撞细微抖动触发动画。")]
        [Min(0f)] public float minimumMoveSpeed = 0.05f;

        Rigidbody2D _rigidbody;
        Transform _boundBody;
        Quaternion _restRotation;
        Vector3 _restLocalPosition;
        Vector3 _restRootPosition;
        Quaternion _restRootRotation;
        Vector2 _lastPhysicsPosition;
        bool _moving;
        float _phase;
        float _angle;

        public bool IsMoving => _moving && InputManager.Instance != null && InputManager.Instance.Move.sqrMagnitude > 0.0001f;

        void OnEnable()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _lastPhysicsPosition = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
            BindBody();
        }

        bool BindBody()
        {
            if (body == null) body = transform.Find("Body");
            var candidate = body != null && body != transform && body.IsChildOf(transform) ? body : null;
            if (_boundBody == candidate) return _boundBody != null;
            RestoreBody();
            _boundBody = candidate;
            _angle = _phase = 0f;
            if (_boundBody != null)
            {
                _restRotation = _boundBody.localRotation;
                _restLocalPosition = _boundBody.localPosition;
                _restRootPosition = transform.InverseTransformPoint(_boundBody.position);
                _restRootRotation = Quaternion.Inverse(transform.rotation) * _boundBody.rotation;
            }
            return _boundBody != null;
        }

        void FixedUpdate()
        {
            // 使用上一次物理解算后的真实位移，输入有值但被墙挡住时不会持续摆动。
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
            Vector2 position = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
            float distanceSquared = (position - _lastPhysicsPosition).sqrMagnitude;
            float threshold = Mathf.Max(0.001f, minimumMoveSpeed) * Time.fixedDeltaTime;
            _moving = distanceSquared > threshold * threshold && distanceSquared < 4f;
            _lastPhysicsPosition = position;
        }

        void LateUpdate()
        {
            if (!BindBody()) return;
            float targetAngle = 0f;
            if (IsMoving)
            {
                _phase = Mathf.Repeat(_phase + Time.deltaTime * Mathf.Max(0f, swayFrequency) * Mathf.PI * 2f, Mathf.PI * 2f);
                targetAngle = Mathf.Sin(_phase) * swayAngle;
            }
            _angle = Mathf.MoveTowards(_angle, targetAngle, Mathf.Max(1f, rotationSpeed) * Time.deltaTime);
            var sway = Quaternion.AngleAxis(_angle, Vector3.forward);
            _boundBody.position = transform.TransformPoint(sway * _restRootPosition);
            _boundBody.rotation = transform.rotation * sway * _restRootRotation;
            if (!IsMoving && Mathf.Abs(_angle) < 0.001f) _phase = 0f;
        }

        void OnDisable()
        {
            RestoreBody();
            _boundBody = null;
            _moving = false;
            _angle = _phase = 0f;
        }

        void RestoreBody()
        {
            if (_boundBody == null) return;
            _boundBody.localPosition = _restLocalPosition;
            _boundBody.localRotation = _restRotation;
        }
    }
}

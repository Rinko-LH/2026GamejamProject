using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// Keeps the chair in front of the player when the player is behind it (higher Y),
    /// and behind the player when the player is in front (lower Y).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOcclusionSorting : MonoBehaviour
    {
        const string ChairObjectName = "椅子";

        SpriteRenderer _playerBody;
        SpriteRenderer[] _chairRenderers;
        Transform _chair;
        int _lastChairOrder = int.MinValue;
        int _lastLayerId;
        bool _searched;

        void LateUpdate()
        {
            if (_playerBody == null)
                _playerBody = FindPlayerBody();
            if (!_searched || _chair == null)
                FindChair();
            if (_playerBody == null || _chair == null) return;

            // In this project, Body is rendered at 15080. Keep the chair one step
            // on either side of it so the comparison is deterministic.
            int chairOrder = _chair.position.y <= transform.position.y
                ? _playerBody.sortingOrder + 1
                : _playerBody.sortingOrder - 1;
            int layerId = _playerBody.sortingLayerID;
            if (chairOrder == _lastChairOrder && layerId == _lastLayerId) return;

            foreach (var renderer in _chairRenderers)
            {
                if (renderer == null) continue;
                renderer.sortingLayerID = layerId;
                renderer.sortingOrder = chairOrder;
            }
            _lastChairOrder = chairOrder;
            _lastLayerId = layerId;
        }

        SpriteRenderer FindPlayerBody()
        {
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null && renderer.gameObject.name != "Shadow") return renderer;
            return null;
        }

        void FindChair()
        {
            _searched = true;
            var chairObject = GameObject.Find(ChairObjectName);
            _chair = chairObject != null ? chairObject.transform : null;
            _chairRenderers = _chair != null
                ? _chair.GetComponentsInChildren<SpriteRenderer>(true)
                : System.Array.Empty<SpriteRenderer>();
            _lastChairOrder = int.MinValue;
        }
    }
}

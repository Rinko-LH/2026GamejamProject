using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// Attach this component to any sprite object that should pass in front of or
    /// behind the player according to their Y positions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOcclusionSorting : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Player root. If empty, the first Simple2DMovement in the scene is used.")]
        public Transform player;

        [Tooltip("Player sprite used as the sorting-order reference. If empty, it is found under Player, excluding Shadow.")]
        public SpriteRenderer playerBody;

        [Tooltip("Player shadow renderer. If empty, a child SpriteRenderer named Shadow is found and its sorting layer follows Player Body.")]
        public SpriteRenderer playerShadow;

        [Tooltip("Renderers to sort. If empty, all SpriteRenderers under this object are collected.")]
        public SpriteRenderer[] targetRenderers;

        [Tooltip("Transform whose world Y determines whether this object is in front of or behind the player. Assign a child collider or another chosen pivot. If empty, this object's root Transform is used.")]
        public Transform occlusionPoint;

        [Header("Sorting")]
        [Tooltip("Use the player's sorting layer for the target renderers.")]
        public bool matchPlayerSortingLayer = true;

        [Tooltip("How many sorting-order steps the target moves in front of or behind the player.")]
        [Min(1)] public int sortingOrderOffset = 1;

        void Awake()
        {
            ResolveReferences();
        }

        void LateUpdate()
        {
            ResolveReferences();
            if (player == null || playerBody == null || targetRenderers == null) return;

            // Keep the shadow on the same Sorting Layer as the player body while
            // preserving its authored Order in Layer.
            if (playerShadow != null && playerShadow.sortingLayerID != playerBody.sortingLayerID)
                playerShadow.sortingLayerID = playerBody.sortingLayerID;

            // Prevent an accidental copy of this component on the Player from
            // changing the reference renderer's own order every frame.
            if (transform == player) return;

            // The artwork can live on the root while the meaningful depth position
            // is authored separately (for example, at a child collider near the feet).
            float targetY = occlusionPoint != null ? occlusionPoint.position.y : transform.position.y;
            bool targetInFront = targetY <= player.position.y;
            int targetOrder = playerBody.sortingOrder
                + (targetInFront ? sortingOrderOffset : -sortingOrderOffset);
            int playerLayerId = playerBody.sortingLayerID;

            foreach (var renderer in targetRenderers)
            {
                if (renderer == null || renderer == playerBody) continue;

                if (matchPlayerSortingLayer && renderer.sortingLayerID != playerLayerId)
                    renderer.sortingLayerID = playerLayerId;
                if (renderer.sortingOrder != targetOrder)
                    renderer.sortingOrder = targetOrder;
            }
        }

        void ResolveReferences()
        {
            if (player == null)
            {
                var movement = FindObjectOfType<Simple2DMovement>();
                if (movement != null) player = movement.transform;
            }

            if (playerBody == null && player != null)
            {
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null || renderer.gameObject.name == "Shadow") continue;
                    playerBody = renderer;
                    break;
                }
            }

            if (playerShadow == null && player != null)
            {
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer != null && renderer.gameObject.name == "Shadow")
                    {
                        playerShadow = renderer;
                        break;
                    }
                }
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}

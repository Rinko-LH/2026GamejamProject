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
        [Tooltip("玩家根物体。留空时自动查找场景中第一个带有 Simple2DMovement 组件的物体。")]
        public Transform player;

        [Tooltip("用于确定排序基准的玩家精灵。留空时自动查找玩家自身及所有子物体（含未激活物体）上的 SpriteRenderer，排除名为 Shadow 的物体。")]
        public SpriteRenderer playerBody;

        [Tooltip("玩家阴影。留空时自动查找玩家自身及所有子物体（含未激活物体）中名为 Shadow 的 SpriteRenderer；阴影的排序图层跟随玩家身体，保留原有的层内顺序。")]
        public SpriteRenderer playerShadow;

        [Tooltip("需要调整排序的精灵。留空时自动收集当前物体自身及所有子物体（含未激活物体）上的 SpriteRenderer。")]
        public SpriteRenderer[] targetRenderers;

        [Tooltip("遮挡判断点。根据其世界坐标 Y 值判断物体位于玩家前方还是后方，可指定带碰撞体的子物体或其他参考点。留空时使用挂载此组件的物体的位置。")]
        public Transform occlusionPoint;

        [Header("Sorting")]
        [Tooltip("让目标精灵使用与玩家相同的排序图层。")]
        public bool matchPlayerSortingLayer = true;

        [Tooltip("目标精灵相对玩家的层内排序偏移量，位于前方时增加，位于后方时减少。")]
        [Min(1)] public int sortingOrderOffset = 1;

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
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

            // Discard references belonging to a previously assigned player.
            if (playerBody != null && (player == null || !playerBody.transform.IsChildOf(player)))
                playerBody = null;
            if (playerShadow != null && (player == null || !playerShadow.transform.IsChildOf(player)))
                playerShadow = null;

            if (player != null && (playerBody == null || playerShadow == null))
            {
                // Includes the player root and inactive descendants.
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.gameObject.name == "Shadow")
                    {
                        if (playerShadow == null) playerShadow = renderer;
                    }
                    else if (playerBody == null)
                        playerBody = renderer;

                    if (playerBody != null && playerShadow != null) break;
                }
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}

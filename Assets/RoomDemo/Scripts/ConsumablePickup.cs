using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 通用道具拾取：零食 / 水等，用于支线补充状态。
    /// 现在只做最小可用版本，等支线玩法展开再细化。
    /// </summary>
    public class ConsumablePickup : RoomInteractable
    {
        public PlayerStats stats;

        public enum Effect { Water, Satiety, Spirit }
        public Effect effect = Effect.Satiety;

        [Tooltip("补充多少点")]
        public float amount = 20f;

        [Tooltip("是否要消耗游戏内时间")]
        public bool costTime = true;
        public float costMinutes = 20f;

        public DayTimer timer;

        public override void Interact()
        {
            if (stats == null) return;

            var channel = effect switch
            {
                Effect.Water => stats.Water,
                Effect.Satiety => stats.Satiety,
                _ => stats.Spirit
            };

            channel.Add(amount);

            if (costTime && timer != null) timer.SpendMinutes(costMinutes);

            canInteract = false;
            gameObject.SetActive(false);
        }
    }
}

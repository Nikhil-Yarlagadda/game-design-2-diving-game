using UnityEngine;

namespace DivingPrototype
{
    public sealed class HydroxTank : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float maxCapacity = 180f;
        [SerializeField, Min(.01f)] private float consumptionRate = 1f;

        public float MaxCapacity => maxCapacity;
        public float ConsumptionRate => consumptionRate;
        public float CurrentCapacity { get; private set; }
        public float NormalizedFill => Mathf.Clamp01(CurrentCapacity / maxCapacity);
        public bool IsDepleted => CurrentCapacity <= 0f;

        private void Awake() => Refill();

        private void OnValidate()
        {
            ValidateSettings();
            CurrentCapacity = Mathf.Clamp(CurrentCapacity, 0f, maxCapacity);
        }

        private void ValidateSettings()
        {
            maxCapacity = float.IsNaN(maxCapacity) || float.IsInfinity(maxCapacity) ? 180f : Mathf.Max(.01f, maxCapacity);
            consumptionRate = float.IsNaN(consumptionRate) || float.IsInfinity(consumptionRate) ? 1f : Mathf.Max(.01f, consumptionRate);
        }

        public void Refill()
        {
            ValidateSettings();
            CurrentCapacity = maxCapacity;
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime)) return;
            CurrentCapacity = Mathf.Max(0f, CurrentCapacity - consumptionRate * deltaTime);
        }
    }
}

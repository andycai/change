using UnityEngine;

namespace Change.Runtime
{
    internal static class FrameBudgetBootstrap
    {
        private const string DriverName = "[Change.FrameBudget]";

        public static FrameBudgetDriver EnsureDriver(in FrameBudgetPolicy policy)
        {
            var existing = GameObject.Find(DriverName);
            if (existing != null && existing.TryGetComponent<FrameBudgetDriver>(out var existingDriver))
            {
                existingDriver.ApplyPolicy(policy);
                return existingDriver;
            }

            var go = new GameObject(DriverName);
            Object.DontDestroyOnLoad(go);

            var driver = go.AddComponent<FrameBudgetDriver>();
            driver.ApplyPolicy(policy);
            return driver;
        }
    }
}

using UnityEngine;

namespace YOUR_CODE {
    public static class AllyPerceptionHelper
    {
        public const float SpotRadius = 25f;        // how far allies can see
        public const float FovDegrees = 140f;       // full cone width
        public const float EnemyEngageRange = 15f;  // enemies only shoot inside this (EnemyAgent.cs)
        public const float StealthMargin = 2f;      // stay this much outside it

        public static bool HasLineOfSight(Map map, Vector3 from, Vector3 to)
        {
            float dist = Vector3.Distance(from, to);
            int steps = Mathf.CeilToInt(dist / 0.5f);
            for (int i = 1; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
                if (!map.IsNavigatable((int)p.x, (int)p.y)) return false;  // tree blocks sight
            }
            return true;
        }

        public static bool CanSee(SteeringAgent self, SteeringAgent other)
        {
            Vector3 toOther = other.transform.position - self.transform.position;
            if (toOther.sqrMagnitude > SpotRadius * SpotRadius) return false;
            if (Vector3.Angle(self.transform.up, toOther) > FovDegrees * 0.5f) return false;
            return HasLineOfSight(GameData.Instance.Map, self.transform.position, other.transform.position);
        }

        public static SteeringAgent FindVisibleEnemy(SteeringAgent self)
        {
            SteeringAgent best = null;
            float bestSqr = float.MaxValue;
            foreach (var enemy in GameData.Instance.enemies)
            {
                if (enemy.Health <= 0f || !CanSee(self, enemy)) continue;
                float sqr = (enemy.transform.position - self.transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = enemy; }
            }
            return best;
        }

        // True while we're within reach of this enemy's guns, so it would shoot us
        public static bool IsInEnemyThreat(SteeringAgent self, SteeringAgent enemy) =>
            (enemy.transform.position - self.transform.position).magnitude <= EnemyEngageRange + StealthMargin;
    }
}
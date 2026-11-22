using UnityEngine;

namespace YOUR_CODE {
    public sealed class WanderBehaviour : SteeringBehaviour
    {
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float maxTurnDegrees = 60f;   // bias towards current heading
        [SerializeField] private float arriveRadius = 1.0f;
        [SerializeField] private float lookAhead = 1.0f;
        [SerializeField] private float maxTimeOnTarget = 6f;   // give up on a target after this
        [SerializeField] private int attempts = 10;
        [SerializeField] private bool avoidWater = true;       // water/mud are "navigatable" but slow

        private Vector3 target;
        private bool hasTarget;
        private float targetTimer;

        public override Vector3 UpdateBehaviour(SteeringAgent agent)
        {
            Map map = GameData.Instance.Map;
            Vector3 pos = transform.position;
            targetTimer += Time.deltaTime;

            bool needNew =
                !hasTarget ||
                (target - pos).sqrMagnitude < arriveRadius * arriveRadius ||
                targetTimer > maxTimeOnTarget ||
                !IsWalkable(map, pos + SafeDir(agent.CurrentVelocity) * lookAhead); // about to hit something

            if (needNew)
                hasTarget = TryPickTarget(map, pos, agent.CurrentVelocity);

            if (!hasTarget)
            {
                // boxed in: brake and try again next frame
                desiredVelocity = Vector3.zero;
            }
            else
            {
                desiredVelocity = (target - pos).normalized * SteeringAgent.MaxCurrentSpeed;
            }

            steeringVelocity = desiredVelocity - agent.CurrentVelocity;
            return steeringVelocity;
        }

        private bool TryPickTarget(Map map, Vector3 pos, Vector3 velocity)
        {
            Vector3 forward = SafeDir(velocity);
            float heading = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

            for (int i = 0; i < attempts; i++)
            {
                // first half: stay roughly on heading. second half: any direction, to escape dead ends
                float angle = i < attempts / 2
                    ? heading + Random.Range(-maxTurnDegrees, maxTurnDegrees)
                    : Random.Range(0f, 360f);

                float dist = Random.Range(minDistance, maxDistance);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 candidate = pos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * dist;

                if (IsWalkable(map, candidate) && PathClear(map, pos, candidate))
                {
                    target = candidate;
                    targetTimer = 0f;
                    return true;
                }
            }
            return false;
        }

        // Sample along the line so we never pick a target behind a wall of trees
        private bool PathClear(Map map, Vector3 from, Vector3 to)
        {
            float dist = Vector3.Distance(from, to);
            int steps = Mathf.CeilToInt(dist / 0.5f);   // cells are 1 unit, 0.5 step won't skip one
            for (int s = 1; s <= steps; s++)
            {
                if (!IsWalkable(map, Vector3.Lerp(from, to, s / (float)steps)))
                    return false;
            }
            return true;
        }

        private bool IsWalkable(Map map, Vector3 p)
        {
            int x = (int)p.x, y = (int)p.y;
            if (!map.IsNavigatable(x, y)) return false;   // also handles out of bounds
            if (avoidWater && map.GetTerrainAt(x, y) == Map.Terrain.Water) return false;
            return true;
        }

        private static Vector3 SafeDir(Vector3 v) =>
            v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.up;

        public override void DebugDraw(SteeringAgent agent)
        {
            base.DebugDraw(agent);
            if (hasTarget)
                DebugDrawLine("DebugWander " + GetType().Name, transform.position, target, Color.green);
        }
    }
}
using UnityEngine;

namespace YOUR_CODE {
    /// <summary>
    /// While the general has an attack order, move the pawn out in front of the general, towards the enemy,
    /// to a spot inside gun range. Enemies shoot the NEAREST ally, so this draws fire away from the general.
    /// </summary>
    public sealed class ProtectGeneralBehaviour : SteeringBehaviour
    {
        private const float StandOffFromEnemy = 10f;   // how far from the enemy pawns stop (gun range is 14)
        private const float SpreadWidth = 4f;          // sideways spread so pawns don't stack
        private const float ArriveRadius = 3f;

        private float lateral;                         // this pawn's fixed sideways slot

        protected override void Start()
        {
            base.Start();
            lateral = Random.Range(-SpreadWidth, SpreadWidth);
        }

        public override Vector3 UpdateBehaviour(SteeringAgent agent)
        {
            var pawn = agent as PawnAgent;
            if (pawn == null || pawn.General == null || !pawn.General.HasAttackOrder)
            {
                desiredVelocity = Vector3.zero;
                steeringVelocity = Vector3.zero;
                return steeringVelocity;
            }

            Vector3 enemyPos = pawn.General.SpottedEnemy.transform.position;
            Vector3 generalPos = pawn.General.transform.position;

            Vector3 dir = enemyPos - generalPos;
            dir.z = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            // Stand point: between us and the enemy, ahead of the general, spread sideways
            Vector3 stand = enemyPos - dir * StandOffFromEnemy + perp * lateral;
            stand = FindWalkable(stand, generalPos);

            Vector3 toStand = stand - transform.position;
            toStand.z = 0f;
            float dist = toStand.magnitude;

            float speed = SteeringAgent.MaxCurrentSpeed * Mathf.Clamp01(dist / ArriveRadius);
            desiredVelocity = dist > 0.01f ? toStand / dist * speed : Vector3.zero;

            steeringVelocity = desiredVelocity - agent.CurrentVelocity;
            return steeringVelocity;
        }

        // If the stand point is a tree/out of bounds, slide it back towards the general until it's walkable
        private static Vector3 FindWalkable(Vector3 point, Vector3 fallback)
        {
            Map map = GameData.Instance.Map;
            for (int i = 0; i <= 10; i++)
            {
                Vector3 p = Vector3.Lerp(point, fallback, i / 10f);
                if (map.IsNavigatable((int)p.x, (int)p.y)) return p;
            }
            return fallback;
        }
    }
}
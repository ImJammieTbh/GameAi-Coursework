namespace YOUR_CODE {
    using UnityEngine;

    /// <summary>
    /// Arrive-style follow: head for the general at full speed, easing to a stop at followDistance.
    /// Same shape as SeekToMouse / EnemySeekBehaviour so debug lines and enable/disable work as normal.
    /// </summary>
    public sealed class FollowGeneralBehaviour : SteeringBehaviour
    {
        public float followDistance = 2.0f;   // pawns settle inside this radius
        public float slowRadius = 2.0f;       // distance over which they ease off

        public override Vector3 UpdateBehaviour(SteeringAgent steeringAgent)
        {
            var pawn = steeringAgent as PawnAgent;

            Vector3 toGeneral = Vector3.zero;
            if (pawn != null && pawn.General != null)
            {
                toGeneral = pawn.General.transform.position - transform.position;
                toGeneral.z = 0.0f;
            }

            float distance = toGeneral.magnitude;
            float speed = Mathf.Clamp01((distance - followDistance) / slowRadius) * SteeringAgent.MaxCurrentSpeed;

            desiredVelocity = distance > 0.0001f ? toGeneral / distance * speed : Vector3.zero;
            steeringVelocity = desiredVelocity - steeringAgent.CurrentVelocity;
            return steeringVelocity;
        }
    }
}
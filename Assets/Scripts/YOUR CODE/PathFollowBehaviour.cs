using System.Collections.Generic;
using UnityEngine;

namespace YOUR_CODE
{
    /// <summary>
    /// Steering behaviour that walks the agent to a target position along an A* path.
    ///
    /// Usage: enable this component and call SetTargetPosition() each frame with where to go.
    /// SteeringAgent.CooperativeArbitration adds up all enabled behaviours, so this works like
    /// Wander or Seek: it just returns a steering velocity.
    /// </summary>
    public class PathFollowBehaviour : SteeringBehaviour
    {
        // ------------------------------------------------------------------ settings

        /// <summary>How often the path is recalculated, since the target (another general) moves</summary>
        private const float SecondsBetweenRepaths = 0.5f;

        /// <summary>How close to a waypoint counts as having reached it</summary>
        private const float WaypointReachedDistance = 0.6f;

        /// <summary>Within this distance of the end, the agent slows down so it doesn't overshoot</summary>
        private const float SlowDownDistance = 3f;

        /// <summary>The agent stops this far away from the target instead of standing on top of it</summary>
        [SerializeField] private float stopDistanceFromTarget = 3f;

        // ------------------------------------------------------------------ state

        private Vector3 targetPosition;
        private bool hasTargetPosition;

        /// <summary>The path from the last search: world positions to walk through, in order</summary>
        private readonly List<Vector3> waypoints = new List<Vector3>();

        /// <summary>Which waypoint we are currently walking towards</summary>
        private int currentWaypointIndex;

        private float nextRepathTimeSeconds;

        /// <summary>True when there is a clear straight line to the target, so the path isn't being used. Only used for the debug line colour</summary>
        private bool isWalkingStraightToTarget;

        // ------------------------------------------------------------------ public functions

        /// <summary>Sets where the agent should go. Safe to call every frame.</summary>
        public void SetTargetPosition(Vector3 newTargetPosition)
        {
            targetPosition = newTargetPosition;
            hasTargetPosition = true;
        }

        public void ClearTargetPosition()
        {
            hasTargetPosition = false;
            waypoints.Clear();
            currentWaypointIndex = 0;
        }

        // ------------------------------------------------------------------ Unity / framework

        /// <summary>Runs when this behaviour is switched on. Any old path is out of date, so throw it away and make a new one</summary>
        private void OnEnable()
        {
            waypoints.Clear();
            currentWaypointIndex = 0;
            nextRepathTimeSeconds = 0f; // 0 means "repath on the very next update"
        }

        public override Vector3 UpdateBehaviour(SteeringAgent steeringAgent)
        {
            desiredVelocity = Vector3.zero;
            isWalkingStraightToTarget = false;

            if (hasTargetPosition)
            {
                Vector3 agentPosition = steeringAgent.transform.position;

                RepathIfDue(agentPosition);

                if (TryGetWaypointToMoveTowards(agentPosition, out Vector3 waypointToMoveTo, out bool isFinalWaypoint))
                {
                    desiredVelocity = CalculateDesiredVelocity(agentPosition, waypointToMoveTo, isFinalWaypoint);
                }
            }

            // Standard steering: the change needed to go from our current velocity to the one we want
            steeringVelocity = desiredVelocity - steeringAgent.CurrentVelocity;
            return steeringVelocity;
        }

        // ------------------------------------------------------------------ the steps of UpdateBehaviour

        /// <summary>Calculates a fresh path, but only every SecondsBetweenRepaths so 16 agents don't all path every frame</summary>
        private void RepathIfDue(Vector3 agentPosition)
        {
            if (Time.time < nextRepathTimeSeconds) return;

            PathFinding.FindPath(agentPosition, targetPosition, waypoints);
            currentWaypointIndex = 0;
            nextRepathTimeSeconds = Time.time + SecondsBetweenRepaths;
        }

        /// <summary>
        /// Works out where to head for right now.
        /// If there's a clear straight line to the target, head straight for it and ignore the path.
        /// Otherwise follow the waypoints. Returns false if there is nowhere to go (no path found).
        /// </summary>
        private bool TryGetWaypointToMoveTowards(Vector3 agentPosition, out Vector3 waypointToMoveTo, out bool isFinalWaypoint)
        {
            isWalkingStraightToTarget = PathFinding.IsStraightLineWalkable(agentPosition, targetPosition);
            if (isWalkingStraightToTarget)
            {
                waypointToMoveTo = targetPosition;
                isFinalWaypoint = true;
                return true;
            }

            // Move on to the next waypoint once we are close to the current one (but never past the last one)
            while (currentWaypointIndex < waypoints.Count - 1 &&
                   DistanceIgnoringHeight(agentPosition, waypoints[currentWaypointIndex]) < WaypointReachedDistance)
            {
                currentWaypointIndex++;
            }

            bool pathExists = currentWaypointIndex < waypoints.Count;
            if (!pathExists)
            {
                waypointToMoveTo = targetPosition;
                isFinalWaypoint = true;
                return false;
            }

            waypointToMoveTo = waypoints[currentWaypointIndex];
            isFinalWaypoint = currentWaypointIndex == waypoints.Count - 1;
            return true;
        }

        /// <summary>The velocity we'd like to have: towards the waypoint at full speed, easing off near the end</summary>
        private Vector3 CalculateDesiredVelocity(Vector3 agentPosition, Vector3 waypointToMoveTo, bool isFinalWaypoint)
        {
            Vector3 towardsWaypoint = waypointToMoveTo - agentPosition;
            towardsWaypoint.z = 0f;
            float distanceToWaypoint = towardsWaypoint.magnitude;

            // Stopping is based on the real target, not just the last waypoint
            float distanceToTarget = DistanceIgnoringHeight(agentPosition, targetPosition);
            bool closeEnoughToStop = distanceToTarget <= stopDistanceFromTarget;

            if (distanceToWaypoint < 0.001f || closeEnoughToStop)
            {
                return Vector3.zero;
            }

            float desiredSpeed = SteeringAgent.MaxCurrentSpeed;
            if (isFinalWaypoint)
            {
                // 0 at the stop distance, 1 once we are SlowDownDistance or more away
                float slowDownAmount = (distanceToTarget - stopDistanceFromTarget) / SlowDownDistance;
                desiredSpeed *= Mathf.Clamp01(slowDownAmount);
            }

            return towardsWaypoint / distanceToWaypoint * desiredSpeed;
        }

        // ------------------------------------------------------------------ debug drawing

        /// <summary>
        /// Draws the path with a LineRenderer (so it shows in the Game view as well as the Scene view).
        /// Tick "Show Debug Lines" on this component to see it.
        ///   Green = the A* path still to walk.   Cyan = clear straight line, path not needed.
        /// </summary>
        public override void DebugDraw(SteeringAgent steeringAgent)
        {
            base.DebugDraw(steeringAgent);

            if (!ShowDebugLines || !enabled || !hasTargetPosition) return;

            // The name must contain "Debug" and this class name so the base class hides it when this behaviour is off
            LineRenderer pathLine = GetDebugLineRenderer("DebugPath " + GetType().Name);
            pathLine.sortingOrder = 100; // draw on top of sprites

            Vector3 agentPosition = steeringAgent.transform.position;

            if (isWalkingStraightToTarget)
            {
                SetLineColour(pathLine, Color.cyan);
                pathLine.positionCount = 2;
                pathLine.SetPosition(0, agentPosition);
                pathLine.SetPosition(1, targetPosition);
                return;
            }

            int waypointsRemaining = waypoints.Count - currentWaypointIndex;
            if (waypointsRemaining <= 0)
            {
                pathLine.positionCount = 0;
                return;
            }

            SetLineColour(pathLine, Color.green);
            pathLine.positionCount = waypointsRemaining + 1;
            pathLine.SetPosition(0, agentPosition);
            for (int i = 0; i < waypointsRemaining; i++)
            {
                pathLine.SetPosition(i + 1, waypoints[currentWaypointIndex + i]);
            }
        }

        private static void SetLineColour(LineRenderer line, Color colour)
        {
            line.startColor = colour;
            line.endColor = colour;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>2D distance (ignores the z axis)</summary>
        private static float DistanceIgnoringHeight(Vector3 a, Vector3 b)
        {
            float differenceX = a.x - b.x;
            float differenceY = a.y - b.y;
            return Mathf.Sqrt(differenceX * differenceX + differenceY * differenceY);
        }
    }
}
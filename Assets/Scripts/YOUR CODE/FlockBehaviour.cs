namespace YOUR_CODE {
    using UnityEngine;

	/// <summary>
	/// Separation + alignment + cohesion against pawns that share this pawn's general.
	/// Returns small additive nudges (the follow behaviour supplies the main "desired - current" term),
	/// so summing the behaviours in the base CooperativeArbitration doesn't double-subtract CurrentVelocity.
	/// The general itself is only used for separation, so pawns don't pile onto it.
	/// </summary>
	public sealed class FlockBehaviour : SteeringBehaviour
	{
		public float neighbourRadius = 3.0f;
		public float separationRadius = SteeringAgent.CollisionRadius * 3.0f;

		public float separationWeight = 1.5f;
		public float alignmentWeight = 0.5f;
		public float cohesionWeight = 0.3f;

		public override Vector3 UpdateBehaviour(SteeringAgent steeringAgent)
		{
			var pawn = steeringAgent as PawnAgent;
			if (pawn == null || pawn.General == null)
			{
				steeringVelocity = Vector3.zero;
				desiredVelocity = steeringAgent.CurrentVelocity;
				return steeringVelocity;
			}

			Vector3 separation = Vector3.zero;
			Vector3 averageVelocity = Vector3.zero;
			Vector3 centre = Vector3.zero;
			int flockmates = 0;

			foreach (var other in GameData.Instance.allies)
			{
				if (other == steeringAgent || other.Health <= 0.0f)
				{
					continue;
				}

				bool isLeader = other == pawn.General;
				var mate = other as PawnAgent;
				bool isFlockmate = mate != null && mate.General == pawn.General;
				if (!isLeader && !isFlockmate)
				{
					continue;
				}

				Vector3 offset = transform.position - other.transform.position;
				offset.z = 0.0f;
				float distance = offset.magnitude;
				if (distance > neighbourRadius)
				{
					continue;
				}

				if (distance < separationRadius && distance > 0.0001f)
				{
					// Push directly away, harder the closer they are
					separation += offset / distance * (1.0f - distance / separationRadius);
				}

				if (isFlockmate)
				{
					flockmates++;
					centre += other.transform.position;
					averageVelocity += other.CurrentVelocity;
				}
			}

			Vector3 nudge = separation * (SteeringAgent.MaxCurrentSpeed * separationWeight);

			if (flockmates > 0)
			{
				nudge += ((averageVelocity / flockmates) - steeringAgent.CurrentVelocity) * alignmentWeight;

				Vector3 toCentre = (centre / flockmates) - transform.position;
				toCentre.z = 0.0f;
				nudge += toCentre * cohesionWeight;
			}

			nudge.z = 0.0f;
			steeringVelocity = nudge;
			desiredVelocity = steeringAgent.CurrentVelocity + steeringVelocity; // so the debug lines show the nudge
			return steeringVelocity;
		}
	}
}
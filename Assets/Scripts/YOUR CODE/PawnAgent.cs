using System.Collections.Generic;

namespace YOUR_CODE {
    /// <summary>
    /// An ally that follows a general. Everything else (attack keys, firing, direction) is inherited from AllyAgent;
    /// movement comes from the two behaviours added below, summed by the stock CooperativeArbitration.
    /// </summary>
    public class PawnAgent : AllyAgent
    {
        public GeneralAgent General { get; private set; }

        // Scratch list so we can reuse SteeringAgent.GetNearestAgent (which also skips dead agents)
        static readonly List<SteeringAgent> generalsScratch = new List<SteeringAgent>();

        protected override void InitialiseFromAwake()
        {
            // Deliberately NOT calling base: AllyAgent would add SeekToMouse, pawns follow a general instead
            gameObject.AddComponent<FollowGeneralBehaviour>();
            gameObject.AddComponent<FlockBehaviour>();
        }

        protected override void CooperativeArbitration(float deltaTime)
        {
            // Pick the nearest living general, and re-pick if ours dies
            if (General == null || General.Health <= 0.0f)
            {
                General = FindNearestGeneral();
            }

            base.CooperativeArbitration(deltaTime);
        }

        GeneralAgent FindNearestGeneral()
        {
            generalsScratch.Clear();
            foreach (var ally in GameData.Instance.allies)
            {
                if (ally is GeneralAgent)
                {
                    generalsScratch.Add(ally);
                }
            }
            return GetNearestAgent(transform.position, generalsScratch) as GeneralAgent;
        }
    }
}
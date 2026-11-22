using System.Collections.Generic;
using UnityEngine;

namespace YOUR_CODE {
    public class PawnAgent : AllyAgent
    {
        public GeneralAgent General { get; private set; }

        static readonly List<SteeringAgent> generalsScratch = new List<SteeringAgent>();

        private float orderReceivedTime = -1f;
        private float reactionDelay;
        
        private FollowGeneralBehaviour followBehaviour;
        private ProtectGeneralBehaviour protectBehaviour;

        protected override void InitialiseFromAwake()
        {
            // Deliberately NOT calling base: AllyAgent would add SeekToMouse, pawns follow a general instead
            followBehaviour = gameObject.AddComponent<FollowGeneralBehaviour>();
            protectBehaviour = gameObject.AddComponent<ProtectGeneralBehaviour>();
            protectBehaviour.enabled = false;
            gameObject.AddComponent<FlockBehaviour>();
        }

        protected override void CooperativeArbitration(float deltaTime)
        {
            if (General == null || General.Health <= 0.0f)
            {
                General = FindNearestGeneral();
            }

            bool ordered = General != null && General.IsFighting;
            followBehaviour.enabled = !ordered;       // stop trailing the general...
            protectBehaviour.enabled = ordered;        // ...and advance in front of it

            base.CooperativeArbitration(deltaTime);

            ObeyGeneral();
        }

        private void ObeyGeneral()
        {
            if (General == null || !General.IsFighting)
            {
                orderReceivedTime = -1f;
                return;
            }

            // Fresh order: small random reaction time so pawns don't all fire on the same frame
            if (orderReceivedTime < 0f)
            {
                orderReceivedTime = Time.time;
                reactionDelay = Random.Range(0.1f, 0.5f);
            }
            if (Time.time - orderReceivedTime < reactionDelay) return;

            SteeringAgent target = General.SpottedEnemy;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.z = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.001f) return;

            bool inRange = distance <= Attack.AllyGunData.range;
            bool clearShot = AllyPerceptionHelper.HasLineOfSight(
                GameData.Instance.Map, transform.position, target.transform.position);

            if (inRange && clearShot && CanAttack(Attack.AttackType.AllyGun))
            {
                transform.up = toTarget / distance;
                AttackWith(Attack.AttackType.AllyGun);
            }
        }

        protected override void UpdateDirection()
        {
            if (General != null && General.IsFighting)
            {
                Vector3 d = General.SpottedEnemy.transform.position - transform.position;
                d.z = 0f;
                if (d.sqrMagnitude > 0.001f)
                {
                    transform.up = d.normalized;
                    return;
                }
            }
            base.UpdateDirection();
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
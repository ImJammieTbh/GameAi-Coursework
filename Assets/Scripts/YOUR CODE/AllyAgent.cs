using UnityEngine;

namespace YOUR_CODE {
    public class AllyAgent : SteeringAgent
    {
        private Attack.AttackType attackType = Attack.AttackType.AllyGun;
        
        private WanderBehaviour wander;
        private SteeringAgent spottedEnemy;
        private float lastSeenTime = -999f;
        private const float AlertMemorySeconds = 0.75f;

        /// <summary>Enemy this agent currently has spotted (pawns read this from their general)</summary>
        public SteeringAgent SpottedEnemy => spottedEnemy;
        public bool HasAttackOrder => spottedEnemy != null && spottedEnemy.Health > 0f;

        protected override void InitialiseFromAwake()
        {
            gameObject.AddComponent<SeekToMouse>();
        }

        // Scouting = has WanderBehaviour and isn't being steered by the mouse
        private bool IsScouting(SeekToMouse seek)
        {
            return wander != null && (seek == null || !seek.enabled);
        }

        protected override void CooperativeArbitration(float deltaTime)
        {
            var seekToMouse = GetComponent<SeekToMouse>();
            if (wander == null) wander = GetComponent<WanderBehaviour>();

            bool scouting = IsScouting(seekToMouse);

            //Decide BEFORE base so the wander behaviour is already on/off when steering is summed
            if (scouting) UpdateSpotting(); else spottedEnemy = null;
            if (wander != null) wander.enabled = scouting && spottedEnemy == null;

            base.CooperativeArbitration(deltaTime);

            if (scouting && spottedEnemy != null)
            {
                EngageEnemy();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                attackType = Attack.AttackType.Melee;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                attackType = Attack.AttackType.AllyGun;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                attackType = Attack.AttackType.AllyRocket;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                attackType = Attack.AttackType.Grenade;
            }
            if (Input.GetKey(KeyCode.Space))
            {
                if(attackType == Attack.AttackType.AllyRocket && GameData.Instance.AllyRocketsAvailable <= 0)
                {
                    attackType = Attack.AttackType.AllyGun;
                }

                AttackWith(attackType);
            }
            
            if(Input.GetMouseButtonDown(1) && seekToMouse != null)
            {
                SteeringVelocity = Vector3.zero;
                CurrentVelocity = Vector3.zero;
                seekToMouse.enabled = !seekToMouse.enabled;
            }
        }

        private void UpdateSpotting()
        {
            if (spottedEnemy != null && spottedEnemy.Health <= 0f)
            {
                spottedEnemy = null;
            }

            var visible = AllyPerceptionHelper.FindVisibleEnemy(this);
            if (visible != null)
            {
                spottedEnemy = visible;
                lastSeenTime = Time.time;
            }
            else if (Time.time - lastSeenTime > AlertMemorySeconds)
            {
                spottedEnemy = null;
            }
        }

        private void EngageEnemy()
        {
            Vector3 toEnemy = spottedEnemy.transform.position - transform.position;
            toEnemy.z = 0.0f;
            float distance = toEnemy.magnitude;
            if (distance < 0.001f) return;

            Vector3 direction = toEnemy / distance;
            float gunRange = Attack.AllyGunData.range;

            //Hold position and let the pawns screen us. Back away if the enemy gets inside its gun range.
            const float SafeDistance = 18f;
            Vector3 desiredVelocity = distance < SafeDistance ? -direction * MaxCurrentSpeed : Vector3.zero;
            SteeringVelocity = desiredVelocity - CurrentVelocity;

            //Attacks fire along transform.up and UpdateDirection runs after this, so aim now
            transform.up = direction;

            bool clearShot = AllyPerceptionHelper.HasLineOfSight(GameData.Instance.Map, transform.position, spottedEnemy.transform.position);
            if (clearShot && distance <= gunRange && CanAttack(Attack.AttackType.AllyGun))
            {
                AttackWith(Attack.AttackType.AllyGun);
            }
        }

        protected override void UpdateDirection()
        {
            var seekToMouse = GetComponent<SeekToMouse>();

            if (IsScouting(seekToMouse))
            {
                //Engaged: already aimed at the enemy. Otherwise face travel direction.
                if (spottedEnemy == null)
                {
                    base.UpdateDirection();
                }
                return;
            }
            
            if (seekToMouse == null || seekToMouse.enabled)
            {
                base.UpdateDirection();
            }
            else
            {
                var mouseInWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseInWorld.z = 0.0f;
                transform.up = Vector3.Normalize(mouseInWorld - transform.position);
            }
        }
    }
}
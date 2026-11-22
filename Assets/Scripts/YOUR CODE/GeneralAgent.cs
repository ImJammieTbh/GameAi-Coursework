namespace YOUR_CODE {
    /// <summary>
    /// A flock leader. Wanders and scouts for enemies on its own; PawnAgents follow it and fire when it spots one.
    /// Right-click toggles between scouting and manual mouse control (attack keys still work either way).
    /// </summary>
    public class GeneralAgent : AllyAgent
    {
        protected override void InitialiseFromAwake()
        {
            base.InitialiseFromAwake();                    // adds SeekToMouse

            gameObject.AddComponent<WanderBehaviour>();    // makes AllyAgent treat this as a general

            var mouse = GetComponent<SeekToMouse>();
            if (mouse != null)
            {
                mouse.enabled = false;                     // start in wander mode
            }
        }
    }
}
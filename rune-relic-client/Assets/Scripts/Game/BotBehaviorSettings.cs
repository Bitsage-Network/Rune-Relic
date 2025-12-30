using System;

namespace RuneRelic.Game
{
    [Serializable]
    public sealed class BotBehaviorSettings
    {
        public BotTargetMode TargetMode;
        public float TargetRefreshInterval;
        public bool ChaseSmallerPlayers;
        public float ChaseRadius;
        public float ChaseSizeRatio;
        public float ChaseLeadTime;
        public bool DenyShrines;
        public float ShrineDenyRadius;
        public float ShrineBaitRadius;
        public float ShrineOrbitRadius;
        public float ShrineOrbitSpeed;
        public float ThreatAwarenessRadius;
        public float ThreatDangerRadius;
        public float ThreatLeadTime;
        public float ThreatAvoidWeight;
        public bool AvoidLargerOnly;
        public bool UseAbilityOnPanic;
        public float AbilityTriggerCooldown;
        public float SteeringAngleStep;
        public int SteeringChecks;
        public float DirectionSmoothing;
        public float InputSendRate;
    }
}

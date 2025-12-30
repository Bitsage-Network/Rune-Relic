using RuneRelic.Utils;

namespace RuneRelic.Game
{
    public interface ILocalPlayerController
    {
        void Initialize(byte[] playerId);
        void UpdateSpeed(Form form, bool hasSpeedBuff, bool hasShrineSpeed);
        void UpdateRadius(float radius);
        void SetSpawnZone(int spawnZoneId, bool active);
        void UpdateAbilityCooldown(float cooldownSeconds);
    }
}

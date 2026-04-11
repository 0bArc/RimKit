using RimWorld;
using UnityEngine;
using Verse;

namespace DontEatMod
{
    public sealed class DontEatSettings : ModSettings
    {
        public const float DefaultFoodThreshold = 10f;
        public const float DefaultEmergencyHungerPercent = 0.12f;
        public const float DefaultDebugCooldownSeconds = 5f;

        public float foodThreshold = DefaultFoodThreshold;
        public float emergencyHungerPercent = DefaultEmergencyHungerPercent;
        public float debugMessageCooldownSeconds = DefaultDebugCooldownSeconds;
        public bool debugLoggingEnabled;
        public bool debugInGameMessages;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref foodThreshold, "foodThreshold", DefaultFoodThreshold);
            Scribe_Values.Look(ref emergencyHungerPercent, "emergencyHungerPercent", DefaultEmergencyHungerPercent);
            Scribe_Values.Look(ref debugMessageCooldownSeconds, "debugMessageCooldownSeconds", DefaultDebugCooldownSeconds);
            Scribe_Values.Look(ref debugLoggingEnabled, "debugLoggingEnabled", false);
            Scribe_Values.Look(ref debugInGameMessages, "debugInGameMessages", false);
        }

        public void ResetToDefaults()
        {
            foodThreshold = DefaultFoodThreshold;
            emergencyHungerPercent = DefaultEmergencyHungerPercent;
            debugMessageCooldownSeconds = DefaultDebugCooldownSeconds;
            debugLoggingEnabled = false;
            debugInGameMessages = false;
        }
    }

    public sealed class DontEatMod : Mod
    {
        public static DontEatMod Instance;
        public static DontEatSettings Settings;

        public DontEatMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<DontEatSettings>();
            Log.Message("[DontEat] Mod settings initialized.");
        }

        public override string SettingsCategory() => "Don't Eat";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings ??= GetSettings<DontEatSettings>();

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Food reserve block threshold: {Settings.foodThreshold:0.0}");
            Settings.foodThreshold = listing.Slider(Settings.foodThreshold, 0f, 100f);
            listing.Label("Colonists will refuse new food jobs when total human-edible nutrition is below this value.");
            listing.Gap(8f);

            listing.Label($"Emergency hunger override: {Settings.emergencyHungerPercent * 100f:0}%");
            Settings.emergencyHungerPercent = listing.Slider(Settings.emergencyHungerPercent, 0f, 0.4f);
            listing.Label("If a colonist food need drops below this %, they can still eat even under rationing.");
            listing.GapLine();

            listing.CheckboxLabeled("Enable debug logging", ref Settings.debugLoggingEnabled, "Write DontEat decision logs to RimWorld log output.");
            listing.CheckboxLabeled("Show in-game debug messages", ref Settings.debugInGameMessages, "Shows periodic debug messages in-game. Can be noisy.");

            if (Settings.debugInGameMessages)
            {
                listing.Gap(6f);
                listing.Label($"Debug message cooldown: {Settings.debugMessageCooldownSeconds:0.0}s");
                Settings.debugMessageCooldownSeconds = listing.Slider(Settings.debugMessageCooldownSeconds, 0f, 30f);
            }

            listing.GapLine();
            if (listing.ButtonText("Reset settings to defaults"))
            {
                Settings.ResetToDefaults();
            }

            listing.End();
        }
    }
}
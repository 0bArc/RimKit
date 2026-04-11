using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace DontEatMod
{
    public static class DontEatDebugUI
    {
        public static void CheckHotkeyAndToggle()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            bool keyDefPressed = DontEatKeyBindingDefOf.DontEat_ToggleDevWindow?.JustPressed == true;
            bool fallbackPressed = UnityEngine.Input.GetKey(KeyCode.LeftControl) && UnityEngine.Input.GetKeyDown(KeyCode.F8);
            if (keyDefPressed || fallbackPressed)
            {
                ToggleWindow();
            }
        }

        public static void ToggleWindow()
        {
            Window existing = Find.WindowStack.WindowOfType<DontEatDebugWindow>();
            if (existing != null)
            {
                existing.Close(false);
                return;
            }

            Find.WindowStack.Add(new DontEatDebugWindow());
        }
    }

    public sealed class DontEatDebugWindow : Window
    {
        public override Vector2 InitialSize => new Vector2(580f, 620f);

        public DontEatDebugWindow()
        {
            doCloseButton = true;
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var settings = DontEatMod.Settings;
            if (settings == null)
            {
                Widgets.Label(inRect, "DontEat settings are not initialized yet.");
                return;
            }

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Don't Eat - Dev GUI");
            string keyText = DontEatKeyBindingDefOf.DontEat_ToggleDevWindow?.MainKeyLabel ?? "F8";
            listing.Label($"Hotkey: {keyText}");
            listing.GapLine();

            var map = Find.CurrentMap;
            if (map == null)
            {
                listing.Label("No active map. Load into a colony to use food controls.");
            }
            else
            {
                float reserves = map.resourceCounter?.TotalHumanEdibleNutrition ?? 0f;
                listing.Label($"Current map edible nutrition: {reserves:0.0}");

                Pawn selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
                if (selectedPawn != null)
                {
                    float selectedFood = selectedPawn.needs?.food?.CurLevelPercentage ?? 0f;
                    listing.Label($"Selected pawn food: {selectedFood:P0} ({selectedPawn.LabelShort})");
                }
                else
                {
                    listing.Label("Selected pawn food: (select a pawn)");
                }

                if (listing.ButtonText("Recount map food now"))
                {
                    map.resourceCounter?.UpdateResourceCounts();
                    Messages.Message("[DontEat] Map food recounted.", MessageTypeDefOf.TaskCompletion, false);
                }

                if (listing.ButtonText("Spawn 10 simple meals near selected pawn"))
                {
                    SpawnSimpleMeals(selectedPawn, map, 10);
                }
            }

            listing.GapLine();
            listing.Label($"Food reserve block threshold: {settings.foodThreshold:0.0}");
            settings.foodThreshold = listing.Slider(settings.foodThreshold, 0f, 100f);

            listing.Label($"Emergency hunger override: {settings.emergencyHungerPercent * 100f:0}%");
            settings.emergencyHungerPercent = listing.Slider(settings.emergencyHungerPercent, 0f, 0.4f);

            listing.CheckboxLabeled("Enable debug logging", ref settings.debugLoggingEnabled);
            listing.CheckboxLabeled("Show in-game debug messages", ref settings.debugInGameMessages);

            if (settings.debugInGameMessages)
            {
                listing.Label($"Debug message cooldown: {settings.debugMessageCooldownSeconds:0.0}s");
                settings.debugMessageCooldownSeconds = listing.Slider(settings.debugMessageCooldownSeconds, 0f, 30f);
            }

            if (listing.ButtonText("Set threshold = current map food"))
            {
                var activeMap = Find.CurrentMap;
                if (activeMap?.resourceCounter != null)
                {
                    settings.foodThreshold = (float)Math.Round(activeMap.resourceCounter.TotalHumanEdibleNutrition, 1);
                }
            }

            if (listing.ButtonText("Save settings"))
            {
                DontEatMod.Instance?.WriteSettings();
                Messages.Message("[DontEat] Settings saved.", MessageTypeDefOf.TaskCompletion, false);
            }

            if (listing.ButtonText("Reset settings to defaults"))
            {
                settings.ResetToDefaults();
            }

            listing.End();
        }

        private static void SpawnSimpleMeals(Pawn selectedPawn, Map map, int amount)
        {
            if (selectedPawn == null)
            {
                Messages.Message("[DontEat] Select a pawn first.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (map == null)
            {
                Messages.Message("[DontEat] No active map.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int left = Math.Max(0, amount);
            while (left > 0)
            {
                Thing meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                int stack = Math.Min(left, meal.def.stackLimit);
                meal.stackCount = stack;
                GenPlace.TryPlaceThing(meal, selectedPawn.Position, map, ThingPlaceMode.Near);
                left -= stack;
            }

            map.resourceCounter?.UpdateResourceCounts();
            Messages.Message($"[DontEat] Spawned {amount} simple meals near {selectedPawn.LabelShort}.", MessageTypeDefOf.TaskCompletion, false);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.pawns gear and policy: equipment, apparel, inventory, outfits, drug and food policies, work settings,
    // beds, timetables for groups. Ops are pawn.*.
    internal static class ApiGear
    {
        public static void Register()
        {
            R("pawn.gear", Gear);
            R("pawn.equip", Equip);
            R("pawn.unequip", Unequip);
            R("pawn.wear", Wear);
            R("pawn.remove_apparel", RemoveApparel);
            R("pawn.add_to_inventory", AddToInventory);
            R("pawn.drop", Drop);
            R("pawn.outfit", OutfitOf);
            R("pawn.set_outfit", SetOutfit);
            R("pawn.outfits", Outfits);
            R("pawn.drug_policy", DrugPolicyOf);
            R("pawn.set_drug_policy", SetDrugPolicy);
            R("pawn.drug_policies", DrugPolicies);
            R("pawn.food_policy", FoodPolicyOf);
            R("pawn.set_food_policy", SetFoodPolicy);
            R("pawn.food_policies", FoodPolicies);
            R("pawn.work_priorities", WorkPriorities);
            R("pawn.bed", BedOf);
            R("pawn.assign_bed", AssignBed);
            R("pawn.unassign_bed", UnassignBed);
            R("pawn.set_assignments", SetAssignments);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.6.0");

        private static string Bad() => Fail("RK2001", "pawn handle is stale or null");

        private static Jb ThingJson(Thing t)
        {
            var j = Jb.Obj().H("thing", t).S("def", t.def.defName).S("label", t.LabelCap).I("count", t.stackCount).I("hp", t.HitPoints).I("max_hp", t.MaxHitPoints);
            if (t.TryGetQuality(out QualityCategory q)) j.S("quality", q.ToString());
            return j;
        }

        private static string Gear(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad();
            var j = Jb.Obj();
            if (p.equipment?.Primary != null) j.Raw("weapon", ThingJson(p.equipment.Primary).ToString());
            var worn = Jb.Arr();
            if (p.apparel != null) foreach (Apparel ap in p.apparel.WornApparel) worn.Add(ThingJson(ap));
            j.Raw("apparel", worn.ToString());
            var inv = Jb.Arr();
            if (p.inventory != null) foreach (Thing t in p.inventory.innerContainer) inv.Add(ThingJson(t));
            j.Raw("inventory", inv.ToString());
            return j.Ok();
        }

        private static string Equip(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.equipment == null) return Bad();
            var t = ObjectHandles.Get<Thing>(Int(a, "thing")) as ThingWithComps;
            if (t == null) return Fail("RK2001", "thing handle is stale or not a piece of equipment");
            if (t.Spawned) t.DeSpawn();
            else t.holdingOwner?.Remove(t);
            p.equipment.AddEquipment(t);
            return OkBool(p.equipment.Primary == t);
        }

        private static string Unequip(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.equipment?.Primary == null) return Bad();
            return OkBool(p.equipment.TryDropEquipment(p.equipment.Primary, out ThingWithComps dropped, p.Position, true));
        }

        private static string Wear(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.apparel == null) return Bad();
            var t = ObjectHandles.Get<Thing>(Int(a, "thing")) as Apparel;
            if (t == null) return Fail("RK2001", "thing handle is stale or not apparel");
            if (!ApparelUtility.HasPartsToWear(p, t.def)) return Fail("RK1001", "this pawn cannot wear " + t.def.defName);
            if (t.Spawned) t.DeSpawn();
            else t.holdingOwner?.Remove(t);
            p.apparel.Wear(t, true);
            return OkBool(p.apparel.WornApparel.Contains(t));
        }

        private static string RemoveApparel(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.apparel == null) return Bad();
            var t = ObjectHandles.Get<Thing>(Int(a, "thing")) as Apparel;
            if (t == null) return Fail("RK2001", "thing handle is stale or not apparel");
            return OkBool(p.apparel.TryDrop(t, out Apparel dropped));
        }

        private static string AddToInventory(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.inventory == null) return Bad();
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            if (t.Spawned) t.DeSpawn();
            return OkBool(p.inventory.innerContainer.TryAdd(t, true));
        }

        private static string Drop(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.inventory == null || p.Map == null) return Bad();
            Thing t = ObjectHandles.Get<Thing>(Int(a, "thing"));
            if (t == null) return Fail("RK2001", "thing handle is stale or null");
            return OkBool(p.inventory.innerContainer.TryDrop(t, p.Position, p.Map, ThingPlaceMode.Near, out Thing result));
        }

        private static string OutfitOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.outfits == null) return Bad();
            return OkStr(p.outfits.CurrentApparelPolicy?.label ?? "");
        }

        private static string SetOutfit(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.outfits == null) return Bad();
            string name = Str(a, "name");
            ApparelPolicy policy = Current.Game.outfitDatabase.AllOutfits.FirstOrDefault(x => string.Equals(x.label, name, StringComparison.OrdinalIgnoreCase));
            if (policy == null) return Fail("RK3001", "no outfit named " + name);
            p.outfits.CurrentApparelPolicy = policy;
            return OkStr(policy.label);
        }

        private static string Outfits(Dictionary<string, string> a) => OkStringList(Current.Game.outfitDatabase.AllOutfits.Select(x => x.label).ToList());

        private static string DrugPolicyOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.drugs == null) return Fail("RK3003", "this pawn has no drug policy");
            return OkStr(p.drugs.CurrentPolicy?.label ?? "");
        }

        private static string SetDrugPolicy(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.drugs == null) return Fail("RK3003", "this pawn has no drug policy");
            string name = Str(a, "name");
            DrugPolicy policy = Current.Game.drugPolicyDatabase.AllPolicies.FirstOrDefault(x => string.Equals(x.label, name, StringComparison.OrdinalIgnoreCase));
            if (policy == null) return Fail("RK3001", "no drug policy named " + name);
            p.drugs.CurrentPolicy = policy;
            return OkStr(policy.label);
        }

        private static string DrugPolicies(Dictionary<string, string> a) => OkStringList(Current.Game.drugPolicyDatabase.AllPolicies.Select(x => x.label).ToList());

        private static string FoodPolicyOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.foodRestriction == null) return Fail("RK3003", "this pawn has no food policy");
            return OkStr(p.foodRestriction.CurrentFoodPolicy?.label ?? "");
        }

        private static string SetFoodPolicy(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.foodRestriction == null) return Fail("RK3003", "this pawn has no food policy");
            string name = Str(a, "name");
            FoodPolicy policy = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrDefault(x => string.Equals(x.label, name, StringComparison.OrdinalIgnoreCase));
            if (policy == null) return Fail("RK3001", "no food policy named " + name);
            p.foodRestriction.CurrentFoodPolicy = policy;
            return OkStr(policy.label);
        }

        private static string FoodPolicies(Dictionary<string, string> a) => OkStringList(Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(x => x.label).ToList());

        private static string WorkPriorities(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.workSettings == null) return Fail("RK3003", "this pawn has no work settings");
            var arr = Jb.Arr();
            foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                arr.Add(Jb.Obj().S("def", w.defName).S("label", w.labelShort).I("priority", p.WorkTypeIsDisabled(w) ? 0 : p.workSettings.GetPriority(w)).B("disabled", p.WorkTypeIsDisabled(w)));
            }

            return arr.Ok();
        }

        private static string BedOf(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ownership == null) return Bad();
            return OkHandle(p.ownership.OwnedBed);
        }

        private static string AssignBed(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ownership == null) return Bad();
            var bed = ObjectHandles.Get<Thing>(Int(a, "bed")) as Building_Bed;
            if (bed == null) return Fail("RK2001", "bed handle is stale or not a bed");
            return OkBool(p.ownership.ClaimBedIfNonMedical(bed));
        }

        private static string UnassignBed(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p?.ownership == null) return Bad();
            p.ownership.UnclaimBed();
            return OkBool(true);
        }

        // Sets one timetable hour for a group of pawns. pawns: list of pawn handles. hour: 0 to 23. def: Anything, Work, Joy, Sleep, Meditate.
        private static string SetAssignments(Dictionary<string, string> a)
        {
            Opts o = Opts.From(a, "pawns");
            int hour = Int(a, "hour");
            if (hour < 0 || hour > 23) return Fail("RK1001", "hour must be 0 to 23");
            TimeAssignmentDef def = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown assignment " + Str(a, "def"));
            int changed = 0;
            if (a.TryGetValue("pawns", out string json) && Json.TryParse(json, out object parsed) && parsed is List<object> list)
            {
                foreach (object item in list)
                {
                    Pawn p = Opts.HandleOf<Pawn>(item);
                    if (p?.timetable == null) continue;
                    p.timetable.SetAssignment(hour, def);
                    changed++;
                }
            }

            return OkInt(changed);
        }
    }
}

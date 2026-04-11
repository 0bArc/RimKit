using RimWorld;
using Verse;

namespace DontEatMod
{
    [DefOf]
    public static class DontEatKeyBindingDefOf
    {
        public static KeyBindingDef DontEat_ToggleDevWindow;

        static DontEatKeyBindingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DontEatKeyBindingDefOf));
        }
    }
}

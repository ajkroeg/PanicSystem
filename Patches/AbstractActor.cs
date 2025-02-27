using BattleTech;
using Harmony;
using PanicSystem.Components;
using static PanicSystem.Components.Controller;
using static PanicSystem.PanicSystem;

// ReSharper disable InconsistentNaming

namespace PanicSystem.Patches
{
    [HarmonyPatch(typeof(AbstractActor), "OnActivationBegin")]
    public static class AbstractActor_OnActivationBegin_Patch
    {
        public static void Prefix(AbstractActor __instance)
        {
            TurnDamageTracker.newTurnFor(__instance);
        }
    }

    [HarmonyPatch(typeof(AbstractActor), "OnActivationEnd")]
    public static class AbstractActor_OnActivationEnd_Patch
    {
        public static void Prefix(AbstractActor __instance)
        {
            var pilot = __instance.GetPilot();
            if (pilot == null)
            {
                modLog.LogReport($"No pilot found for {__instance.Nickname}:{__instance.GUID}");
                return;
            }
            modLog.LogReport($"Processing {__instance.Nickname}:{__instance.GUID}");
            if ((pilot.StatCollection.GetValue<float>("BleedingRate") * pilot.StatCollection.GetValue<float>("BleedingRateMulti")) > 0)
            {
                modLog.LogReport($"Pilot is bleeding, forcing panic check here.");
                DamageHandler.ProcessBatchedTurnDamage(__instance);
            }

            TurnDamageTracker.completedTurnFor(__instance);
            if (__instance.IsDead || __instance.IsFlaggedForDeath && __instance.HasHandledDeath)
            {
                return;
            }

            var index = GetActorIndex(__instance);

            modLog.LogReport($"Checking pilot panic for {__instance.Nickname}:{__instance.GUID} recent panic{TrackedActors[index].PanicWorsenedRecently} {TrackedActors[index].PanicStatus}" +
                $" Health:{Helpers.ActorHealth(__instance):F3}% v/s {(modSettings.MechHealthForCrit + (((int) TrackedActors[index].PanicStatus) * 10))} " +
                $"Alone:{__instance.Combat.GetAllAlliesOf(__instance).TrueForAll(m => m.IsDead || m == __instance)}");

            // reduce panic level
            //fix https://github.com/gnivler/PanicSystem/issues/54
            //dont improve panic system if damage level>crit health+ panicstatus*10%
            if (!TrackedActors[index].PanicWorsenedRecently &&
                TrackedActors[index].PanicStatus > PanicStatus.Confident && Helpers.ActorHealth(__instance)> (modSettings.MechHealthForCrit+(((int)TrackedActors[index].PanicStatus)*10)) && !__instance.Combat.GetAllAlliesOf(__instance).TrueForAll(m => m.IsDead || m == __instance))
            {
                modLog.LogReport($"Improving pilot panic for {__instance.Nickname}:{__instance.GUID}");
                TrackedActors[index].PanicStatus--;
            }

            TrackedActors[index].PanicWorsenedRecently = false;
            SaveTrackedPilots();
        }
    }
}

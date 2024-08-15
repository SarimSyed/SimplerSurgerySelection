
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace SimplerSurgery
{
    [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffRow")]
    public class InjuryClickPatch
    {
        public static void PostFix(Rect rect,Pawn pawn, Hediff hediff)
        {
            try
            {
            if (Widgets.ButtonInvisible(rect))
            {
                Log.Message($"Injury has been clicked!: {hediff.LabelCap} (patch works so far)");
            }

            }
            catch (System.Exception e)
            {

                Log.Message("Error in PostFix method: " + e);
            }
        }

    }
}
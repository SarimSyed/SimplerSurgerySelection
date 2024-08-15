using Verse;


namespace SimplerSurgery
{
    public class Settings : ModSettings
    {
        public static bool SuggestDrugs = true;
        public static bool ShowAllHostiles = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref SuggestDrugs, "SuggestDrugs", true);
            Scribe_Values.Look(ref ShowAllHostiles, "ShowAllHostiles", false);
        }
    }
}

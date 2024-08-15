using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;


namespace SimplerSurgery
{
    [StaticConstructorOnStartup]
    public static class ModInitializer
    {
        static ModInitializer()
        {
            Log.Message("Static Project Mod Class Loaded");

            Harmony harmony = new Harmony("rimworld.mod.tallermage.surgery");
            harmony.PatchAll();
        }
    }

    //inherited from mod
    public class ProjectModConfig : Mod
    {
        public ProjectModConfig(ModContentPack content) : base(content)
        {
            Log.Message($"Mod initialized through :Mod method");
            Harmony harmony = new Harmony("rimworld.mod.cssen.projectmod");
            harmony.PatchAll();
        }
    }
    //TargetMethod is used to access a private method which i needed to set up a 
    //"ClickHandler"

    [HarmonyPatch(typeof(HealthCardUtility))]
    public static class HealthCardUtility_Click_Patch
    {
        //[HarmonyPatch(nameof(HealthCardUtility), nameof(HealthCardUtility.)]


        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HealthCardUtility), "EntryClicked", new[] { typeof(IEnumerable<Hediff>), typeof(Pawn) });
        }

        [HarmonyPostfix]
        public static void Postfix(IEnumerable<Hediff> diffs, Pawn pawn)
        {

            try
            {
                foreach (var hediff in diffs)
                {
                    Log.Message($"Injury Clicked: {hediff.LabelCap}");

                    //Surgery dropdown

                    InvokeGenerateSurgeryOptions(pawn, hediff);
                   
                    Log.Message("FloatMenu added to stack");
                    //Find.WindowStack.Add(new InjuryWindow(hediff));

                }

            }
            catch (System.Exception ex)
            {
                
                Log.Error($"Error thrown in the healthcard click patch: {ex.Message}");
            }


        }
        private static FloatMenuOption GenerateSurgeryOptions(Pawn pawn, Hediff hediff)
        {
            string label = $"Perform surgery on {hediff.LabelCap}";
            System.Action action = delegate
            {
                Log.Message($"Surgery option selected for {hediff.LabelCap} on {pawn.Name}");
            };
            return new FloatMenuOption(label, action);
        }


        //Absolute terribly written and formatted code but its my first "Advance mod"
        //This was just a learning experience so I should focus more on the formatting in the future
        private static void InvokeGenerateSurgeryOptions(Pawn pawn, Hediff hediff)
        {
            Log.Warning($"All hediff info: {hediff}");
            Log.Message($"Pawn: {pawn}");
            int num = 0;
            //Getting a list of all recipes and filtering it based on the body parts
            IEnumerable<RecipeDef> recipes
                = pawn.def.AllRecipes.Where(r => r.AvailableNow && r.targetsBodyPart && NotMissingVitalIngredient(pawn, r) && r.appliedOnFixedBodyParts.Contains(hediff.Part.def));
            Log.Message($"Hediff Part: {hediff.Part.def} \n just the Part: {hediff.Part}");
            if(recipes == null)
            {
                Log.Warning($"recipes is null: {recipes}");
                return;
            }
            Log.Warning($"RecipeDef: {recipes.Count()}");
            
            //Exposing a private method to generate surgery bills
            MethodInfo generateSurgeryBillMethod =
                typeof(HealthCardUtility).GetMethod("GenerateSurgeryOption", BindingFlags.NonPublic | BindingFlags.Static);
            //Empty list of FloatMenuOption, this is the dropdown and each option is an option in the dropdown
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            Log.Warning($"generateSurgeryBillMethod: {options}");
            if (generateSurgeryBillMethod != null)
            {
                Log.Message($"Recipes: {recipes}");
                Log.Message("GenerateSurgeryOptions method invoked.");
                foreach (RecipeDef recipe in recipes)
                {
                    Log.Message($"recipe: {recipe}");
                    List<ThingDef> missingIngredients =
                        recipe.PotentiallyMissingIngredients(null, pawn.Map).ToList();
                    AcceptanceReport report = recipe.Worker.AvailableReport(pawn);
                    Log.Message($"Report: {report}");

                    //ACCEPTANCE REPORT ACCEPTED
                    if (report.Accepted || !report.Reason.NullOrEmpty())
                    {
                        //I think this is filtering the recipe thingDef based on whther if its a drug or recipes 
                        //used in surgeries
                        IEnumerable<ThingDef> enumerable = recipe.PotentiallyMissingIngredients(null, pawn.Map);
                        Log.Message($"enumerable: {enumerable}");
                        if (!enumerable.Any((ThingDef x) => x.isTechHediff) && !enumerable.Any((ThingDef x) => x.IsDrug &&
                        (!enumerable.Any() || !recipe.dontShowIfAnyIngredientMissing))){

                            Log.Message($"Targets Body Part: {recipe.targetsBodyPart}");
                        if (recipe.targetsBodyPart)
                        {
                                Log.Message("Inside the targetsBodyPart if statement");
                            foreach (BodyPartRecord item in recipe.Worker.GetPartsToApplyOn(pawn, recipe))
                            {

                                if (recipe.AvailableOnNow(pawn, item))
                                {
                                        options.Add((FloatMenuOption)generateSurgeryBillMethod.Invoke(null, new object[]
                                        {
                                        pawn, pawn, recipe, enumerable, report, num++, item
                                        }));
                                        Log.Message($"Added menu option for recipe: {recipe.defName}");

                                 }

                            }
                        }
                    }

                    }

                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("None".Translate(), null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Log.Error("GenerateSurgeryOptions method not found");
                
            }
        }
        public static bool IsHediffThatReducesPain(this HediffDef hediffDef)
        {
            if (hediffDef?.stages.NullOrEmpty() ?? true)
            {
                return false;
            }

            return hediffDef.stages?.Any(hs => hs.painFactor < 1f || hs.painOffset < 0f) ?? false;
        }
        public static bool AddsHediffThatReducesPain(this RecipeDef r)
        {
            return r.addsHediff.IsHediffThatReducesPain();
        }
        public static bool AdministersDrugThatReducesPain(this RecipeDef r)
        {
            if (r.ingredients.NullOrEmpty())
            {
                return false;
            }

            return r.ingredients[0].filter.BestThingRequest.singleDef.ReducesPainOnIngestion();
        }
        public static bool ReducesPainOnIngestion(this ThingDef def)
        {
            return
                def?.ingestible?.outcomeDoers?.OfType<IngestionOutcomeDoer_GiveHediff>()
                    .Any(od => od.hediffDef.IsHediffThatReducesPain()) ?? false;
        }
        public static bool NotMissingVitalIngredient(Pawn pawn, RecipeDef r)
        {
            return !r.PotentiallyMissingIngredients(null, pawn.Map).Any();
        }
    }
}

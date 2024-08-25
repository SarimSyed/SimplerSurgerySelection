using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace SimplerSurgery
{
    #region Initializing patch
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
        public ProjectModConfig(ModContentPack content)
            : base(content)
        {
            Log.Message($"Simpler Surgery Shortcut initialized through :Mod method");
            Harmony harmony = new Harmony("rimworld.mod.cssen.projectmod");
            harmony.PatchAll();
        }
    }
    #endregion

    #region Creating Surgery Bill
    //TargetMethod is used to access a private method which i needed to set up a
    //"ClickHandler"

    [HarmonyPatch(typeof(HealthCardUtility))]
    public static class HealthCardUtility_Click_Patch
    {
        const char  SPLIT_MATCHER = ' ';

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(HealthCardUtility),
                "EntryClicked",
                new[] { typeof(IEnumerable<Hediff>), typeof(Pawn) }
            );
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
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error thrown in the healthcard click patch: {ex.Message}");
            }
        }

        //Absolute terribly written and formatted code but its my first "Advance mod"
        //This was just a learning experience so I should focus more on the formatting in the future
        private static void InvokeGenerateSurgeryOptions(Pawn pawn, Hediff hediff)
        {

            //Log.Warning($"All hediff info: {hediff}");
            if (hediff == null)
            {
                Log.Message($"Pawn is null: {pawn}");
                return;
            }
            if (hediff.Part == null)
            {
                Log.Message($"PartL: {hediff.Part}");
                return;
            }
            if (pawn.def.AllRecipes == null)
            {
                Log.Message($"AllRecipes is null: {pawn.def.AllRecipes.Count()}");
                return;
            }
            if (hediff.Part.def == null)
            {
                Log.Message($"part.def is null: {hediff.Part.def}");
                return;
            }

            //This should have an array with 2 elements, the side and the part. It will just be 1 if its something like a kidney
            string[] partDetails = hediff.Label.ToLower().Split(SPLIT_MATCHER);
            int num = 0;
            //Getting a list of all recipes and filtering it based on the body parts

            //Maybe dont filter based on prosthetics and bionic arm and do it afterwards

            IEnumerable<RecipeDef> recipes = pawn.def.AllRecipes.Where(r =>
                (
                    r.targetsBodyPart
                    && r.appliedOnFixedBodyParts.Contains(hediff.Part.def)
                )
                || (IsRelatedBodyPart(r.appliedOnFixedBodyParts, hediff.Part.def)
                


                )
            );

            Log.Message($"Hediff Part: {hediff.Part.def} \n just the Part: {hediff.Part}");
            if (recipes == null)
            {
                return;
            }
            Log.Warning($"RecipeDef: {recipes.Count()}");

            //Exposing a private method to generate surgery bills
            MethodInfo generateSurgeryBillMethod = typeof(HealthCardUtility).GetMethod(
                "GenerateSurgeryOption",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            //Empty list of FloatMenuOption, this is the dropdown and each option is an option in the dropdown
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            if (generateSurgeryBillMethod != null)
            {
                foreach (RecipeDef recipe in recipes)
                {
                    //Log.Message($"recipe: {recipe.label}|| {recipe.appliedOnFixedBodyPartGroups.Count}");

                    for (int i = 0; i < recipe.appliedOnFixedBodyPartGroups.Count(); i++)
                    {
                        Log.Message($"-->   aplliedOnGroup: {recipe.appliedOnFixedBodyPartGroups[i]}");
                    }

                    List<ThingDef> missingIngredients = recipe
                        .PotentiallyMissingIngredients(null, pawn.Map)
                        .ToList();
                    AcceptanceReport report = recipe.Worker.AvailableReport(pawn);

                    //ACCEPTANCE REPORT ACCEPTED
                    if (report.Accepted || !report.Reason.NullOrEmpty())
                    {
                        //I think this is filtering the recipe thingDef based on whther if its a drug or recipes
                        //used in surgeries
                        IEnumerable<ThingDef> enumerable = recipe.PotentiallyMissingIngredients(
                            null,
                            pawn.Map
                        );
                        if (
                            !enumerable.Any((ThingDef x) => x.isTechHediff)
                            && !enumerable.Any(
                                (ThingDef x) =>
                                    x.IsDrug
                                    && (!enumerable.Any() || !recipe.dontShowIfAnyIngredientMissing)//Is this whats filtering the operations that arent available
                            )
                        )
                        {
                            if (recipe.targetsBodyPart)
                            {
                                foreach (
                                    //Maybe add a check for 'left' or 'right' here
                                    BodyPartRecord item in recipe.Worker.GetPartsToApplyOn(
                                        pawn,
                                        recipe
                                    )
                                )
                                {
                                    if (recipe.AvailableOnNow(pawn, item))
                                    {



                                        //item keeps record of part similar to hediff.Part
                                        var hediffPart = hediff.Part.Label.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        string[] currentPart = item.Label.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (currentPart.Count() == 2)
                                        {
                                            //This means that its a part with a side
                                            if (currentPart[0] != hediffPart[0])
                                            {
                                                continue;
                                            }
                                        }
                                            options.Add(
                                            (FloatMenuOption)
                                                generateSurgeryBillMethod.Invoke(
                                                    null,
                                                    new object[]
                                                    {
                                                        pawn,
                                                        pawn,
                                                        recipe,
                                                        enumerable,
                                                        report,
                                                        num++,
                                                        item
                                                    }
                                                )
                                        );


                                        
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
        #endregion

        
       
        #region Helper functions
        
        private static bool IsRelatedBodyPart(
            IEnumerable<BodyPartDef> bodyParts,
            BodyPartDef targetPart
        )
        {
            // Define related body parts logic
            var relatedParts = new Dictionary<string, List<string>>
            {
                {
                    "Hand",
                    new List<string> { "Arm", "Shoulder" }
                },
                {
                    "Foot",
                    new List<string> { "Leg", "Hip" }
                },
                {
                    "Arm",
                    new List<string> { "Shoulder" }
                },
                {
                    "Leg",
                    new List<string> { "Hip" }
                },
                {
                    "Finger",
                    new List<string> { "Hand", "Shoulder", "Arm" }
                },
                // Add more related parts as needed
            };

            foreach (var bp in bodyParts)
            {
                if (relatedParts.TryGetValue(targetPart.defName, out var related))
                {
                    if (related.Contains(bp.defName))
                    {
                        //This is not being logged meaning the function is not reaching this point
                        Log.Warning($"Inside IsRelatedBodyPart: {bp.defName} || {bp.label}");
                        return true;
                    }
                }
            }

            return false;
        }
        //Helper function returning if part is limb or not
        private static bool IsLimb(BodyPartGroupDef part)
        {
            // Example check for limbs
            var limbKeywords = new List<string>
            {
                "arm",
                "leg",
                "hand",
                "foot"
                // Add more limb keywords as needed
            };
            
            return limbKeywords.Any(keyword => part.label.ToLower().Contains(keyword));
        }

        //Helper function returning if its an organ or not
        private static bool IsOrgan(BodyPartGroupDef part)
        {
            // Example check for organs
            var organKeywords = new List<string>
            {
                "heart",
                "lung",
                "kidney",
                "liver",
                "heart"
                // Add more organ keywords as needed
            };

            return organKeywords.Any(keyword => part.label.ToLower().Contains(keyword));
        }

        public static bool IsHediffThatReducesPain(this HediffDef hediffDef)
        {
            if (hediffDef?.stages.NullOrEmpty() ?? true)
            {
                return false;
            }

            return hediffDef.stages?.Any(hs => hs.painFactor < 1f || hs.painOffset < 0f) ?? false;
        }

        public static bool ReducesPainOnIngestion(this ThingDef def)
        {
            return def?.ingestible?.outcomeDoers?.OfType<IngestionOutcomeDoer_GiveHediff>()
                    .Any(od => od.hediffDef.IsHediffThatReducesPain()) ?? false;
        }

        public static bool NotMissingVitalIngredient(Pawn pawn, RecipeDef r)
        {
            return !r.PotentiallyMissingIngredients(null, pawn.Map).Any();
        }

        public static IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipeDef)
        {
            return recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef);
        }
        #endregion
    }

    
}
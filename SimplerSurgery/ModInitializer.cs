using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

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
            LogHediffDetails(hediff);
            int num = 0;
            //Getting a list of all recipes and filtering it based on the body parts

            //Maybe dont filter based on prosthetics and bionic arm and do it afterwards

            IEnumerable<RecipeDef> recipes = pawn.def.AllRecipes.Where(r =>
                (
                    r.targetsBodyPart
                    && r.appliedOnFixedBodyParts.Contains(hediff.Part.def)
                )
                || (IsRelatedBodyPart(r.appliedOnFixedBodyParts, hediff.Part.def)
                || (r.defName.ToLower().Contains("remove") 
                && r.defName.ToLower().Contains("install")))
                && ( //Rewrite logic here so that organs are not filtered out.
                r.appliedOnFixedBodyPartGroups.Any(bp => IsLimb(bp))
                || r.appliedOnFixedBodyPartGroups.Any(g => IsOrgan(g))

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
                                    && (!enumerable.Any() || !recipe.dontShowIfAnyIngredientMissing)
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

        
        private static void LogHediffDetails(Hediff hediff)
        {
            string position = string.Empty;

            if (hediff == null)
            {
                Log.Message("Hediff is null.");
                return;
            }

            BodyPartRecord part = hediff.Part;
            if (part == null)
            {
                Log.Message("BodyPartRecord is null.");
                return;
            }

            try
            {

                // Save the literal string inside the position variable before logging
                position = $"Hediff: {hediff.def.label} ({hediff.def.defName})";
                Log.Message(position);

                position = $"Body Part: {part.Label} ({part.def.defName})";
                Log.Message(position);

                position = $"Part Index: {part.Index}";
                Log.Message(position);

                position = $"Part Coverage: {part.coverage}";
                Log.Message(position);

                position = $"Part Depth: {part.depth}";
                Log.Message(position);

                position = $"Part Height: {part.height}";
                Log.Message(position);

                position = $"Part Parent: {part.parent?.Label ?? "None"}";
                Log.Message(position);

                position = $"Part Groups: {string.Join(", ", part.groups.Select(g => g.defName))}";
                Log.Message(position);

            }
            catch (System.Exception ex)
            {

                Log.Error($"Unable to read one of the hediff values at: {position} \nError:{ex.Message}");
            }


        }
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

    //There for debugging purpose as well as understanding how surgery bills work
    //REMEMBER TO DELETE BEFORE PUBLISHING
    [HarmonyPatch]
    public static class PrintSurgeryBillMethod
    {
        [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
        [HarmonyPostfix]
        public static void BillCreated(Bill bill)
        {
            if (bill == null)
            {
                throw new System.Exception("Medical Bill is null");
            }
            Log.Warning($"bill: {bill} ");
            //Log.Message($"() Bill created: {bill.Label}\n {bill.LabelCap}");
            if (bill is Bill_Medical medical)
            {
                if (medical != null)
                {
                    BodyPartRecord partRecord = new BodyPartRecord();

                    try
                    {
                        Log.Warning($"Attempting to Expose data");
                        medical.ExposeData();
                        Scribe_Values.Look(ref partRecord, "part");
                        Log.Message($"Exposed Part: {partRecord}");
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Expose data function failed: {ex.Message}");
                    }

                    Log.Warning("inside if statement: medical not null");
                    Log.Message($"Medical: {medical}");
                    Log.Message($"Part: {medical.Part}");

                    try
                    {
                        Log.Warning("Accessing recipe.defName property");
                        Log.Message($"Recipe.defname: {medical.recipe.defName}");
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error accessing medical.recipe.defName: {ex.Message}");
                    }
                    try
                    {
                        Log.Warning("Accessing recipe.label property");
                        Log.Message($"Recipe.defname: {medical.recipe.label}");
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error accessing recipe.label: {ex.Message}");
                    }

                    try
                    {
                        Log.Warning("Attempting to read properties of the bill");

                        if (medical.Part == null)
                        {
                            Log.Warning("medical.Part is null");
                        }
                        else
                        {
                            Log.Message($"Part: {medical.Part}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error with accessing medical.Part : {ex.Message}");
                    }

                    try
                    {
                        Log.Warning("Trying to access label");
                        if (medical.Label == null)
                        {
                            Log.Warning("medical label is null");
                        }
                        else
                        {
                            Log.Warning($"label : {medical.Label}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error with accessing medical.label : {ex.Message}");
                    }

                    //var targetPart = medical.Part;
                    //if (targetPart != null) {
                    //    Log.Message($"targetPart: {targetPart}");
                    //}
                    //if ( targetPart.Label != null)
                    //{
                    //    Log.Warning($"TargetBody: {targetPart.Label}\n" +
                    //        $"TargetDefName: {targetPart.def.defName.ToUpper()}\n" +
                    //        $"isLeft?: {targetPart.def.defName.ToLower().Contains("left")}");
                    //}
                }
            }
        }
    }
}
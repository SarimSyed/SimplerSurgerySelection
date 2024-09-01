using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Steamworks;
using UnityEngine.Analytics;
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
        const char SPLIT_MATCHER = ' ';

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
            {
                // Get all ancestors of the current part
                List<BodyPartRecord> allAncestors = GetAllAncestors(hediff.Part);

                // Check if the recipe applies to the current part or any of its ancestors
                bool appliesToCurrentOrAncestors =
                    r.appliedOnFixedBodyParts.Contains(hediff.Part.def)
                    || allAncestors.Any(ancestor =>
                        r.appliedOnFixedBodyParts.Contains(ancestor.def)
                    );

                return r.AvailableNow
                    && r.targetsBodyPart
                    && (appliesToCurrentOrAncestors || r.defName == "RemoveBodyPart");
            });

            Log.Message($"Hediff Part: {hediff.Part.def} \n just the Part: {hediff.Part}");
            if (recipes == null)
            {
                return;
            }
            Log.Warning($"RecipeDef: {recipes.Count()}");

            //Is it getting filtered out here?
            //IEnumerable<RecipeDef> recipes = pawn.def.AllRecipes.Where(r =>
            //    (
            //     r.AvailableNow
            //        &&
            //        (r.targetsBodyPart
            //        && (r.appliedOnFixedBodyParts.Contains(hediff.Part.def) ||
            //            (hediff.Part.parent != null &&
            //             !IsTorsoOrHead(hediff.Part.parent.def) &&
            //            r.appliedOnFixedBodyParts.Contains(hediff.Part.parent.def))
            //        ) //This is filtering the parent surgeries inadvertently
            //    ) )

            //);

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
                        Log.Message(
                            $"-->   aplliedOnGroup: {recipe.appliedOnFixedBodyPartGroups[i]}"
                        );
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
                                    && (!enumerable.Any() || !recipe.dontShowIfAnyIngredientMissing) //Is this whats filtering the operations that arent available?
                            )
                        )
                        {
                            if (recipe.targetsBodyPart)
                            {
                                IEnumerable<BodyPartRecord> parents = GetAllAncestors(hediff.Part);

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
                                        bool isCompatible = recipe.CompatibleWithHediff(hediff.def);

                                        try
                                        {
                                            //FILTERING HERE GIVES MORE CONTROL

                                            //item keeps record of part similar to hediff.Part
                                            var hediffPart = hediff.Part.Label.Split(
                                                new char[] { ' ' },
                                                StringSplitOptions.RemoveEmptyEntries
                                            );
                                            string[] currentPart = item
                                                .Label.ToLower()
                                                .Split(
                                                    new char[] { ' ' },
                                                    StringSplitOptions.RemoveEmptyEntries
                                                );
                                            if (currentPart.Count() == 2)
                                            {
                                                //This means that its a part with the same side
                                                if (currentPart[0] != hediffPart[0])
                                                {
                                                    //.Count ancestors and if its over a certain amount then it is a limb, except maybe shoulder would have trouble
                                                    continue;
                                                }
                                            }

                                            //if (parents.Count() >= 2)
                                            //{
                                            //    if (
                                            //        item.parent.Label != null
                                            //        && item.parent.Label == "torso"
                                            //    )
                                            //    {
                                            //        continue;
                                            //    }
                                            //}
                                            IEnumerable<BodyPartRecord> filteredList = parents
                                                .Where(p =>
                                                    true
                                                //!p.defName.Equals(
                                                //    "Torso",
                                                //    StringComparison.OrdinalIgnoreCase
                                                //)
                                                )
                                                .ToList();
                                            int counter = 0;
                                            int totalItems = filteredList.Count();
                                            //foreach (BodyPartRecord part in filteredList)
                                            //{
                                            //    counter++;
                                            //    if (totalItems >= 2)
                                            //    {
                                            //        if (item.parent == null)
                                            //        {
                                            //            continue;
                                            //        }
                                            //        if (true  )
                                            //        {
                                            //            if(item.depth.ToString() == "Outside")
                                            //                options.Add(
                                            //                    (FloatMenuOption)
                                            //                        generateSurgeryBillMethod.Invoke(
                                            //                            null,
                                            //                            new object[]
                                            //                            {
                                            //                                pawn,
                                            //                                pawn,
                                            //                                recipe,
                                            //                                enumerable,
                                            //                                report,
                                            //                                num++,
                                            //                                item
                                            //                            }
                                            //                        )
                                            //                );



                                            //        }
                                            //    }
                                            //    //If statement for torso potentially
                                            //    if (totalItems == 1)
                                            //    {
                                            //        if(hediff.Part.Label == item.LabelCap)
                                            //        {
                                            //            options.Add(
                                            //            (FloatMenuOption)
                                            //                generateSurgeryBillMethod.Invoke(
                                            //                    null,
                                            //                    new object[]
                                            //                    {
                                            //                        pawn,
                                            //                        pawn,
                                            //                        recipe,
                                            //                        enumerable,
                                            //                        report,
                                            //                        num++,
                                            //                        item
                                            //                    }
                                            //                )
                                            //        );
                                            //        }
                                            //    }

                                            //    Write($"bodypartdef label: {part.Label}");
                                            //}
                                            //if (parents.Count() >= 2)
                                            //{
                                            //    if (
                                            //        filteredList.Any(p =>
                                            //            p.labelShort == item.parent.Label
                                            //        )
                                            //    )
                                            //    {
                                            //        options.Add(
                                            //            (FloatMenuOption)
                                            //                generateSurgeryBillMethod.Invoke(
                                            //                    null,
                                            //                    new object[]
                                            //                    {
                                            //                        pawn,
                                            //                        pawn,
                                            //                        recipe,
                                            //                        enumerable,
                                            //                        report,
                                            //                        num++,
                                            //                        item
                                            //                    }
                                            //                )
                                            //        );
                                            //        continue;
                                            //    }
                                            //}



                                            //If parent of hediff and current part isnt the same continue

                                            if (totalItems >= 2)
                                            {
                                                if (item.depth.ToString() != "Outside")
                                                {
                                                    continue;
                                                }
                                            }
                                            //If !hediff.parts.contains(item.part). This will let you know if item is a child element. May will have to use any for that compare label short?

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
                                        catch (Exception ex)
                                        {
                                            throw new Exception(
                                                $"Error thrown in option filtering. \n"
                                                    + $"{item.parent.Label} | Error: {ex.Message}"
                                            );
                                        }
                                    }
                                }
                            }

                            //End of Adding?
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

        //Recursive funtion that gets all the parent parts

        private static List<BodyPartRecord> GetAllAncestors(this BodyPartRecord part)
        {
            List<BodyPartRecord> ancestors = new List<BodyPartRecord>();
            BodyPartRecord current = part.parent;

            while (current != null)
            {
                ancestors.Add(current);
                current = current.parent;
            }

            return ancestors;
        }

        //Might not be doing anything, CHECK LATER
        private static bool GetParentPartRelation(
            IEnumerable<BodyPartDef> bodyParts,
            BodyPartRecord parent
        )
        {
            if (parent.Label != null && IgnoreTorsoAndHead(parent.Label))
            {
                //Theres no damn filtering logic

                //swapped to ! because its somewhat better to exit the method sooner than later
                return true;
            }
            Log.Warning("Torso or Head");
            return false;
        }

        private static bool IgnoreTorsoAndHead(string parent)
        {
            return !parent.Contains("torso") || !parent.Contains("head");
        }

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
                    new List<string> { "Arm", "Shoulder", "Radius", "Humerous" }
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

        // Helper method to check if a part is torso or head
        private static bool IsTorsoOrHead(BodyPartDef partDef)
        {
            return partDef.defName == "torso" || partDef.defName == "head";
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

        public static bool IsDigit()
        {
            return true;
        }

        #endregion
        #region shortcut functions
        public static void Write(string message)
        {
            Log.Message(message);
        }

        public static void Error(string message)
        {
            Log.Error(message);
        }

        public static void Warning(string message)
        {
            Log.Warning(message);
        }
        #endregion
    }
}

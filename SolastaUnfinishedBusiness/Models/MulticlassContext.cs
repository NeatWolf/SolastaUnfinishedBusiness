﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterSubclassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionPointPools;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionProficiencys;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionAttributeModifiers;
using static FeatureDefinitionAttributeModifier;

namespace SolastaUnfinishedBusiness.Models;

internal static class MulticlassContext
{
    internal const int MaxClasses = 3;

    private const string ArmorTrainingDescription = "Feature/&ArmorTrainingShortDescription";

    private const string SkillGainChoicesDescription = "Feature/&SkillGainChoicesPluralDescription";

    private const BindingFlags PrivateBinding = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FeatureDefinitionProficiency ProficiencyBarbarianArmorMulticlass =
        FeatureDefinitionProficiencyBuilder
            .Create("ProficiencyBarbarianArmorMulticlass")
            .SetGuiPresentation("Feature/&BarbarianArmorProficiencyTitle", ArmorTrainingDescription)
            .SetProficiencies(RuleDefinitions.ProficiencyType.Armor,
                EquipmentDefinitions.ShieldCategory)
            .AddToDB();

    private static readonly FeatureDefinitionProficiency ProficiencyFighterArmorMulticlass =
        FeatureDefinitionProficiencyBuilder
            .Create("ProficiencyFighterArmorMulticlass")
            .SetGuiPresentation("Feature/&FighterArmorProficiencyTitle", ArmorTrainingDescription)
            .SetProficiencies(RuleDefinitions.ProficiencyType.Armor,
                EquipmentDefinitions.LightArmorCategory,
                EquipmentDefinitions.MediumArmorCategory,
                EquipmentDefinitions.ShieldCategory)
            .AddToDB();

    private static readonly FeatureDefinitionProficiency ProficiencyPaladinArmorMulticlass =
        FeatureDefinitionProficiencyBuilder
            .Create("ProficiencyPaladinArmorMulticlass")
            .SetGuiPresentation("Feature/&PaladinArmorProficiencyTitle", ArmorTrainingDescription)
            .SetProficiencies(RuleDefinitions.ProficiencyType.Armor,
                EquipmentDefinitions.LightArmorCategory,
                EquipmentDefinitions.MediumArmorCategory,
                EquipmentDefinitions.ShieldCategory)
            .AddToDB();

    private static readonly FeatureDefinitionPointPool PointPoolBardSkillPointsMulticlass =
        FeatureDefinitionPointPoolBuilder
            .Create("PointPoolBardSkillPointsMulticlass")
            .SetGuiPresentation("Feature/&BardSkillPointsTitle", SkillGainChoicesDescription)
            .SetPool(HeroDefinitions.PointsPoolType.Skill, 1)
            .RestrictChoices(
                SkillDefinitions.Acrobatics,
                SkillDefinitions.AnimalHandling,
                SkillDefinitions.Arcana,
                SkillDefinitions.Athletics,
                SkillDefinitions.Deception,
                SkillDefinitions.History,
                SkillDefinitions.Insight,
                SkillDefinitions.Intimidation,
                SkillDefinitions.Investigation,
                SkillDefinitions.Medecine,
                SkillDefinitions.Nature,
                SkillDefinitions.Perception,
                SkillDefinitions.Performance,
                SkillDefinitions.Persuasion,
                SkillDefinitions.Religion,
                SkillDefinitions.SleightOfHand,
                SkillDefinitions.Stealth,
                SkillDefinitions.Survival
            )
            .AddToDB();

    private static readonly FeatureDefinitionPointPool PointPoolRangerSkillPointsMulticlass =
        FeatureDefinitionPointPoolBuilder
            .Create("PointPoolRangerSkillPointsMulticlass")
            .SetGuiPresentation("Feature/&RangerSkillsTitle", SkillGainChoicesDescription)
            .SetPool(HeroDefinitions.PointsPoolType.Skill, 1)
            .RestrictChoices(
                SkillDefinitions.AnimalHandling,
                SkillDefinitions.Athletics,
                SkillDefinitions.Insight,
                SkillDefinitions.Investigation,
                SkillDefinitions.Nature,
                SkillDefinitions.Perception,
                SkillDefinitions.Survival,
                SkillDefinitions.Stealth
            )
            .AddToDB();

    private static readonly FeatureDefinitionPointPool PointPoolRogueSkillPointsMulticlass =
        FeatureDefinitionPointPoolBuilder
            .Create("PointPoolRogueSkillPointsMulticlass")
            .SetGuiPresentation("Feature/&RogueSkillPointsTitle", SkillGainChoicesDescription)
            .SetPool(HeroDefinitions.PointsPoolType.Skill, 1)
            .RestrictChoices(
                SkillDefinitions.Acrobatics,
                SkillDefinitions.Athletics,
                SkillDefinitions.Deception,
                SkillDefinitions.Insight,
                SkillDefinitions.Intimidation,
                SkillDefinitions.Investigation,
                SkillDefinitions.Perception,
                SkillDefinitions.Performance,
                SkillDefinitions.Persuasion,
                SkillDefinitions.SleightOfHand,
                SkillDefinitions.Stealth
            )
            .AddToDB();

    private static readonly MethodInfo NullMethod = null;

    // these features will be replaced to comply to SRD multiclass rules
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> FeaturesToReplace = new()
    {
        { ProficiencyBarbarianArmor, ProficiencyBarbarianArmorMulticlass },
        { ProficiencyFighterArmor, ProficiencyFighterArmorMulticlass },
        { ProficiencyPaladinArmor, ProficiencyPaladinArmorMulticlass },
        { PointPoolBardSkillPoints, PointPoolBardSkillPointsMulticlass },
        { PointPoolRangerSkillPoints, PointPoolRangerSkillPointsMulticlass },
        { PointPoolRogueSkillPoints, PointPoolRogueSkillPointsMulticlass }
    };

    // these features will be removed to comply with SRD multiclass rules
    private static readonly Dictionary<CharacterClassDefinition, List<FeatureDefinition>> FeaturesToExclude = new()
    {
        {
            Barbarian, new List<FeatureDefinition> { PointPoolBarbarianrSkillPoints, ProficiencyBarbarianSavingThrow }
        },
        { Bard, new List<FeatureDefinition> { ProficiencyBardWeapon, ProficiencyBardSavingThrow } },
        {
            Cleric,
            new List<FeatureDefinition>
            {
                ProficiencyClericWeapon, PointPoolClericSkillPoints, ProficiencyClericSavingThrow
            }
        },
        { Druid, new List<FeatureDefinition> { PointPoolDruidSkillPoints, ProficiencyDruidSavingThrow } },
        { Fighter, new List<FeatureDefinition> { PointPoolFighterSkillPoints, ProficiencyFighterSavingThrow } },
        { Monk, new List<FeatureDefinition> { PointPoolMonkSkillPoints, ProficiencyMonkSavingThrow } },
        { Paladin, new List<FeatureDefinition> { PointPoolPaladinSkillPoints, ProficiencyPaladinSavingThrow } },
        { Ranger, new List<FeatureDefinition> { ProficiencyRangerSavingThrow } },
        { Rogue, new List<FeatureDefinition> { ProficiencyRogueWeapon, ProficiencyRogueSavingThrow } },
        {
            Sorcerer,
            new List<FeatureDefinition>
            {
                ProficiencySorcererWeapon, PointPoolSorcererSkillPoints, ProficiencySorcererSavingThrow
            }
        },
        {
            Warlock,
            new List<FeatureDefinition>
            {
                ProficiencyWarlockWeapon, PointPoolWarlockSkillPoints, ProficiencyWarlockSavingThrow
            }
        },
        {
            Wizard,
            new List<FeatureDefinition>
            {
                ProficiencyWizardWeapon, PointPoolWizardSkillPoints, ProficiencyWizardSavingThrow
            }
        }
    };

    private static (MethodInfo, HeroContext) FeatureUnlocksContext { get; set; }

    internal static void LateLoad()
    {
        FixExtraAttacksScenarios();
        AddNonOfficialBlueprintsToFeaturesCollections();
        PatchClassLevel();
        PatchEquipmentAssignment();
        PatchFeatureUnlocks();
    }

    private static void FixExtraAttacksScenarios()
    {
        // make all extra attacks use Force If Better
        foreach (var featureDefinitionAttributeModifier in DatabaseRepository
                     .GetDatabase<FeatureDefinitionAttributeModifier>()
                     .Where(x => x.modifiedAttribute == AttributeDefinitions.AttacksNumber))
        {
            featureDefinitionAttributeModifier.modifierValue = 2;
            featureDefinitionAttributeModifier.modifierOperation = AttributeModifierOperation.ForceIfBetter;
        }

        // fix use cases at level 11 when certain classes / subs get a 3rd attack
        var attributeModifierExtraAttackForce3 = FeatureDefinitionAttributeModifierBuilder
            .Create(AttributeModifierFighterExtraAttack, "AttributeModifierExtraAttackForce3")
            .SetGuiPresentationNoContent(true)
            .SetModifier(AttributeModifierOperation.ForceIfBetter, AttributeDefinitions.AttacksNumber, 3)
            .AddToDB();

        // leave here for now as we will need this on level 20...
        _ = FeatureDefinitionAttributeModifierBuilder
            .Create(AttributeModifierFighterExtraAttack, "AttributeModifierExtraAttackForce4")
            .SetGuiPresentationNoContent(true)
            .SetModifier(AttributeModifierOperation.ForceIfBetter, AttributeDefinitions.AttacksNumber, 4)
            .AddToDB();

        Fighter.FeatureUnlocks.Add(new FeatureUnlockByLevel(attributeModifierExtraAttackForce3, 11));
        RangerSwiftBlade.FeatureUnlocks.Add(new FeatureUnlockByLevel(attributeModifierExtraAttackForce3, 11));
    }

    private static void AddNonOfficialBlueprintsToFeaturesCollections()
    {
        if (!DatabaseHelper.TryGetDefinition<CharacterClassDefinition>("Inventor", out var inventorClass))
        {
            return;
        }

        var dbFeatureDefinitionPointPool = DatabaseRepository.GetDatabase<FeatureDefinitionPointPool>();
        var dbFeatureDefinitionProficiency = DatabaseRepository.GetDatabase<FeatureDefinitionProficiency>();

        FeaturesToExclude.Add(inventorClass,
            new List<FeatureDefinition>
            {
                dbFeatureDefinitionPointPool.GetElement("PointPoolInventorSkills"),
                dbFeatureDefinitionProficiency.GetElement("ProficiencyInventorSavingThrow")
            });
    }

    //
    // ClassLevel patching support
    //

    private static IEnumerable<CodeInstruction> ClassLevelTranspiler(
        [NotNull] IEnumerable<CodeInstruction> instructions)
    {
        var classesAndLevelsMethod = typeof(RulesetCharacterHero).GetMethod("get_ClassesAndLevels");
        var classesHistoryMethod = typeof(RulesetCharacterHero).GetMethod("get_ClassesHistory");
        var getClassLevelMethod = new Func<RulesetCharacterHero, int>(LevelUpContext.GetSelectedClassLevel).Method;

        return instructions
            .ReplaceAllCode(instruction => instruction.Calls(classesAndLevelsMethod),
                -1,
                2,
                new CodeInstruction(OpCodes.Call, getClassLevelMethod))
            .ReplaceAllCode(instruction => instruction.Calls(classesAndLevelsMethod),
                -1,
                1,
                new CodeInstruction(OpCodes.Call, classesHistoryMethod));
    }

    private static void PatchClassLevel()
    {
        var methods = new[]
        {
            // these use ClassesAndLevels[classDefinition]
            typeof(CharacterStageClassSelectionPanel).GetMethod("FillClassFeatures", PrivateBinding),
            typeof(CharacterStageClassSelectionPanel).GetMethod("RefreshCharacter", PrivateBinding),
            // these use ClassesHistory.Count
            typeof(CharacterStageSpellSelectionPanel).GetMethod("Refresh", PrivateBinding),
            typeof(CharacterBuildingManager).GetMethod("AutoAcquireSpells")
        };

        var harmony = new Harmony("SolastaUnfinishedBusiness");
        var transpiler =
            new Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>(
                ClassLevelTranspiler).Method;

        try
        {
            foreach (var method in methods)
            {
                harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
            }
        }
        catch
        {
            Main.Error("cannot fully patch Multiclass selected class levels");
        }
    }

    //
    // Equipment patching support
    //

    private static bool ShouldEquipmentBeAssigned([NotNull] CharacterHeroBuildingData heroBuildingData)
    {
        var hero = heroBuildingData.HeroCharacter;
        var isLevelingUp = LevelUpContext.IsLevelingUp(hero);

        return !isLevelingUp;
    }

    private static void PatchEquipmentAssignment()
    {
        var methods = new[]
        {
            typeof(CharacterBuildingManager).GetMethod("BuildWieldedConfigurations"),
            typeof(CharacterBuildingManager).GetMethod("ClearWieldedConfigurations"),
            typeof(CharacterBuildingManager).GetMethod("GrantBaseEquipment"),
            typeof(CharacterBuildingManager).GetMethod("RemoveBaseEquipment"),
            typeof(CharacterBuildingManager).GetMethod("UnassignEquipment")
        };

        var harmony = new Harmony("SolastaUnfinishedBusiness");
        var prefix = new Func<CharacterHeroBuildingData, bool>(ShouldEquipmentBeAssigned).Method;

        try
        {
            foreach (var method in methods)
            {
                harmony.Patch(method, new HarmonyMethod(prefix));
            }
        }
        catch
        {
            Main.Error("cannot fully patch Multiclass equipment");
        }
    }

    private static void PatchFeatureUnlocks()
    {
        var patches = new[]
        {
            // CharacterStageClassSelectionPanel
            (
                typeof(CharacterStageClassSelectionPanel).GetMethod("EnumerateActiveFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageClassSelectionPanel).GetMethod("FillClassFeatures", PrivateBinding) ?? NullMethod,
                HeroContext.StagePanel
            ),

            // CharacterStageDeitySelectionPanel
            (
                typeof(CharacterStageDeitySelectionPanel).GetMethod("OnHigherLevelCb") ?? NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageDeitySelectionPanel).GetMethod("EnumerateActiveFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageDeitySelectionPanel).GetMethod("FillSubclassFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (typeof(CharacterStageDeitySelectionPanel).GetMethod("EnterStage") ?? NullMethod, HeroContext.StagePanel),

            // CharacterStageLevelGainsPanel
            (
                typeof(CharacterStageLevelGainsPanel).GetMethod("OnHigherLevelClassCb") ?? NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageLevelGainsPanel).GetMethod("EnumerateActiveClassFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageLevelGainsPanel).GetMethod("FillUnlockedClassFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageLevelGainsPanel).GetMethod("Refresh", PrivateBinding) ?? NullMethod,
                HeroContext.StagePanel
            ),

            // CharacterStageSubclassSelectionPanel
            (
                typeof(CharacterStageSubclassSelectionPanel).GetMethod("OnHigherLevelCb") ?? NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageSubclassSelectionPanel).GetMethod("EnumerateActiveFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageSubclassSelectionPanel).GetMethod("FillSubclassFeatures", PrivateBinding) ??
                NullMethod,
                HeroContext.StagePanel
            ),
            (
                typeof(CharacterStageSubclassSelectionPanel).GetMethod("Refresh", PrivateBinding) ?? NullMethod,
                HeroContext.StagePanel
            ),

            // CharacterBuildingManager
            (
                typeof(CharacterBuildingManager).GetMethod("FinalizeCharacter") ?? NullMethod,
                HeroContext.BuildingManager
            ),

            // ArchetypesPreviewModal
            // (typeof(ArchetypesPreviewModal).GetMethod("Refresh", PrivateBinding), HeroContext.BuildingManager),

            // CharacterInformationPanel
            (
                typeof(CharacterInformationPanel).GetMethod("TryFindChoiceFeature", PrivateBinding) ?? NullMethod,
                HeroContext.InformationPanel
            ),

            // RulesetCharacterHero
            (
                typeof(RulesetCharacterHero).GetMethod("FindClassHoldingFeature") ?? NullMethod,
                HeroContext.CharacterHero
            ),
            (
                typeof(RulesetCharacterHero).GetMethod("LookForFeatureOrigin", PrivateBinding) ?? NullMethod,
                HeroContext.CharacterHero
            )
        };

        var harmony = new Harmony("SolastaUnfinishedBusiness");
        var transpiler = new Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>
            (FeatureUnlocksTranspiler).Method;

        try
        {
            foreach (var patch in patches)
            {
                FeatureUnlocksContext = patch;

                harmony.Patch(patch.Item1, transpiler: new HarmonyMethod(transpiler));
            }
        }
        catch
        {
            Main.Error("cannot fully patch Multiclass feature unlocks");
        }
    }

    [ItemNotNull]
    private static IEnumerable<CodeInstruction> YieldHero()
    {
        switch (FeatureUnlocksContext.Item2)
        {
            case HeroContext.StagePanel:
                var classType = FeatureUnlocksContext.Item1.DeclaringType;

                if (classType != null)
                {
                    var currentHeroField =
                        classType.GetField("currentHero", BindingFlags.Instance | BindingFlags.NonPublic);

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, currentHeroField);
                }

                break;

            case HeroContext.BuildingManager:
                yield return new CodeInstruction(OpCodes.Ldarg_1);

                break;

            case HeroContext.CharacterHero:
                yield return new CodeInstruction(OpCodes.Ldarg_0);

                break;

            case HeroContext.InformationPanel:
                var inspectedCharacterMethod =
                    typeof(CharacterInformationPanel).GetMethod("get_InspectedCharacter");
                var rulesetCharacterHeroMethod = typeof(GuiCharacter).GetMethod("get_RulesetCharacterHero");

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, inspectedCharacterMethod);
                yield return new CodeInstruction(OpCodes.Call, rulesetCharacterHeroMethod);

                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // ReSharper disable once UnusedMember.Global
    private static IEnumerable<CodeInstruction> FeatureUnlocksTranspiler(
        [NotNull] IEnumerable<CodeInstruction> instructions)
    {
        var classFeatureUnlocksMethod = typeof(CharacterClassDefinition).GetMethod("get_FeatureUnlocks");
        var classFilteredFeatureUnlocksMethod =
            new Func<CharacterClassDefinition, RulesetCharacterHero, IEnumerable<FeatureUnlockByLevel>>(
                ClassFilteredFeatureUnlocks).Method;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(classFeatureUnlocksMethod))
            {
                foreach (var inst in YieldHero())
                {
                    yield return inst;
                }

                yield return new CodeInstruction(OpCodes.Call, classFilteredFeatureUnlocksMethod);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    // support class filtered feature unlocks
    private static IEnumerable<FeatureUnlockByLevel> ClassFilteredFeatureUnlocks(
        CharacterClassDefinition characterClassDefinition, [NotNull] RulesetCharacterHero rulesetCharacterHero)
    {
        var firstClass = rulesetCharacterHero.ClassesHistory[0];
        var selectedClass = LevelUpContext.GetSelectedClass(rulesetCharacterHero) ?? characterClassDefinition;
        var selectedSubClass = LevelUpContext.GetSelectedSubclass(rulesetCharacterHero);
        var filteredFeatureUnlockByLevels = selectedClass.FeatureUnlocks.ToList();

        //
        // supports a better MC UI offering
        //
        if (LevelUpContext.IsLevelingUp(rulesetCharacterHero)
            && LevelUpContext.IsClassSelectionStage(rulesetCharacterHero)
            && selectedSubClass != null)
        {
            filteredFeatureUnlockByLevels.AddRange(selectedSubClass.FeatureUnlocks);
        }

        // don't mess up with very first class taken
        if (!LevelUpContext.IsMulticlass(rulesetCharacterHero) || firstClass == selectedClass)
        {
            return characterClassDefinition.FeatureUnlocks;
        }

        // replace features per mc rules
        foreach (var featureNameToReplace in FeaturesToReplace)
        {
            var count = filteredFeatureUnlockByLevels.RemoveAll(x => x.FeatureDefinition == featureNameToReplace.Key);

            if (count > 0)
            {
                filteredFeatureUnlockByLevels.Add(new FeatureUnlockByLevel(featureNameToReplace.Value, 1));
            }
        }

        // exclude features per mc rules
        if (FeaturesToExclude.TryGetValue(selectedClass, out var featureNamesToExclude))
        {
            filteredFeatureUnlockByLevels.RemoveAll(x => featureNamesToExclude.Contains(x.FeatureDefinition));
        }

        // sort back results
        filteredFeatureUnlockByLevels.Sort(Sorting.CompareFeatureUnlock);

        return filteredFeatureUnlockByLevels;
    }

    internal static int SpellCastingLevel(RulesetSpellRepertoire repertoire, RulesetEffectSpell rulesetEffect)
    {
        return SpellCastingLevel(repertoire, rulesetEffect.Caster, rulesetEffect.SpellDefinition);
    }

    internal static int SpellCastingLevel(RulesetSpellRepertoire repertoire,
        CharacterActionCastSpell action)
    {
        return SpellCastingLevel(repertoire, action.ActingCharacter.RulesetActor, action.ActiveSpell.SpellDefinition);
    }

    private static int SpellCastingLevel(RulesetSpellRepertoire repertoire, RulesetActor caster, SpellDefinition spell)
    {
        if (caster is RulesetCharacterHero hero && spell.SpellLevel == 0)
        {
            return hero.GetAttribute(AttributeDefinitions.CharacterLevel).CurrentValue;
        }

        return repertoire.SpellCastingLevel;
    }

    //
    // FeatureUnlocks patching support
    //

    private enum HeroContext
    {
        BuildingManager,
        CharacterHero,
        StagePanel,
        InformationPanel
    }
}

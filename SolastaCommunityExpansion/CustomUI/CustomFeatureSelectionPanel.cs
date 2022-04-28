using System;
using System.Collections;
using System.Collections.Generic;
using SolastaModApi.Infrastructure;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace SolastaCommunityExpansion.CustomUI
{
    public class CustomFeatureSelectionPanel : CharacterStagePanel
    {
        private static CustomFeatureSelectionPanel _instance;

        public static CharacterStagePanel Get(GameObject[] prefabs, Transform parent)
        {
            if (_instance == null)
            {
                var gameObject = Gui.GetPrefabFromPool(prefabs[8], parent);

                var spells = gameObject.GetComponent<CharacterStageSpellSelectionPanel>();
                _instance = gameObject.AddComponent<CustomFeatureSelectionPanel>();
                _instance.Setup(gameObject, spells, prefabs);
            }

            return _instance;
        }

        #region Fields from CharacterStageSpellSelectionPanel

        private CharacterStageSpellSelectionPanel spellsPanel; //TODO: do we need it?

        private RectTransform spellsByLevelTable;
        private GameObject spellsByLevelPrefab;
        private ScrollRect spellsScrollRect;
        private RectTransform learnStepsTable;
        private GameObject learnStepPrefab;
        private AssetReferenceSprite backdropReference;
        private Image backdrop;
        private AnimationCurve curve;
        private RectTransform levelButtonsTable;
        private GameObject levelButtonPrefab;

        #endregion

        private void Setup(GameObject o, CharacterStageSpellSelectionPanel spells, GameObject[] prefabs)
        {
            spellsPanel = spells;
            _instance.SetField("stageDefinition", spells.StageDefinition);

            spellsByLevelTable = spells.GetField<RectTransform>("spellsByLevelTable");
            spellsByLevelPrefab = spells.GetField<GameObject>("spellsByLevelPrefab");
            spellsScrollRect = spells.GetField<ScrollRect>("spellsScrollRect");
            learnStepsTable = spells.GetField<RectTransform>("learnStepsTable");
            learnStepPrefab = spells.GetField<GameObject>("learnStepPrefab");
            backdropReference = spells.GetField<AssetReferenceSprite>("backdropReference");
            backdrop = spells.GetField<Image>("backdrop");
            curve = spells.GetField<AnimationCurve>("curve");
            levelButtonsTable = spells.GetField<RectTransform>("levelButtonsTable");
            levelButtonPrefab = spells.GetField<GameObject>("levelButtonPrefab");
        }

        private const float ScrollDuration = 0.3f;
        private const float SpellsByLevelMargin = 10.0f;

        //TODO: add proper translation strings
        public override string Name => "Custom Feature Panel";
        public override string Title => "Custom Feature Panel Title";
        public override string Description => "Custom Feature Panel Description";
        private bool IsFinalStep => this.currentLearnStep == this.allTags.Count;

        private int currentLearnStep;
        private List<string> allTags = new ();
        private bool wasClicked;

        public override void SetScrollSensitivity(float scrollSensitivity)
        {
            this.spellsScrollRect.scrollSensitivity = -scrollSensitivity;
        }

        public override IEnumerator Load()
        {
            Main.Log($"[ENDER] CUSTOM Load");

            yield return base.Load();
            IRuntimeService runtimeService = ServiceRepository.GetService<IRuntimeService>();
            runtimeService.RuntimeLoaded += this.RuntimeLoaded;
        }

        public override IEnumerator Unload()
        {
            Main.Log($"[ENDER] CUSTOM Unload");

            IRuntimeService runtimeService = ServiceRepository.GetService<IRuntimeService>();
            runtimeService.RuntimeLoaded -= this.RuntimeLoaded;

            // this.allCantrips.Clear();
            // this.allSpells.Clear();
            yield return base.Unload();
        }

        private void RuntimeLoaded(Runtime runtime)
        {
            //TODO: collect any relevant info we need
            // SpellDefinition[] allSpellDefinitions = DatabaseRepository.GetDatabase<SpellDefinition>().GetAllElements();
            // this.allCantrips = new List<SpellDefinition>();
            // this.allSpells = new Dictionary<int, List<SpellDefinition>>();
            //
            // foreach (SpellDefinition spellDefinition in allSpellDefinitions)
            // {
            //     if (spellDefinition.SpellLevel == 0)
            //     {
            //         this.allCantrips.Add(spellDefinition);
            //     }
            //     else
            //     {
            //         if (!this.allSpells.ContainsKey(spellDefinition.SpellLevel))
            //         {
            //             this.allSpells.Add(spellDefinition.SpellLevel, new List<SpellDefinition>());
            //         }
            //
            //         this.allSpells[spellDefinition.SpellLevel].Add(spellDefinition);
            //     }
            // }
        }

        public override void UpdateRelevance()
        {
            RulesetCharacterHero hero = this.currentHero;
            CharacterHeroBuildingData heroBuildingData = hero.GetHeroBuildingData();

            //TODO: check if we have any custom features for selection

            this.IsRelevant = true;
        }

        public override void EnterStage()
        {
            Main.Log($"[ENDER] CUSTOM EnterStage '{this.StageDefinition}'");

            this.currentLearnStep = 0;

            this.CollectTags();

            this.OnEnterStageDone();
        }

        protected override void OnBeginShow(bool instant = false)
        {
            base.OnBeginShow(instant);

            this.backdrop.sprite = Gui.LoadAssetSync<Sprite>(this.backdropReference);

            this.CommonData.CharacterStatsPanel.Show(CharacterStatsPanel.ArmorClassFlag |
                                                     CharacterStatsPanel.InitiativeFlag | CharacterStatsPanel.MoveFlag |
                                                     CharacterStatsPanel.ProficiencyFlag |
                                                     CharacterStatsPanel.HitPointMaxFlag |
                                                     CharacterStatsPanel.HitDiceFlag);

            this.BuildLearnSteps();
            this.spellsScrollRect.normalizedPosition = Vector2.zero;

            this.OnPreRefresh();
            this.RefreshNow();
        }

        protected override void OnEndHide()
        {
            for (int i = 0; i < this.spellsByLevelTable.childCount; i++)
            {
                Transform child = this.spellsByLevelTable.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    SpellsByLevelGroup group = child.GetComponent<SpellsByLevelGroup>();
                    group.Unbind();
                }
            }

            Gui.ReleaseChildrenToPool(this.spellsByLevelTable);
            Gui.ReleaseChildrenToPool(this.learnStepsTable);
            Gui.ReleaseChildrenToPool(this.levelButtonsTable);

            base.OnEndHide();

            if (this.backdrop.sprite != null)
            {
                Gui.ReleaseAddressableAsset(this.backdrop.sprite);
                this.backdrop.sprite = null;
            }
        }

        protected override void Refresh()
        {
            RulesetCharacterHero hero = this.currentHero;
            CharacterHeroBuildingData heroBuildingData = hero.GetHeroBuildingData();
            Main.Log($"[ENDER] Refresh - start");
            string currentTag = string.Empty;
            for (int i = 0; i < this.learnStepsTable.childCount; i++)
            {
                Transform child = this.learnStepsTable.GetChild(i);
                Main.Log($"[ENDER] Refresh - learnt step {i} child: {child!= null}, object:{child?.gameObject}");
                
                if (child.gameObject.activeSelf)
                {
                    LearnStepItem stepItem = child.GetComponent<LearnStepItem>();
                    Main.Log($"[ENDER] Refresh - get step item {i} item: {stepItem!=null}");

                    LearnStepItem.Status status;
                    if (i == this.currentLearnStep)
                    {
                        status = LearnStepItem.Status.InProgress;
                    }
                    else if (i == this.currentLearnStep - 1)
                    {
                        status = LearnStepItem.Status.Previous;
                    }
                    else
                    {
                        status = LearnStepItem.Status.Locked;
                    }
                    
                    stepItem.CustomRefresh(status);

                    if (status == LearnStepItem.Status.InProgress)
                    {
                        currentTag = stepItem.Tag;
                    }
                }
            }
            Main.Log($"[ENDER] Refresh - steps refreshed");

            LayoutRebuilder.ForceRebuildLayoutImmediate(this.learnStepsTable);

            string lastTag = currentTag;
            if (this.IsFinalStep)
            {
                lastTag = this.allTags[this.allTags.Count - 1];
            }

            int requiredSpellGroups = 1;
            while (this.spellsByLevelTable.childCount < requiredSpellGroups)
            {
                Gui.GetPrefabFromPool(this.spellsByLevelPrefab, this.spellsByLevelTable);
            }
            Main.Log($"[ENDER] Refresh - spell groups counted");

            float totalWidth = 0;
            float lastWidth = 0;
            HorizontalLayoutGroup layout = this.spellsByLevelTable.GetComponent<HorizontalLayoutGroup>();
            layout.padding.left = (int)SpellsByLevelMargin;
            
            List<SpellDefinition> unlearnedSpells = null;
            heroBuildingData.UnlearnedSpells.TryGetValue(lastTag, out unlearnedSpells);
            Main.Log($"[ENDER] Refresh - start configuring spell groups");

            for (int i = 0; i < this.spellsByLevelTable.childCount; i++)
            {
                Transform child = this.spellsByLevelTable.GetChild(i);
                child.gameObject.SetActive(i < requiredSpellGroups);
                if (i < requiredSpellGroups)
                {
                    SpellsByLevelGroup group = child.GetComponent<SpellsByLevelGroup>();
                    int spellLevel = i;

                    group.Selected = true;

                    //TODO: create own wrapper for this
                    // group.BindLearning(this.CharacterBuildingService, spellFeature.SpellListDefinition,
                    //     spellFeature.RestrictedSchools, spellLevel, this.OnSpellBoxSelectedCb, knownSpells,
                    //     unlearnedSpells, lastTag, group.Selected, this.IsSpellUnlearnStep(this.currentLearnStep));

                    // lastWidth = group.RectTransform.rect.width + layout.spacing;
                    totalWidth += lastWidth;
                }
            }
            Main.Log($"[ENDER] Refresh - finished configuring spell groups");

            // Compute manually the table width, adding a reserve of fluff for the scrollview
            totalWidth += this.spellsScrollRect.GetComponent<RectTransform>().rect.width - lastWidth;
            this.spellsByLevelTable.sizeDelta = new Vector2(totalWidth, this.spellsByLevelTable.sizeDelta.y);

            // Spell Level Buttons
            while (this.levelButtonsTable.childCount < requiredSpellGroups)
            {
                Gui.GetPrefabFromPool(this.levelButtonPrefab, this.levelButtonsTable);
            }

            // Bind the required group, once for each spell level
            for (int spellLevel = 0; spellLevel < requiredSpellGroups; spellLevel++)
            {
                Transform child = this.levelButtonsTable.GetChild(spellLevel);
                child.gameObject.SetActive(true);
                SpellLevelButton button = child.GetComponent<SpellLevelButton>();
                button.Bind(spellLevel, this.LevelSelected);
            }
            Main.Log($"[ENDER] Refresh - finished configuring buttons");

            // Hide remaining useless groups
            for (int i = requiredSpellGroups; i < this.levelButtonsTable.childCount; i++)
            {
                Transform child = this.levelButtonsTable.GetChild(i);
                child.gameObject.SetActive(false);
            }
            Main.Log($"[ENDER] Refresh - finished hiding buttons");

            LayoutRebuilder.ForceRebuildLayoutImmediate(this.spellsByLevelTable);

            base.Refresh();
        }


        public override bool CanProceedToNextStage(out string failureString)
        {
            Main.Log($"[ENDER] CUSTOM CanProceedToNextStage");
            failureString = string.Empty;

            if (!this.IsFinalStep)
            {
                failureString = Gui.Localize("Stage/&SpellSelectionStageFailLearnSpellsDescription");
                return false;
            }

            failureString = "Not implementes yet";
            return false;
        }

        public void MoveToNextLearnStep()
        {
            this.currentLearnStep++;

            this.LevelSelected(0);

            this.OnPreRefresh();
            this.RefreshNow();
        }

        public void MoveToPreviousLearnStep(bool refresh = true, Action onDone = null)
        {
            IHeroBuildingCommandService heroBuildingCommandService =
                ServiceRepository.GetService<IHeroBuildingCommandService>();

            if (this.currentLearnStep > 0)
            {
                if (!this.IsFinalStep)
                {
                    this.ResetLearnings(this.currentLearnStep);
                }

                this.currentLearnStep--;
                this.ResetLearnings(this.currentLearnStep);
                if (this.IsFeatureUnlearnStep(this.currentLearnStep))
                {
                    heroBuildingCommandService.AcknowledgePreviousCharacterBuildingCommandLocally(() =>
                    {
                        this.CollectTags();
                        this.BuildLearnSteps();
                    });
                }
            }

            heroBuildingCommandService.AcknowledgePreviousCharacterBuildingCommandLocally(() =>
            {
                this.LevelSelected(0);
                this.OnPreRefresh();
                this.RefreshNow();
                this.ResetWasClickedFlag();
            });
        }

        private bool IsFeatureUnlearnStep(int step)
        {
            return false;
        }

        private void CollectTags()
        {
            //TODO: collect all relevant groups of feature selections
            allTags.SetRange("TestGroup1");
        }

        private void BuildLearnSteps()
        {
            Main.Log($"[ENDER] BuildLearnSteps");

            // Register all steps
            if (this.allTags != null && this.allTags.Count > 0)
            {
                while (this.learnStepsTable.childCount < this.allTags.Count)
                {
                    Gui.GetPrefabFromPool(this.learnStepPrefab, this.learnStepsTable);
                }

                for (int i = 0; i < this.learnStepsTable.childCount; i++)
                {
                    Transform child = this.learnStepsTable.GetChild(i);

                    if (i < this.allTags.Count)
                    {
                        child.gameObject.SetActive(true);
                        LearnStepItem learnStepItem = child.GetComponent<LearnStepItem>();
                        learnStepItem.CustomBind(i, this.allTags[i], 
                            this.OnLearnBack, 
                            this.OnLearnReset,
                             this.OnLearnAuto
                        );
                        // Bind(i, this.allTags[i], HeroDefinitions.PointsPoolType.Cantrip,
                        //     this.OnLearnBack, this.OnLearnReset,
                        //     this.OnLearnAuto);
                    }
                    else
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        public override void CancelStage()
        {
            int stepNumber = this.currentLearnStep;
            if (this.IsFinalStep)
            {
                stepNumber--;
            }

            for (int i = stepNumber; i >= 0; i--)
            {
                this.ResetLearnings(i);
            }

            IHeroBuildingCommandService heroBuildingCommandService =
                ServiceRepository.GetService<IHeroBuildingCommandService>();
            heroBuildingCommandService.AcknowledgePreviousCharacterBuildingCommandLocally(this.OnCancelStageDone);
        }

        public void OnLearnBack()
        {
            if (this.wasClicked)
            {
                return;
            }

            this.wasClicked = true;

            this.MoveToPreviousLearnStep(true, this.ResetWasClickedFlag);
        }

        public void OnLearnReset()
        {
            if (this.wasClicked)
            {
                return;
            }

            this.wasClicked = true;

            if (this.IsFinalStep)
            {
                this.currentLearnStep = this.allTags.Count - 1;
            }

            this.ResetLearnings(this.currentLearnStep,
                () =>
                {
                    this.OnPreRefresh();
                    this.RefreshNow();
                    this.ResetWasClickedFlag();
                });
        }

        private void ResetLearnings(int stepNumber, Action onDone = null)
        {
            RulesetCharacterHero hero = this.currentHero;
            var heroBuildingCommandService = ServiceRepository.GetService<IHeroBuildingCommandService>();

            string tag = this.allTags[stepNumber];

            //TODO: implement resetting of gained/forgotten features for specific step

            //
            // if (this.IsCantripStep(stepNumber))
            // {
            //     heroBuildingCommandService.UnacquireCantrips(hero, tag);
            // }
            //
            // if (this.IsSpellUnlearnStep(stepNumber))
            // {
            //     heroBuildingCommandService.UndoUnlearnSpells(hero, tag);
            // }
            //
            // if (this.IsSpellStep(stepNumber))
            // {
            //     heroBuildingCommandService.UnacquireSpells(hero, tag);
            // }

            heroBuildingCommandService.AcknowledgePreviousCharacterBuildingCommandLocally(() => onDone?.Invoke());
        }

        #region UI helpers

        private void ResetWasClickedFlag()
        {
            wasClicked = false;
        }

        public void LevelSelected(int level)
        {
            this.StartCoroutine(this.BlendToLevelGroup(level));
        }

        private IEnumerator BlendToLevelGroup(int level)
        {
            float duration = ScrollDuration;
            SpellsByLevelGroup group = this.spellsByLevelTable.GetChild(0).GetComponent<SpellsByLevelGroup>();
            foreach (Transform child in this.spellsByLevelTable)
            {
                SpellsByLevelGroup spellByLevelGroup = child.GetComponent<SpellsByLevelGroup>();
                if (spellByLevelGroup.SpellLevel == level)
                {
                    group = spellByLevelGroup;
                }
            }

            float initialX = this.spellsByLevelTable.anchoredPosition.x;
            float finalX = -group.RectTransform.anchoredPosition.x + SpellsByLevelMargin;

            while (duration > 0)
            {
                this.spellsByLevelTable.anchoredPosition = new Vector2(
                    Mathf.Lerp(initialX, finalX, this.curve.Evaluate((ScrollDuration - duration) / ScrollDuration)), 0);
                duration -= Gui.SystemDeltaTime;
                yield return null;
            }

            this.spellsByLevelTable.anchoredPosition = new Vector2(finalX, 0);
        }

        #endregion


        #region autoselect stuff

        public override void AutotestAutoValidate() => this.OnLearnAuto();

        public void OnLearnAuto()
        {
            if (this.wasClicked)
            {
                return;
            }

            this.wasClicked = true;

            this.OnLearnAutoImpl();
        }

        private void OnLearnAutoImpl(System.Random rng = null)
        {
            //TODO: implement auto-selection of stuff
        }

        #endregion
    }

    internal static class LearnStepItemExtension
    {
        
        public static void CustomBind(this LearnStepItem instance, int rank,
            string tag,
            LearnStepItem.ButtonActivatedHandler onBackOneStepActivated,
            LearnStepItem.ButtonActivatedHandler onResetActivated,
            LearnStepItem.ButtonActivatedHandler onAutoSelectActivated)
        {
            instance.Tag = tag;
            instance.SetField("rank", rank);
        }

        public static void CustomRefresh(this LearnStepItem instance, LearnStepItem.Status status)
        {

        }
    }
}

﻿using System;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using R2API;
using System.Reflection;

namespace CooldownOnHit
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Modernkennnern.CooldownOnHit", "CooldownOnHit", "0.1")]
    public class CooldownOnHit : BaseUnityPlugin
    {

        // TODO: Redo the way cooldowns are displayed.
        // Currently, Huntress Glaive has 7second cooldown - it says 7 until you've hit enough times to get it to 6.
        // What I would like is to turn that number into a "Hits remaining" visual that, instead of telling you a (non-functioning) timer, tells you how many hits until the cooldown is off.
        // This would require a function that turns the total cooldown into a number (7 seconds = 10 hits, for example), and ideally would be different for each character. (MUL-T would be bonkers with only 10 hits, while Sniper would be incredibly bad)

        // TODO: Turn this into a (Lunar) item

        // TODO: Allow the possibility of assigning specific skillslots to other damage sources

        // TODO: Change cooldown reduction to scale with ProcCoeffiency (This is on the damage source, so it will be somewhat awkward to implement)

        private static ConfigWrapper<bool> PrimarySkillRechargingConfig { get; set; }
        private static ConfigWrapper<bool> SecondarySkillRechargingConfig { get; set; }
        private static ConfigWrapper<bool> UtilitySkillRechargingConfig { get; set; }
        private static ConfigWrapper<bool> SpecialSkillRechargingConfig { get; set; }
        private static ConfigWrapper<bool> EquipmentRechargingConfig { get; set; }

        private static ConfigWrapper<float> SecondarySkillCooldownReductionOnHitAmountConfig { get; set; }
        private static ConfigWrapper<float> SpecialSkillCooldownReductionOnHitAmountConfig { get; set; }
        private static ConfigWrapper<float> EquipmentCooldownReductionOnHitAmountConfig { get; set; }

        private SurvivorIndex[] workingSurvivors = new SurvivorIndex[] { SurvivorIndex.Huntress };

        public float SecondaryAbilityCooldownReductionOnHitAmount {
            get => SecondarySkillCooldownReductionOnHitAmountConfig.Value;
            protected set => SecondarySkillCooldownReductionOnHitAmountConfig.Value = value;
        }

        public float SpecialAbilityCooldownReductionOnHitAmount {
            get => SpecialSkillCooldownReductionOnHitAmountConfig.Value;
            protected set => SpecialSkillCooldownReductionOnHitAmountConfig.Value = value;
        }
        public float EquipmentCooldownReductionOnHitAmount {
            get => EquipmentCooldownReductionOnHitAmountConfig.Value;
            protected set => EquipmentCooldownReductionOnHitAmountConfig.Value = value;
        }

        public bool PrimarySkillRecharging {
            get => PrimarySkillRechargingConfig.Value;
            protected set => PrimarySkillRechargingConfig.Value = value;
        }
        public bool SecondarySkillRecharging {
            get => SecondarySkillRechargingConfig.Value;
            protected set => SecondarySkillRechargingConfig.Value = value;
        }
        public bool UtilitySkillRecharging {
            get => UtilitySkillRechargingConfig.Value;
            protected set => UtilitySkillRechargingConfig.Value = value;
        }
        public bool SpecialSkillRecharging {
            get => SpecialSkillRechargingConfig.Value;
            protected set => SpecialSkillRechargingConfig.Value = value;
        }
        public bool EquipmentRecharging {
            get => EquipmentRechargingConfig.Value;
            protected set => EquipmentRechargingConfig.Value = value;
        }



        private float newRechargeStopwatch;
        private float newFinalRechargeInterval;

        private CharacterMaster characterMaster;
        private SurvivorIndex survivorIndex;

        private bool characterSupported;
        private bool checkedForCharacterSupport;

        public void Awake()
        {
            SetStartStats();
            SetConfigWraps();


            On.RoR2.Orbs.LightningOrb.OnArrival += LightningOrb_OnArrival;
            On.RoR2.Orbs.ArrowOrb.OnArrival += ArrowOrb_OnArrival;
            On.RoR2.GenericSkill.RunRecharge += GenericSkill_RunRecharge;


            On.RoR2.PlayerCharacterMasterController.Start += (orig, self) =>
            {
                orig(self);
                SetStartStats();
            };
            On.RoR2.Console.Awake += (orig, self) =>
            {
                CommandHelper.RegisterCommands(self);
                orig(self);
            };
        }

        private void SetStartStats()
        {
            checkedForCharacterSupport = false;
            Debug.Log("Reset CooldownOnHit Stats");

            newFinalRechargeInterval = float.NaN;
            newRechargeStopwatch = float.NaN;

        }

        private void ShowStats()
        {
            Chat.AddMessage("Primary(" + PrimarySkillRecharging + ")");
            Chat.AddMessage("Secondary(" + SecondarySkillRecharging + "): " + SecondaryAbilityCooldownReductionOnHitAmount.ToString());
            Chat.AddMessage("Utility(" + UtilitySkillRecharging + ")");
            Chat.AddMessage("Special(" + SpecialSkillRecharging + "): " + SpecialAbilityCooldownReductionOnHitAmount.ToString());
            Chat.AddMessage("Equipment(" + EquipmentRecharging + ")");

        }

        private void SetConfigWraps()
        {
            PrimarySkillRechargingConfig = Config.Wrap(
                "Cooldowns",
                "PrimarySkillRecharging",
                "Enables normal recharging of primary skill",
                true);
            SecondarySkillRechargingConfig = Config.Wrap(
                "Cooldowns",
                "SecondarySkillRecharging",
                "Enables normal recharging of Secondary Skill",
                false);
            UtilitySkillRechargingConfig = Config.Wrap(
                "Cooldowns",
                "UtilitySkillRecharging",
                "Enables normal recharging of Utility skills",
                true);
            SpecialSkillRechargingConfig = Config.Wrap(
                "Cooldowns",
                "SpecialSkillRecharging",
                "Enables normal recharging of Special Skills",
                false);
            EquipmentRechargingConfig = Config.Wrap(
                "Cooldowns",
                "EquipmentRecharging",
                "W.I.P - Cannot currently be disabled with this mod (Coming in a future major update)\nEnables normal recharging of Equipment",
                true);

            SecondarySkillCooldownReductionOnHitAmountConfig = Config.Wrap(
                "Cooldowns",
                "SecondaryAbilityCooldownReductionOnHitAmount",
                "How many seconds to reduce the Secondary skill(RMB) cooldown by on each hit with the Primary skill.",
                1f);

            SpecialSkillCooldownReductionOnHitAmountConfig = Config.Wrap(
                "Cooldowns",
                "SpecialAbilityCooldownReductionOnHitAmount",
                "How many seconds to reduce the Special Skill(R) cooldown by on each hit with the Secondary skill.",
                2.5f);

            EquipmentCooldownReductionOnHitAmountConfig = Config.Wrap(
                "Cooldowns",
                "EquipmentCooldownReductionOnHitAmount",
                "How many seconds to reduce the Equipment(Q) cooldown by on each hit with the ??? [Special Skill feels too limiting. All skills? Would like some suggestions here",
                1f);
        }

        private void GenericSkill_RunRecharge(On.RoR2.GenericSkill.orig_RunRecharge orig, GenericSkill self, float dt)
        {
            CheckCharacterMaster();
            CheckSupport();


            if (!characterSupported) orig(self, dt);

            var characterBody = characterMaster.GetBody();
            var skillLocator = characterBody.GetComponent<SkillLocator>();

            // If 'self' is an ability that should be recharging, do the normal RunRecharge (And await further instructions)
            if ((skillLocator.FindSkillSlot(self) == SkillSlot.Primary && PrimarySkillRecharging) ||
                    (skillLocator.FindSkillSlot(self) == SkillSlot.Secondary && SecondarySkillRecharging) ||
                    (skillLocator.FindSkillSlot(self) == SkillSlot.Utility && UtilitySkillRecharging) ||
                    (skillLocator.FindSkillSlot(self) == SkillSlot.Special && SpecialSkillRecharging))
            {
                orig(self, dt);

                // If the skill should be recharging based on hits as well, do my weird RunRecharge, otherwise return
                if ((skillLocator.FindSkillSlot(self) == SkillSlot.Secondary && SecondaryAbilityCooldownReductionOnHitAmount == 0) ||
                        (skillLocator.FindSkillSlot(self) == SkillSlot.Special && SpecialAbilityCooldownReductionOnHitAmount == 0))
                {
                    return;
                }
            }

            if (self.stock >= self.maxStock) return;
            if (dt == Time.fixedDeltaTime) return;

            Chat.AddMessage("Secondary or Special currently on cooldown");

            var skillType = typeof(RoR2.GenericSkill);

            var rechargeStopwatchField = skillType.GetField("rechargeStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
            var finalRechargeIntervalField = skillType.GetField("finalRechargeInterval", BindingFlags.NonPublic | BindingFlags.Instance);
            var restockSteplikeMethod = skillType.GetMethod("RestockSteplike", BindingFlags.NonPublic | BindingFlags.Instance);

            if (newRechargeStopwatch == float.NaN) newRechargeStopwatch = GetPrivateFloatFromGenericSkills(self, "rechargeStopwatch");
            if (newFinalRechargeInterval == float.NaN) newFinalRechargeInterval = GetPrivateFloatFromGenericSkills(self, "finalRechargeInterval");

            if (!self.beginSkillCooldownOnSkillEnd || (self.stateMachine.state.GetType() != self.activationState.stateType))
            {
                rechargeStopwatchField.SetValue(self, (float)rechargeStopwatchField.GetValue(self) + dt);
            }
            if ((float)rechargeStopwatchField.GetValue(self) >= (float)finalRechargeIntervalField.GetValue(self))
            {
                restockSteplikeMethod.Invoke(self, null);
            }
        }

        private void CheckCharacterMaster()
        {
            if (characterMaster == null)
            {
                GetSurvivorInfo();
            }
        }

        private void CheckSupport()
        {
            if (checkedForCharacterSupport) return;
            var find = Array.IndexOf<SurvivorIndex>(workingSurvivors, survivorIndex);
            var workingSurvivorString = string.Join(", ", workingSurvivors);
            if (find == (int)SurvivorIndex.None)
            {
                Chat.AddMessage($"This mod currently does not work with {survivorIndex}\nIt currently only works with {workingSurvivorString}");
                characterSupported = false;
            }
            else
            {
                //Chat.AddMessage($"This mod currently does work with {survivorIndex}\nIt currently only works with {workingSurvivorString}");
                characterSupported = true;
            }
            checkedForCharacterSupport = true;
        }

        private void GetSurvivorInfo()
        {
            characterMaster = PlayerCharacterMasterController.instances[0].master;
            survivorIndex = GetSurvivorIndex(characterMaster);
        }

        private SurvivorIndex GetSurvivorIndex(CharacterMaster master)
        {
            var bodyPrefab = master.bodyPrefab;
            var def = SurvivorCatalog.FindSurvivorDefFromBody(bodyPrefab);
            var index = def.survivorIndex;
            
            return index;
        }

        public float GetSkillCooldown(GenericSkill skill)
        {
            float value = GetPrivateFloatFromGenericSkills(skill, "finalRechargeInterval");
            return value;
        }
        public float GetRechargeTimer(GenericSkill skill)
        {
            float value = GetPrivateFloatFromGenericSkills(skill, "rechargeStopwatch");
            return value;
        }

        public float GetPrivateFloatFromGenericSkills(GenericSkill skill, string field)
        {
            return (float)typeof(RoR2.GenericSkill).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(skill);
        }

        public void AlterCooldownByFlatAmount(GenericSkill skill, float amount)
        {
            AlterCooldown(skill, amount);
        }

        public void AlterCooldownByTotalPercentage(GenericSkill skill, float percent)
        {
            var amount = GetSkillCooldown(skill) * percent;
            AlterCooldown(skill, amount);
        }

        public void AlterCooldown(GenericSkill skill, float amount)
        {
            Chat.AddMessage(skill.ToString() + amount.ToString());
            skill.RunRecharge(amount);
            AlteredCooldownChatMessage(skill, amount);
        }

        private void AlteredCooldownChatMessage(GenericSkill skill, float amount)
        {
            var roundedNumber = decimal.Round((decimal)amount, 1, System.MidpointRounding.AwayFromZero);
            string line0 = "Skill: " + skill.skillName;
            string line1 = "Total cooldown: " + GetSkillCooldown(skill).ToString();
            string line2 = "Cooldown Reduction: " + roundedNumber;
            string line3 = "Remaining Cooldown: " + skill.cooldownRemaining;
            Chat.AddMessage(line0 + "\n" + line1 + "\n" + line2 + "\n" + line3);
        }

        private void ArrowOrb_OnArrival(On.RoR2.Orbs.ArrowOrb.orig_OnArrival orig, RoR2.Orbs.ArrowOrb self)
        {
            orig(self);

            if (SecondaryAbilityCooldownReductionOnHitAmount == 0) return;

            var skillLocator = self.attacker.GetComponent<SkillLocator>();
            var skill = skillLocator.secondary;
            AlterCooldownByFlatAmount(skill, SecondaryAbilityCooldownReductionOnHitAmount);
        }
        private void LightningOrb_OnArrival(On.RoR2.Orbs.LightningOrb.orig_OnArrival orig, RoR2.Orbs.LightningOrb self)
        {
            orig(self);

            if (SpecialAbilityCooldownReductionOnHitAmount == 0) return;

            if (self.lightningType == RoR2.Orbs.LightningOrb.LightningType.HuntressGlaive)
            {
                var skillLocator = self.attacker.GetComponent<SkillLocator>();
                var skill = skillLocator.special;
                AlterCooldownByFlatAmount(skill, SpecialAbilityCooldownReductionOnHitAmount);
            }
        }


        private static String SeeConfig {
            get => $"Current config is:\n" +
                $"Primary Skills recharging: { (PrimarySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}\n" +
                $"Secondary Skills recharging: { (SecondarySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}: {(SecondarySkillCooldownReductionOnHitAmountConfig.Value)}\n" +
                $"Utility Skills recharging: { (UtilitySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}\n" +
                $"Special Skills recharging: { (SpecialSkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}: {(SpecialSkillCooldownReductionOnHitAmountConfig.Value)}\n" +
                $"Equipment Skills recharging: { (EquipmentRechargingConfig.Value == true ? "Enabled" : "Disabled")}\n";
        }

        //public static string ChangeConfig<T>(ConfigWrapper<Type> config, string arg, T type) where T : struct
        //{
        //    var previousValue = config.Value;
        //    switch (type)
        //    {
        //        case int i:
        //            int.TryParse(arg, out var parsed);
        //            config.Value = parsed;
        //            return $"integer {i}";
        //        case float f:
        //            return $"float {f}";
        //        case bool b:
        //            return $"bool {b}";
        //        default:
        //            return "no valid type";
        //    }
        //}


        private static void SetDefaultConfig()
        {
            PrimarySkillRechargingConfig.Value = true;
            SecondarySkillRechargingConfig.Value = false;
            UtilitySkillRechargingConfig.Value = true;
            SpecialSkillRechargingConfig.Value = false;
            EquipmentRechargingConfig.Value = true;

            SecondarySkillCooldownReductionOnHitAmountConfig.Value = 1;
            SpecialSkillCooldownReductionOnHitAmountConfig.Value = 3f;
        }



        [ConCommand(commandName = "COH_Primary", flags = ConVarFlags.None, helpText = "Primary Skill configurations.")]
        private static void CCPrimary(ConCommandArgs args)
        {

            if (args.Count == 0)
            {
                Debug.Log($"Primary Skills recharging: { (SecondarySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}");
            }

            if (!bool.TryParse(args[0], out var recharging))
            {
                Debug.Log("Argument was invalid. It should be a boolean (True / False)");
            }
            else
            {
                PrimarySkillRechargingConfig.Value = recharging;
                Debug.Log($"Primary Skills recharging: { (SecondarySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}");
            }
        }

        [ConCommand(commandName = "COH_SetSecondary", flags = ConVarFlags.None, helpText = "Secondary Skill configurations.")]
        private static void CCSetSecondary(ConCommandArgs args)
        {

            args.CheckArgumentCount(2);

            if (!bool.TryParse(args[0], out var recharging))
            {
                Debug.Log("First argument was invalid. It should be a boolean (True / False)");
            }

            else if (!float.TryParse(args[1], out var amount))
            {
                Debug.Log("Second argument was invalid. It should be a positive float (any number 0 or above)");
            }
            else
            {
                SecondarySkillRechargingConfig.Value = recharging;
                SecondarySkillCooldownReductionOnHitAmountConfig.Value = amount;
                Debug.Log($"Secondary Skills recharging: { (SecondarySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}: {(SecondarySkillCooldownReductionOnHitAmountConfig.Value)}");
            }
        }

        [ConCommand(commandName = "COH_SetUtility", flags = ConVarFlags.None, helpText = "Utility Skill configurations.")]
        private static void CCSetUtility(ConCommandArgs args)
        {

            args.CheckArgumentCount(1);

            if (!bool.TryParse(args[0], out var recharging))
            {
                Debug.Log("Argument was invalid. It should be a boolean (True / False)");
            }
            else
            {
                PrimarySkillRechargingConfig.Value = recharging;
                Debug.Log($"Utility Skills recharging: { (UtilitySkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}");
            }
        }

        [ConCommand(commandName = "COH_SetSpecial", flags = ConVarFlags.None, helpText = "Sets Special Skill configurations.")]
        private static void CCSetSpecial(ConCommandArgs args)
        {

            args.CheckArgumentCount(2);

            if (!bool.TryParse(args[0], out var recharging))
            {
                Debug.Log("First argument was invalid. It should be a boolean (True / False)");
            }

            else if (!float.TryParse(args[1], out var amount))
            {
                Debug.Log("Second argument was invalid. It should be a positive float (any number 0 or above)");
            }
            else
            {
                SecondarySkillRechargingConfig.Value = recharging;
                SecondarySkillCooldownReductionOnHitAmountConfig.Value = amount;
                Debug.Log($"Special Skills recharging: { (SpecialSkillRechargingConfig.Value == true ? "Enabled" : "Disabled")}: {(SpecialSkillCooldownReductionOnHitAmountConfig.Value)}");
            }
        }

        [ConCommand(commandName = "COH_SetEquipment", flags = ConVarFlags.None, helpText = "Sets Equipment Skill configurations.")]
        private static void CCSetEquipment(ConCommandArgs args)
        {

            args.CheckArgumentCount(1);

            if (!bool.TryParse(args[0], out var recharging))
            {
                Debug.Log("Argument was invalid. It should be a boolean (True / False)");
            }
            else
            {
                PrimarySkillRechargingConfig.Value = recharging;
                Debug.Log($"Equipment Skills recharging: { (EquipmentRechargingConfig.Value == true ? "Enabled" : "Disabled")}");
            }
        }

        [ConCommand(commandName = "COH_GetConfig", flags = ConVarFlags.None, helpText = "Displays the current configuration.")]
        private static void CCGetConfig(ConCommandArgs args)
        {
            Debug.Log(args.Count != 0
                ? "Does not accept arguments. Did you mean something else?"
                : SeeConfig);
        }

        [ConCommand(commandName = "COH_ResetConfig", flags = ConVarFlags.None, helpText = "Sets the config back to its default state")]
        private static void CCResetConfig(ConCommandArgs args)
        {
            SetDefaultConfig();
            Debug.Log(SeeConfig);
        }
    }
}
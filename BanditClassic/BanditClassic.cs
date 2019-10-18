using BepInEx;
using R2API;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;

using EntityStates.Bandit;
using RoR2.Projectile;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using BepInEx.Configuration;
using System.Collections;
using RoR2.Skills;

namespace BanditClassic
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Moffein.BanditClassic", "Bandit Classic", "1.5.1")]
    public class BanditClassic : BaseUnityPlugin
    {

        public void Start()
        {
            #region cfg
            BlastDamage = base.Config.Wrap<float>("Blast", "Damage", "How much damage Blast deals. Default: 2", 2f);
            BlastMagnetism = base.Config.Wrap<float>("Blast", "Radius", "How wide Blast shots are. Default: 0.2, 0 disables smart collision", 0.2f);
            BlastMaxFireRate = base.Config.Wrap<float>("Blast", "Auto firerate", "Time between shots while autofiring. Default: 0.3", 0.3f);
            BlastMinFireRate = base.Config.Wrap<float>("Blast", "Max firerate", "Time between shots while mashing. Default: 0.2", 0.2f);
            BlastReloadTime = base.Config.Wrap<float>("Blast", "Reload Time", "Time it takes to reload Blast. Default: 1, 0 removes reload", 1f);
            BlastMagSize = base.Config.Wrap<int>("Blast", "Mag Size", "How many slugs Blast can shoot before reloading. Default: 8", 8);
            BlastSpread = base.Config.Wrap<float>("Blast", "Spread", "How much spread each shot adds. Default: 0.25", 0.25f);
            BlastForce = base.Config.Wrap<float>("Blast", "Force", "How much push force each shot has. Default: 1000", 1000f);
            BlastReloadScaling = base.Config.Wrap<bool>("Blast", "Reload Speed Scaling", "Scale reload speed with attack speed. Default: true", true);

            LightsOutCooldown = base.Config.Wrap<float>("Lights Out", "Cooldown", "How long it takes for Lights Out to recharge. Default: 6", 6f);
            LightsOutDamage = base.Config.Wrap<float>("Lights Out", "Damage", "How much damage Lights Out deals. Default: 6", 6f);
            LightsOutDuration = base.Config.Wrap<float>("Lights Out", "Duration", "Length of the shooting animation. Default: 0.6", 0.6f);
            LightsOutForce = base.Config.Wrap<float>("Lights Out", "Force", "How much push force each shot has. Default: 1000", 1000f);
            LightsOutIgnoreArmor = base.Config.Wrap<bool>("Lights Out", "Ignores Armor", "Makes Lights Out ignore armor. This makes it not receive any benefits from Weaken/Shattering Justice. Default: false", false);
            LightsOutReloadsPrimary = base.Config.Wrap<bool>("Lights Out", "Reload Primary On-Use", "Automatically reload your primary when using Lights Out. Default: true", true);

            SmokeCooldown = base.Config.Wrap<float>("Smokebomb", "Cooldown", "How long Smokebomb takes to recharge. Default: 6", 6f);
            SmokeDamage = base.Config.Wrap<float>("Smokebomb", "Damage", "How much damage Smokebomb deals. Default: 1.4", 1.4f);
            SmokeRadius = base.Config.Wrap<float>("Smokebomb", "Radius", "How large the stun aura is. Default: 10", 10f);
            SmokeMinDuration = base.Config.Wrap<float>("Smokebomb", "MinDuration", "How long a player must wait before being able to attack after entering cloak. Default: 0.4", 0.4f);
            SmokeDuration = base.Config.Wrap<float>("Smokebomb", "Invis Duration", "How long Smokebomb's Invisibility lasts. Default: 3", 3f);

            GrenCooldown = base.Config.Wrap<float>("Grenade Toss", "Cooldown", "How long Grenade Toss takes to recharge. Default: 6", 6f);
            GrenDamage = base.Config.Wrap<float>("Grenade Toss", "Damage", "How much damage Grenade Toss deals. Default: 4", 4f);
            GrenRadius = base.Config.Wrap<float>("Grenade Toss", "Radius", "Grenade explosive radius. Default: 8", 8f);
            AcidBombEnabled = base.Config.Wrap<bool>("Grenade Toss", "Weaken on Hit", "Replace Grenade Toss with a weakening Acid Bomb. Default: true", true);

            LightsOutExecute = base.Config.Wrap<bool>("zExperimental: Lights Out Execute", "Execute Low HP Enemies", "Experimental: Lights Out executes enemies below a certain HP. This setting is host-based. Default: false", false);
            LightsOutExecutePercentageBase = base.Config.Wrap<float>("zExperimental: Lights Out Execute", "Execute HP Percentage", "Experimental: HP percentage where Lights Out executes. Default: 0.15", 0.15f);

            LightsOutLevelScalingEnabled = base.Config.Wrap<bool>("zExperimental: Lights Out Damage Scaling", "Damage Scaling", "Experimental: Lights Out gains extra damage with each level. Default: false", false);
            LightsOutLevelScalingRate = base.Config.Wrap<float>("zExperimental: Lights Out Damage Scaling", "Damage Scaling Rate", "Experimental: How much extra damage Lights Out gains each level. Default: 0.1", 0.1f);
            LightsOutLevelScalingMax = base.Config.Wrap<float>("zExperimental: Lights Out Damage Scaling", "Damage Scaling Max", "Experimental: How much extra damage Lights Out can gain in total. Default: 4", 4f);

            #endregion

            GameObject banditBody = Resources.Load<GameObject>("prefabs/characterbodies/banditbody");
            banditBody.GetComponent<CharacterBody>().preferredPodPrefab = Resources.Load<GameObject>("prefabs/networkedobjects/survivorpod");
            SkillLocator skillComponent = banditBody.GetComponent<SkillLocator>();
            GenericSkill[] banditSkills = banditBody.GetComponents<GenericSkill>();

            banditBody.GetComponent<CharacterBody>().baseRegen = 1f;
            banditBody.GetComponent<CharacterBody>().levelRegen = 0.2f;

            #region primary
            SkillDef primaryDef = SkillDef.CreateInstance<SkillDef>();
            primaryDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.Blast));
            var field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(primaryDef.activationState, typeof(EntityStates.Bandit.Blast)?.AssemblyQualifiedName);

            primaryDef.baseRechargeInterval = BlastReloadTime.Value;
            if (BlastReloadTime.Value == 0f)
            {
                primaryDef.baseMaxStock = 1;
            }
            else
            {
                primaryDef.baseMaxStock = BlastMagSize.Value;
                primaryDef.rechargeStock = BlastMagSize.Value;
            }
            Blast.bulletRadius = BlastMagnetism.Value;
            if (BlastMagnetism.Value == 0f)
            {
                Blast.blastSmartCollision = false;
            }
            Blast.spreadBloomValue = BlastSpread.Value;
            Blast.damageCoefficient = BlastDamage.Value;
            Blast.baseMaxDuration = BlastMaxFireRate.Value;
            Blast.baseMinDuration = BlastMinFireRate.Value;
            Blast.force = BlastForce.Value;
            Blast.reloadScaling = BlastReloadScaling.Value;
            primaryDef.skillDescriptionToken = "Fire a powerful slug for <color=#E5C962>" + Blast.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
            if (BlastReloadTime.Value > 0f && BlastMagSize.Value > 1)
            {
                primaryDef.skillDescriptionToken += " Reload every " + BlastMagSize.Value + " shots.";
            }

            primaryDef.skillName = "Blast";
            primaryDef.skillNameToken = "Blast";
            primaryDef.activationStateMachineName = skillComponent.primary.skillFamily.variants[0].skillDef.activationStateMachineName;
            primaryDef.isBullets = true;
            primaryDef.shootDelay = 0;
            primaryDef.beginSkillCooldownOnSkillEnd = false;
            primaryDef.interruptPriority = EntityStates.InterruptPriority.Any;
            primaryDef.isCombatSkill = true;
            primaryDef.noSprint = true;
            primaryDef.canceledFromSprinting = false;
            primaryDef.mustKeyPress = false;
            primaryDef.icon = skillComponent.primary.skillFamily.variants[0].skillDef.icon;
            primaryDef.requiredStock = 1;
            primaryDef.stockToConsume = 1;
            skillComponent.primary.skillFamily.variants[0].skillDef = primaryDef;
            #endregion
            #region secondary
            SkillDef secondaryDef = SkillDef.CreateInstance<SkillDef>();
            secondaryDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.PrepLightsOut));
            field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(secondaryDef.activationState, typeof(EntityStates.Bandit.PrepLightsOut)?.AssemblyQualifiedName);
            secondaryDef.baseRechargeInterval = LightsOutCooldown.Value;
            FireLightsOut.damageCoefficient = LightsOutDamage.Value;
            FireLightsOut.force = LightsOutForce.Value;
            PrepLightsOut.baseDuration = LightsOutDuration.Value;
            FireLightsOut.gainDamageWithLevel = LightsOutLevelScalingEnabled.Value;
            FireLightsOut.gainDamgeWithLevelAmount = LightsOutLevelScalingRate.Value;
            FireLightsOut.gainDamgeWithLevelMax = LightsOutLevelScalingMax.Value;
            PrepLightsOut.reloadPrimary = LightsOutReloadsPrimary.Value;
            if (LightsOutIgnoreArmor.Value)
            {
                FireLightsOut.ignoreArmor = true;
            }
            else
            {
                FireLightsOut.ignoreArmor = false;
            }
            secondaryDef.skillDescriptionToken = "Take aim with a revolver, <color=#E5C962>dealing " + FireLightsOut.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color> If the ability <color=#E5C962>kills an enemy,</color> <color=#95CDE5>skill cooldowns are all reset to 0.</color>";
            if (LightsOutLevelScalingEnabled.Value)
            {
                secondaryDef.skillDescriptionToken += " Gains an extra <color=#E5C962>" + LightsOutLevelScalingRate.Value.ToString("P0").Replace(" ", "") + " damage</color> per level.";
            }
            /* if (LightsOutExecute.Value)
             {
                 secondaryDef.skillDescriptionToken += " Enemies are instantly killed if their <color=#E5C962>health drops below " + LightsOutExecutePercentageBase.Value.ToString("P0").Replace(" ", "") + ".</color>";
             }*/
            if (FireLightsOut.ignoreArmor)
            {
                secondaryDef.skillDescriptionToken += " <color=#E5C962> Ignores armor.</color>";
            }
            secondaryDef.skillNameToken = "Lights Out";
            secondaryDef.skillName = "LightsOut";
            secondaryDef.baseMaxStock = 1;
            secondaryDef.rechargeStock = 1;
            secondaryDef.isBullets = false;
            secondaryDef.shootDelay = 0.3f;
            secondaryDef.activationStateMachineName = skillComponent.secondary.skillFamily.variants[0].skillDef.activationStateMachineName;
            secondaryDef.icon = skillComponent.secondary.skillFamily.variants[0].skillDef.icon;
            secondaryDef.interruptPriority = EntityStates.InterruptPriority.Skill;
            secondaryDef.beginSkillCooldownOnSkillEnd = false;
            secondaryDef.isCombatSkill = true;
            secondaryDef.canceledFromSprinting = false;
            secondaryDef.mustKeyPress = false;
            secondaryDef.requiredStock = 1;
            secondaryDef.stockToConsume = 1;
            skillComponent.secondary.skillFamily.variants[0].skillDef = secondaryDef;
            #endregion
            #region utility
            skillComponent.utility.skillFamily.variants[0].skillDef.baseRechargeInterval = SmokeCooldown.Value;
            skillComponent.utility.skillFamily.variants[0].skillDef.beginSkillCooldownOnSkillEnd = true;
            EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.damageCoefficient = SmokeDamage.Value;
            EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.radius = SmokeRadius.Value;
            EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.minimumStateDuration = SmokeMinDuration.Value;
            EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.duration = SmokeDuration.Value;
            skillComponent.utility.skillFamily.variants[0].skillDef.skillNameToken = "Smokebomb";
            skillComponent.utility.skillFamily.variants[0].skillDef.skillDescriptionToken = "<color=#95CDE5>Turn invisible.</color> After " + EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.duration.ToString("N1") + " seconds or after using another ability, surprise and <color=#E5C962>stun enemies for " + EntityStates.Commando.CommandoWeapon.CastSmokescreenNoDelay.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
            #endregion
            #region special
            SkillDef specialDef = SkillDef.CreateInstance<SkillDef>();
            specialDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.GrenadeToss));
            field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(specialDef.activationState, typeof(EntityStates.Bandit.GrenadeToss)?.AssemblyQualifiedName);

            if (!AcidBombEnabled.Value)
            {
                specialDef.skillNameToken = "Grenade Toss";
            }
            else
            {
                specialDef.skillNameToken = "Acid Bomb";
            }
            specialDef.baseRechargeInterval = GrenCooldown.Value;
            Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().blastRadius = GrenRadius.Value;
            GrenadeToss.damageCoefficient = GrenDamage.Value;
            if (AcidBombEnabled.Value)
            {
                Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileDamage>().damageType = DamageType.WeakOnHit;
            }
            Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().blastProcCoefficient = 1f;
            Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().falloffModel = BlastAttack.FalloffModel.None;

            specialDef.skillDescriptionToken = "Toss an explosive <color=#E5C962>in a straight line</color> for <color=#E5C962>" + GrenadeToss.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
            if (AcidBombEnabled.Value)
            {
                specialDef.skillDescriptionToken += " Enemies caught in the blast are <color=#E5C962>Weakened,</color> reducing their movement speed, armor, and damage.";
            }
            specialDef.skillName = "Grenade";
            specialDef.icon = skillComponent.special.skillFamily.variants[0].skillDef.icon;
            specialDef.shootDelay = 0.08f;
            specialDef.baseMaxStock = 1;
            specialDef.rechargeStock = 1;
            specialDef.isBullets = false;
            specialDef.beginSkillCooldownOnSkillEnd = false;
            specialDef.activationStateMachineName = skillComponent.special.skillFamily.variants[0].skillDef.activationStateMachineName;
            specialDef.interruptPriority = EntityStates.InterruptPriority.Skill;
            specialDef.isCombatSkill = true;
            specialDef.noSprint = false;
            specialDef.canceledFromSprinting = false;
            specialDef.mustKeyPress = false;
            specialDef.requiredStock = 1;
            specialDef.stockToConsume = 1;

            skillComponent.special.skillFamily.variants[0].skillDef = specialDef;
            #endregion
        }
        public void Awake()
        {
   
                var banditDisplay = Resources.Load<GameObject>("prefabs/characterbodies/banditbody").GetComponent<ModelLocator>().modelTransform.gameObject;
                banditDisplay.AddComponent<MenuAnimComponent>();
                SurvivorDef item = new SurvivorDef
                {
                    bodyPrefab = Resources.Load<GameObject>("prefabs/characterbodies/banditbody"),
                    descriptionToken = "The Bandit is a hit-and-run survivor who excels at assassinating single targets.<color=#CCD3E0>\n\n< ! > Blast fires faster if you click faster!\n\n< ! > Dealing a killing blow with Lights Out allows you to chain many skills together, allowing for maximum damage AND safety.\n\n< ! > Use Smokebomb to either run away or to stun many enemies at once.\n\n< ! > Grenade Toss can trigger item effects.</color>",
                    displayPrefab = banditDisplay,
                    primaryColor = new Color(0.8039216f, 0.482352942f, 0.843137264f),
                    unlockableName = ""
                };
                
                SurvivorAPI.AddSurvivor(item);

                On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
                {
                        orig(self, damageInfo);
                        if (damageInfo.inflictor != null && (damageInfo.inflictor.name == "BanditBody(Clone)") &&
                        (damageInfo.damageType == (DamageType.ResetCooldownsOnKill | DamageType.BypassArmor) || damageInfo.damageType == DamageType.ResetCooldownsOnKill))
                        {
                            if (self.alive)
                            {
                                if (LightsOutExecute.Value)
                                {
                                    float executeThreshold = LightsOutExecutePercentageBase.Value;
                                    if (self.body.isElite)
                                    {
                                        executeThreshold += damageInfo.inflictor.GetComponent<CharacterBody>().executeEliteHealthFraction;
                                    }
                                    if (self.isInFrozenState && executeThreshold < (0.3f + LightsOutExecutePercentageBase.Value))
                                    {
                                        executeThreshold = 0.3f + LightsOutExecutePercentageBase.Value;
                                    }

                                    if (self.alive && (self.combinedHealthFraction < executeThreshold))
                                    {
                                        damageInfo.damage = self.health;
                                        damageInfo.damageType = (DamageType.ResetCooldownsOnKill | DamageType.BypassArmor);
                                        damageInfo.procCoefficient = 0f;
                                        damageInfo.crit = true;
                                        orig(self, damageInfo);
                                    }
                                }
                            }
                        }
                };
            base.StartCoroutine(this.FixIce());
        }

        private IEnumerator FixIce()
        {
            GameObject banditBody = Resources.Load<GameObject>("prefabs/characterbodies/banditbody");
            for (; ; )
            {
                if (banditBody != null && banditBody.GetComponent<SetStateOnHurt>() != null && banditBody.GetComponent<SetStateOnHurt>().idleStateMachine.Length != 0)
                {
                    banditBody.GetComponent<SetStateOnHurt>().idleStateMachine[0] = banditBody.GetComponent<SetStateOnHurt>().idleStateMachine[1];
                    yield return null;
                }
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        private class MenuAnimComponent : MonoBehaviour
        {
            internal void OnEnable()
            {
                if (base.gameObject.transform.parent.gameObject.name == "CharacterPad")
                {
                    base.StartCoroutine(this.RevolverAnim());
                }
            }

            private IEnumerator RevolverAnim()
            {
                Animator animator = base.gameObject.GetComponent<Animator>();
                EffectManager.instance.SpawnEffect(Resources.Load<GameObject>("prefabs/effects/smokescreeneffect"), new EffectData
                {
                    origin = base.gameObject.transform.position
                }, false);
                Util.PlaySound("play_bandit_shift_end", base.gameObject);
                this.PlayAnimation("Gesture, Additive", "FireShotgun", "FireShotgun.playbackRate", 1f, animator);
                this.PlayAnimation("Gesture, Override", "FireShotgun", "FireShotgun.playbackRate", 1f, animator);
                yield return new WaitForSeconds(0.48f);
                Util.PlaySound("play_bandit_m1_pump", base.gameObject);
                yield return new WaitForSeconds(0.4f);
                this.PlayAnimation("Gesture, Additive", "PrepRevolver", "PrepRevolver.playbackRate", 0.62f, animator);
                this.PlayAnimation("Gesture, Override", "PrepRevolver", "PrepRevolver.playbackRate", 0.62f, animator);
                Util.PlaySound("play_bandit_m2_load", base.gameObject);
                yield break;
            }

            private void PlayAnimation(string layerName, string animationStateName, string playbackRateParam, float duration, Animator animator)
            {
                int layerIndex = animator.GetLayerIndex(layerName);
                animator.SetFloat(playbackRateParam, 1f);
                animator.PlayInFixedTime(animationStateName, layerIndex, 0f);
                animator.Update(0f);
                float length = animator.GetCurrentAnimatorStateInfo(layerIndex).length;
                animator.SetFloat(playbackRateParam, length / duration);
            }
        }

        private static ConfigWrapper<float> BlastSpread;
        private static ConfigWrapper<float> BlastForce;
        private static ConfigWrapper<float> BlastMinFireRate;
        private static ConfigWrapper<float> BlastMaxFireRate;
        private static ConfigWrapper<float> BlastReloadTime;
        private static ConfigWrapper<int> BlastMagSize;
        private static ConfigWrapper<float> BlastDamage;
        private static ConfigWrapper<float> BlastMagnetism;
        private static ConfigWrapper<bool> BlastReloadScaling;

        private static ConfigWrapper<float> LightsOutCooldown;
        private static ConfigWrapper<float> LightsOutDamage;
        private static ConfigWrapper<float> LightsOutDuration;
        private static ConfigWrapper<float> LightsOutForce;
        private static ConfigWrapper<bool> LightsOutReloadsPrimary;

        private static ConfigWrapper<float> SmokeCooldown;
        private static ConfigWrapper<float> SmokeDamage;
        private static ConfigWrapper<float> SmokeRadius;
        private static ConfigWrapper<float> SmokeMinDuration;
        private static ConfigWrapper<float> SmokeDuration;

        private static ConfigWrapper<float> GrenCooldown;
        private static ConfigWrapper<float> GrenDamage;
        private static ConfigWrapper<float> GrenRadius;

        private static ConfigWrapper<bool> LightsOutIgnoreArmor;
        private static ConfigWrapper<bool> LightsOutExecute;
        private static ConfigWrapper<float> LightsOutExecutePercentageBase;
        
        private static ConfigWrapper<bool> LightsOutLevelScalingEnabled;
        private static ConfigWrapper<float> LightsOutLevelScalingRate;
        private static ConfigWrapper<float> LightsOutLevelScalingMax;

        private static ConfigWrapper<bool> AcidBombEnabled;
    }
}

namespace EntityStates.Bandit
{
    public class Blast : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            base.AddRecoil(-1f * Blast.recoilAmplitude, -2f * Blast.recoilAmplitude, -0.5f * Blast.recoilAmplitude, 0.5f * Blast.recoilAmplitude);
            this.maxDuration = Blast.baseMaxDuration / this.attackSpeedStat;
            this.minDuration = Blast.baseMinDuration / this.attackSpeedStat;
            Ray aimRay = base.GetAimRay();
            base.StartAimMode(aimRay, 2f, false);
            Util.PlaySound(Blast.attackSoundString, base.gameObject);
            base.PlayAnimation("Gesture, Additive", "FireShotgun", "FireShotgun.playbackRate", this.maxDuration * 1.1f);
            base.PlayAnimation("Gesture, Override", "FireShotgun", "FireShotgun.playbackRate", this.maxDuration * 1.1f);
            string muzzleName = "MuzzleShotgun";
            if (Blast.effectPrefab)
            {
                EffectManager.instance.SimpleMuzzleFlash(Blast.effectPrefab, base.gameObject, muzzleName, false);
            }
            if (base.isAuthority)
            {
                new BulletAttack
                {
                    owner = base.gameObject,
                    weapon = base.gameObject,
                    origin = aimRay.origin,
                    aimVector = aimRay.direction,
                    minSpread = 0f,
                    maxSpread = 1f,
                    bulletCount = 1u,
                    procCoefficient = 1f,
                    damage = Blast.damageCoefficient * this.damageStat,
                    force = Blast.force,
                    falloffModel = BulletAttack.FalloffModel.DefaultBullet,
                    tracerEffectPrefab = Blast.tracerEffectPrefab,
                    muzzleName = muzzleName,
                    hitEffectPrefab = Blast.hitEffectPrefab,
                    isCrit = Util.CheckRoll(this.critStat, base.characterBody.master),
                    HitEffectNormal = false,
                    radius = bulletRadius,
                    smartCollision = blastSmartCollision,
                    maxDistance = 500
                }.Fire();
            }
            base.characterBody.AddSpreadBloom(Blast.spreadBloomValue);
            if (Blast.reloadScaling)
            {
                base.characterBody.skillLocator.primary.rechargeStopwatch = base.characterBody.skillLocator.primary.CalculateFinalRechargeInterval() - base.characterBody.skillLocator.primary.CalculateFinalRechargeInterval() / this.attackSpeedStat;
            }
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            this.buttonReleased |= !base.inputBank.skill1.down;
            if (base.fixedAge >= this.maxDuration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (this.buttonReleased && base.fixedAge >= this.minDuration)
            {
                return InterruptPriority.Any;
            }
            return InterruptPriority.Skill;
        }
        public static GameObject effectPrefab = Resources.Load<GameObject>("prefabs/effects/muzzleflashes/muzzleflashbanditshotgun");
        public static GameObject hitEffectPrefab = Resources.Load<GameObject>("prefabs/effects/impacteffects/hitsparkbandit");
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("prefabs/effects/tracers/tracerbanditshotgun");
        public static float damageCoefficient = 1.8f;
        public static float force = 1000f;
        public static int bulletCount = 8;
        public static float baseMaxDuration = 0.3f;
        public static float baseMinDuration = 0.2f;
        public static string attackSoundString = "Play_bandit_M1_shot";
        public static float recoilAmplitude = 1.3f;
        public static float spreadBloomValue = 0.4f;
        public static float bulletRadius = 0.2f;
        public static bool blastSmartCollision = true;
        private float maxDuration;
        private float minDuration;
        private bool buttonReleased;
        public static bool reloadScaling = true;
    }

    public class PrepLightsOut : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = PrepLightsOut.baseDuration / this.attackSpeedStat;
            base.PlayAnimation("Gesture, Additive", "PrepRevolver", "PrepRevolver.playbackRate", this.duration);
            base.PlayAnimation("Gesture, Override", "PrepRevolver", "PrepRevolver.playbackRate", this.duration);
            Util.PlaySound(PrepLightsOut.prepSoundString, base.gameObject);
            this.defaultCrosshairPrefab = base.characterBody.crosshairPrefab;
            base.characterBody.crosshairPrefab = PrepLightsOut.specialCrosshairPrefab;

            if (base.characterBody)
            {
                base.characterBody.SetAimTimer(this.duration);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextState(new FireLightsOut());
                return;
            }
        }

        public override void OnExit()
        {
            if (PrepLightsOut.reloadPrimary)
            {
                base.characterBody.skillLocator.primary.stock = base.characterBody.skillLocator.primary.maxStock;
                base.characterBody.skillLocator.primary.rechargeStopwatch = 0f;
            }
            base.characterBody.crosshairPrefab = this.defaultCrosshairPrefab;
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
        public static float baseDuration = 0.5f;
        public static GameObject specialCrosshairPrefab = Resources.Load<GameObject>("prefabs/crosshair/banditcrosshairrevolver");
        public static string prepSoundString = "Play_bandit_M2_load";
        private float duration;
        private ChildLocator childLocator;
        private GameObject defaultCrosshairPrefab = Resources.Load<GameObject>("prefabs/crosshair/banditcrosshair");
        public static bool reloadPrimary = true;
    }

    public class FireLightsOut : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = FireLightsOut.baseDuration / this.attackSpeedStat;
            base.AddRecoil(-3f * FireLightsOut.recoilAmplitude, -4f * FireLightsOut.recoilAmplitude, -0.5f * FireLightsOut.recoilAmplitude, 0.5f * FireLightsOut.recoilAmplitude);
            Ray aimRay = base.GetAimRay();
            base.StartAimMode(aimRay, 2f, false);
            string muzzleName = "MuzzlePistol";
            Util.PlaySound(FireLightsOut.attackSoundString, base.gameObject);
            base.PlayAnimation("Gesture, Additive", "FireRevolver");
            base.PlayAnimation("Gesture, Override", "FireRevolver");
            if (FireLightsOut.effectPrefab)
            {
                EffectManager.instance.SimpleMuzzleFlash(FireLightsOut.effectPrefab, base.gameObject, muzzleName, false);
            }
            if (base.isAuthority)
            {
                BulletAttack bulletAttack = new BulletAttack();
                bulletAttack.owner = base.gameObject;
                bulletAttack.weapon = base.gameObject;
                bulletAttack.origin = aimRay.origin;
                bulletAttack.aimVector = aimRay.direction;
                bulletAttack.minSpread = FireLightsOut.minSpread;
                bulletAttack.maxSpread = FireLightsOut.maxSpread;
                bulletAttack.bulletCount = (uint)((FireLightsOut.bulletCount > 0) ? FireLightsOut.bulletCount : 0);
                bulletAttack.damage = FireLightsOut.damageCoefficient * this.damageStat;
                bulletAttack.force = FireLightsOut.force;
                bulletAttack.falloffModel = BulletAttack.FalloffModel.None;
                bulletAttack.tracerEffectPrefab = FireLightsOut.tracerEffectPrefab;
                bulletAttack.muzzleName = muzzleName;
                bulletAttack.hitEffectPrefab = FireLightsOut.hitEffectPrefab;
                bulletAttack.isCrit = Util.CheckRoll(this.critStat, base.characterBody.master);
                bulletAttack.HitEffectNormal = false;
                bulletAttack.radius = 0.5f;
                bulletAttack.maxDistance = 500f;

                if (!ignoreArmor)
                {
                    bulletAttack.damageType |= DamageType.ResetCooldownsOnKill;
                }
                else
                {
                    bulletAttack.damageType = (DamageType.ResetCooldownsOnKill | DamageType.BypassArmor);
                }
                bulletAttack.smartCollision = true;

                if (gainDamageWithLevel)
                {
                    float bonusDamage = gainDamgeWithLevelAmount * (base.gameObject.GetComponent<CharacterBody>().level - 1);
                    if (gainDamgeWithLevelMax > 0f && bonusDamage > gainDamgeWithLevelMax)
                    {
                        bonusDamage = gainDamgeWithLevelMax;
                    }
                    bulletAttack.damage = (FireLightsOut.damageCoefficient + bonusDamage) * this.damageStat;
                }
                bulletAttack.Fire();
            }
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public static GameObject effectPrefab = Resources.Load<GameObject>("prefabs/effects/muzzleflashes/muzzleflashbanditpistol");
        public static GameObject hitEffectPrefab = Resources.Load<GameObject>("prefabs/effects/impacteffects/hitsparkbanditpistol");
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("prefabs/effects/tracers/tracerbanditpistol");
        public static float damageCoefficient = 6f;
        public static float force = 1000f;
        public static float minSpread = 0f;
        public static float maxSpread = 0f;
        public static int bulletCount = 1;
        public static float baseDuration = 0.4f;
        public static string attackSoundString = "Play_bandit_M2_shot";
        public static float recoilAmplitude = 1f;
        private ChildLocator childLocator;
        public int bulletCountCurrent = 1;
        private float duration;

        public static bool gainDamageWithLevel = false;
        public static float gainDamgeWithLevelAmount = 0.1f;
        public static float gainDamgeWithLevelMax = 4f;
        public static bool ignoreArmor = false;
    }

    public class GrenadeToss : BaseState
    {
        // Token: 0x060028BB RID: 10427 RVA: 0x000B8EEC File Offset: 0x000B70EC
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = GrenadeToss.baseDuration / this.attackSpeedStat;
            Ray aimRay = base.GetAimRay();
            base.StartAimMode(aimRay, 2f, false);
            base.PlayAnimation("Gesture", "FireRevolver", "FireRevolver.playbackRate", this.duration);
            Util.PlayScaledSound(PrepLightsOut.prepSoundString, base.gameObject, 1.2f);
            if (base.isAuthority)
            {
                ProjectileManager.instance.FireProjectile(GrenadeToss.projectilePrefab, aimRay.origin, Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * GrenadeToss.damageCoefficient, 0f, Util.CheckRoll(this.critStat, base.characterBody.master), DamageColorIndex.Default, null, -1f);
            }
            if (base.characterMotor && !base.characterMotor.isGrounded)
            {
                Vector3 vector = -aimRay.direction * GrenadeToss.selfForce;
                vector.y *= 0.5f;
                base.characterMotor.ApplyForce(vector, true, false);
            }
        }

        // Token: 0x060028BC RID: 10428 RVA: 0x0009B508 File Offset: 0x00099708
        public override void OnExit()
        {
            base.OnExit();
        }

        // Token: 0x060028BD RID: 10429 RVA: 0x000B9016 File Offset: 0x000B7216
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public static GameObject projectilePrefab = Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile");
        public static float damageCoefficient = 3.2f;
        public static float force = 0f;
        public static float selfForce = 0f;
        public static float baseDuration = 0.5f;
        private float duration;
        public int bulletCountCurrent = 1;
    }

    public class ThermiteBomb : BaseState
    {
        // Token: 0x060028BB RID: 10427 RVA: 0x000B8EEC File Offset: 0x000B70EC
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = ThermiteBomb.baseDuration / this.attackSpeedStat;
            Ray aimRay = base.GetAimRay();
            base.StartAimMode(aimRay, 2f, false);
            base.PlayAnimation("Gesture", "FireRevolver", "FireRevolver.playbackRate", this.duration);
            Util.PlayScaledSound(PrepLightsOut.prepSoundString, base.gameObject, 1.2f);
            if (base.isAuthority)
            {
                ProjectileManager.instance.FireProjectile(ThermiteBomb.projectilePrefab, aimRay.origin, Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * ThermiteBomb.damageCoefficient, 0f, Util.CheckRoll(this.critStat, base.characterBody.master), DamageColorIndex.Default, null, -1f);
            }
            if (base.characterMotor && !base.characterMotor.isGrounded)
            {
                Vector3 vector = -aimRay.direction * ThermiteBomb.selfForce;
                vector.y *= 0.5f;
                base.characterMotor.ApplyForce(vector, true, false);
            }
        }

        // Token: 0x060028BC RID: 10428 RVA: 0x0009B508 File Offset: 0x00099708
        public override void OnExit()
        {
            base.OnExit();
        }

        // Token: 0x060028BD RID: 10429 RVA: 0x000B9016 File Offset: 0x000B7216
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public static GameObject projectilePrefab = Resources.Load<GameObject>("prefabs/projectiles/thermite");
        public static float damageCoefficient = 6f;
        public static float force = 0f;
        public static float selfForce = 0f;
        public static float baseDuration = 0.5f;
        private float duration;
        public int bulletCountCurrent = 1;
    }
}
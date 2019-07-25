using BepInEx;
using R2API;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

using EntityStates.Bandit;
using RoR2.Projectile;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using BepInEx.Configuration;

namespace BanditClassic
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Moffein.BanditClassic", "Bandit Classic", "1.1.2")]
    public class BanditClassic : BaseUnityPlugin
    {
        public void Awake()
        {
            #region cfg
            BlastDamage = base.Config.Wrap<float>("Blast", "Damage", "How much damage Blast deals. Default: 1.8", 1.8f);
            BlastMagnetism = base.Config.Wrap<float>("Blast", "Radius", "How wide Blast shots are. Default: 0.2, 0 disables smart collision", 0.2f);
            BlastMaxFireRate = base.Config.Wrap<float>("Blast", "Auto firerate", "Time between shots while autofiring. Default: 0.3", 0.3f);
            BlastMinFireRate = base.Config.Wrap<float>("Blast", "Max firerate", "Time between shots while mashing. Default: 0.2", 0.2f);
            BlastReloadTime = base.Config.Wrap<float>("Blast", "Reload Time", "Time it takes to reload Blast. Default: 1, 0 removes reload", 1f);
            BlastMagSize = base.Config.Wrap<int>("Blast", "Mag Size", "How many slugs Blast can shoot before reloading. Default: 8", 8);
            BlastSpread = base.Config.Wrap<float>("Blast", "Spread", "How much spread each shot adds. Default: 0.4", 0.4f);
            BlastForce = base.Config.Wrap<float>("Blast", "Force", "How much pushing force each shot has. Default: 1000", 1000f);

            LightsOutDamage = base.Config.Wrap<float>("Lights Out", "Damage", "How much damage Lights Out deals. Default: 6", 6f);
            LightsOutDuration = base.Config.Wrap<float>("Lights Out", "Duration", "Length of the shooting animation. Default: 0.5", 0.5f);
            LightsOutForce = base.Config.Wrap<float>("Lights Out", "Force", "How much pushing force each shot has. Default: 1000", 1000f);

            SmokeDamage = base.Config.Wrap<float>("Smokebomb", "Damage", "How much damage Smokebomb deals. Default: 1.4", 1.4f);
            SmokeRadius = base.Config.Wrap<float>("Smokebomb", "Radius", "How large the stun aura is. Default: 10", 10f);
            SmokeMinDuration = base.Config.Wrap<float>("Smokebomb", "MinDuration", "How long a player must wait before being able to attack after entering cloak. Default: 0.5", 0.5f);

            GrenDamage = base.Config.Wrap<float>("Grenade Toss", "Damage", "How much damage Grenade Toss deals. Default: 3.2", 3.2f);
            GrenRadius = base.Config.Wrap<float>("Grenade Toss", "Radius", "Grenade explosive radius. Default: 10", 10f);

            LightsOutExecute = base.Config.Wrap<bool>("zExperimental: Lights Out Execute", "Execute Low HP Enemies", "Experimental: Lights Out executes enemies below a certain HP. Default: false", false);
            LightsOutExecutePercentageBase = base.Config.Wrap<float>("zExperimental: Lights Out Execute", "Execute HP Percentage", "Experimental: HP percentage where Lights Out executes. Default: 0.05", 0.05f);
            LightsOutExecutePercentageScalingRate = base.Config.Wrap<float>("zExperimental: Lights Out Execute", "Execute HP Percentage Scaling Rate", "Experimental: How much the execute threshold increases each level. Default: 0.01", 0.01f);
            LightsOutExecutePercentageScalingMax = base.Config.Wrap<float>("zExperimental: Lights Out Execute", "Execute HP Percentage Scaling Max", "Experimental: Maximum increase for the execute threshold (does not include base execute threshold). Default: 0.05", 0.05f);


            LightsOutLevelScalingEnabled = base.Config.Wrap<bool>("zExperimental: Lights Out Damage Scaling", "Damage Scaling", "Experimental: Lights Out gains extra damage with each level. Default: false", false);
            LightsOutLevelScalingRate = base.Config.Wrap<float>("zExperimental: Lights Out Damage Scaling", "Damage Scaling Rate", "Experimental: How much extra damage Lights Out gains each level. Default: 0.1", 0.1f);
            LightsOutLevelScalingMax = base.Config.Wrap<float>("zExperimental: Lights Out Damage Scaling", "Damage Scaling Max", "Experimental: How much extra damage Lights Out can gain in total. Default: 4", 4f);
            #endregion

            SurvivorAPI.SurvivorCatalogReady += delegate (object s, EventArgs e)
            {
                SurvivorDef item = new SurvivorDef
                {
                    bodyPrefab = BodyCatalog.FindBodyPrefab("BanditBody"),
                    descriptionToken = "The Bandit is a hit-and-run survivor who excels at assassinating single targets.<color=#CCD3E0>\n\n< ! > Blast fires faster if you click faster!\n\n< ! > Dealing a killing blow with Lights Out allows you to chain many skills together, allowing for maximum damage AND safety.\n\n< ! > Use Smokebomb to either run away or to stun many enemies at once.\n\n< ! > Grenade Toss can trigger item effects.</color>",
                    displayPrefab = Resources.Load<GameObject>("prefabs/characterbodies/banditbody").GetComponent<ModelLocator>().modelTransform.gameObject,
                    primaryColor = new Color(0.8039216f, 0.482352942f, 0.843137264f),
                    unlockableName = "",
                    survivorIndex = SurvivorIndex.Count
                };

                item.bodyPrefab.GetComponent<CharacterBody>().preferredPodPrefab = Resources.Load<GameObject>("prefabs/networkedobjects/survivorpod");

                #region skillsetup
                SkillLocator skillComponent = item.bodyPrefab.GetComponent<SkillLocator>();

                GenericSkill primarySkill = skillComponent.primary;
                primarySkill.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.Blast));
                var field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(primarySkill.activationState, typeof(EntityStates.Bandit.Blast)?.AssemblyQualifiedName);

                GenericSkill secondarySkill = skillComponent.secondary;
                secondarySkill.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.PrepLightsOut));
                field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(secondarySkill.activationState, typeof(EntityStates.Bandit.PrepLightsOut)?.AssemblyQualifiedName);

                GenericSkill utilitySkill = skillComponent.utility;
                utilitySkill.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.Smokebomb));
                field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(utilitySkill.activationState, typeof(EntityStates.Bandit.Smokebomb)?.AssemblyQualifiedName);

                GenericSkill specialSkill = skillComponent.special;
                specialSkill.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Bandit.GrenadeToss));
                field = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(specialSkill.activationState, typeof(EntityStates.Bandit.GrenadeToss)?.AssemblyQualifiedName);

                skillComponent.primary.skillNameToken = "Blast";
                skillComponent.secondary.skillNameToken = "Lights Out";
                skillComponent.utility.skillNameToken = "Smokebomb";
                skillComponent.special.skillNameToken = "Grenade Toss";
                #endregion

                #region skilloverwrite
                skillComponent.primary.baseRechargeInterval = BlastReloadTime.Value;
                if (BlastReloadTime.Value == 0f)
                {
                    skillComponent.primary.baseMaxStock = 1;
                }
                else
                {
                    skillComponent.primary.baseMaxStock = BlastMagSize.Value;
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

                skillComponent.secondary.baseRechargeInterval = 7f;
                FireLightsOut.damageCoefficient = LightsOutDamage.Value;
                FireLightsOut.force = LightsOutForce.Value;
                PrepLightsOut.baseDuration = LightsOutDuration.Value;
                FireLightsOut.gainDamageWithLevel = LightsOutLevelScalingEnabled.Value;
                FireLightsOut.gainDamgeWithLevelAmount = LightsOutLevelScalingRate.Value;
                FireLightsOut.gainDamgeWithLevelMax = LightsOutLevelScalingMax.Value;

                skillComponent.utility.baseRechargeInterval = 9f;
                Smokebomb.damageCoefficient = SmokeDamage.Value;
                Smokebomb.radius = SmokeRadius.Value;
                Smokebomb.minimumStateDuration = SmokeMinDuration.Value;

                skillComponent.special.baseRechargeInterval = 4f;
                Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().blastRadius = GrenRadius.Value;
                Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().blastProcCoefficient = 1f;
                Resources.Load<GameObject>("prefabs/projectiles/banditgrenadeprojectile").GetComponent<ProjectileImpactExplosion>().falloffModel = BlastAttack.FalloffModel.None;
                Smokebomb.damageCoefficient = GrenDamage.Value;
                #endregion

                #region skilldesc
                skillComponent.primary.skillDescriptionToken = "Fire a powerful slug for <color=#E5C962>" + Blast.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
                if (BlastReloadTime.Value > 0f && BlastMagSize.Value > 1)
                {
                    skillComponent.primary.skillDescriptionToken += " Reload every " + BlastMagSize.Value + " shots.";
                }
                skillComponent.secondary.skillDescriptionToken = "Take aim with a revolver, <color=#E5C962>dealing " + FireLightsOut.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color> If the ability <color=#E5C962>kills an enemy,</color> <color=#95CDE5>skill cooldowns are all reset to 0.</color>";
                if (LightsOutLevelScalingEnabled.Value)
                {
                    skillComponent.secondary.skillDescriptionToken += " Gains an extra <color=#E5C962>" + LightsOutLevelScalingRate.Value.ToString("P0").Replace(" ", "") + " damage</color> per level.";
                }
                if (LightsOutExecute.Value)
                {
                    skillComponent.secondary.skillDescriptionToken += " Enemies are instantly killed if their <color=#E5C962>health drops below " + LightsOutExecutePercentageBase.Value.ToString("P0").Replace(" ", "") + ".</color>";
                }
                skillComponent.utility.skillDescriptionToken = "<color=#95CDE5>Turn invisible.</color> After " + Smokebomb.duration.ToString("N0") + " seconds or after using another ability, surprise and <color=#E5C962>stun enemies for " + Smokebomb.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
                skillComponent.special.skillDescriptionToken = "Toss an explosive <color=#E5C962>in a straight line</color> for <color=#E5C962>" + GrenadeToss.damageCoefficient.ToString("P0").Replace(" ", "") + " damage.</color>";
                #endregion
                SurvivorAPI.AddSurvivor(item);
            };
            if (LightsOutExecute.Value)
            {
                IL.RoR2.HealthComponent.TakeDamage += (il) =>
                 {
                     var c = new ILCursor(il);
                     c.GotoNext(
                         x => x.MatchLdnull(),
                         x => x.MatchStloc(7),
                         x => x.MatchLdcR4(0.0f),
                         x => x.MatchStloc(8),
                         x => x.MatchLdstr(""),
                         x => x.MatchStloc(9),
                         x => x.MatchLdarg(0)
                         );
                     c.Index += 2;
                     c.Remove();
                     c.Emit(OpCodes.Ldarg_1);
                     c.EmitDelegate<Func<DamageInfo, float>>((di) =>
                     {
                         if (di.inflictor.name == "BanditBody(Clone)" && di.damageType == DamageType.ResetCooldownsOnKill)
                         {
                             if (LightsOutExecutePercentageScalingMax.Value > 0f && LightsOutExecutePercentageScalingRate.Value * (di.inflictor.GetComponent<CharacterBody>().level - 1) > LightsOutExecutePercentageScalingMax.Value)
                             {
                                 return (LightsOutExecutePercentageBase.Value + LightsOutExecutePercentageScalingMax.Value);
                             }
                             else
                             {
                                 return (LightsOutExecutePercentageBase.Value + LightsOutExecutePercentageScalingRate.Value*(di.inflictor.GetComponent<CharacterBody>().level - 1));
                             }
                         }
                         return 0.0f;
                     });
                 };
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

        private static ConfigWrapper<float> LightsOutDamage;
        private static ConfigWrapper<float> LightsOutDuration;
        private static ConfigWrapper<float> LightsOutForce;

        private static ConfigWrapper<float> SmokeDamage;
        private static ConfigWrapper<float> SmokeRadius;
        private static ConfigWrapper<float> SmokeMinDuration;

        private static ConfigWrapper<float> GrenDamage;
        private static ConfigWrapper<float> GrenRadius;

        private static ConfigWrapper<bool> LightsOutExecute;
        private static ConfigWrapper<float> LightsOutExecutePercentageBase;
        private static ConfigWrapper<float> LightsOutExecutePercentageScalingRate;
        private static ConfigWrapper<float> LightsOutExecutePercentageScalingMax;
        
        private static ConfigWrapper<bool> LightsOutLevelScalingEnabled;
        private static ConfigWrapper<float> LightsOutLevelScalingRate;
        private static ConfigWrapper<float> LightsOutLevelScalingMax;
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
                    maxSpread = base.characterBody.spreadBloomAngle,
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
                    smartCollision = blastSmartCollision
                }.Fire();
            }
            base.characterBody.AddSpreadBloom(Blast.spreadBloomValue);
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
                bulletAttack.damageType |= DamageType.ResetCooldownsOnKill;
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
            return InterruptPriority.Any;
        }

        public static GameObject effectPrefab = Resources.Load<GameObject>("prefabs/effects/muzzleflashes/muzzleflashbanditpistol");
        public static GameObject hitEffectPrefab = Resources.Load<GameObject>("prefabs/effects/impacteffects/hitsparkbanditpistol");
        public static GameObject tracerEffectPrefab = Resources.Load<GameObject>("prefabs/effects/tracers/tracerbanditpistol");
        public static float damageCoefficient = 6f;
        public static float force = 1000f;
        public static float minSpread = 0f;
        public static float maxSpread = 0f;
        public static int bulletCount = 1;
        public static float baseDuration = 0.5f;
        public static string attackSoundString = "Play_bandit_M2_shot";
        public static float recoilAmplitude = 1f;
        private ChildLocator childLocator;
        public int bulletCountCurrent = 1;
        private float duration;

        public static bool gainDamageWithLevel = false;
        public static float gainDamgeWithLevelAmount = 0.1f;
        public static float gainDamgeWithLevelMax = 4f;
    }

    public class Smokebomb : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.animator = base.GetModelAnimator();
            this.CastSmoke();
            if (base.characterBody && NetworkServer.active)
            {
                base.characterBody.AddBuff(BuffIndex.Cloak);
                base.characterBody.AddBuff(BuffIndex.CloakSpeed);
            }
        }

        public override void OnExit()
        {
            if (base.characterBody && NetworkServer.active)
            {
                if (base.characterBody.HasBuff(BuffIndex.Cloak))
                {
                    base.characterBody.RemoveBuff(BuffIndex.Cloak);
                }
                if (base.characterBody.HasBuff(BuffIndex.CloakSpeed))
                {
                    base.characterBody.RemoveBuff(BuffIndex.CloakSpeed);
                }
            }
            if (!this.outer.destroying)
            {
                this.CastSmoke();
            }
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            this.stopwatch += Time.fixedDeltaTime;
            if (this.stopwatch >= Smokebomb.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        private void CastSmoke()
        {
            if (!this.hasCastSmoke)
            {
                Util.PlaySound(Smokebomb.startCloakSoundString, base.gameObject);
                this.hasCastSmoke = true;
            }
            else
            {
                Util.PlaySound(Smokebomb.stopCloakSoundString, base.gameObject);
            }
            EffectManager.instance.SpawnEffect(Smokebomb.smokescreenEffectPrefab, new EffectData
            {
                origin = base.transform.position
            }, false);
            int layerIndex = this.animator.GetLayerIndex("Impact");
            if (layerIndex >= 0)
            {
                this.animator.SetLayerWeight(layerIndex, 1f);
                this.animator.PlayInFixedTime("LightImpact", layerIndex, 0f);
            }
            if (NetworkServer.active)
            {
                new BlastAttack
                {
                    attacker = base.gameObject,
                    inflictor = base.gameObject,
                    teamIndex = TeamComponent.GetObjectTeam(base.gameObject),
                    baseDamage = this.damageStat * Smokebomb.damageCoefficient,
                    baseForce = Smokebomb.forceMagnitude,
                    position = base.transform.position,
                    radius = Smokebomb.radius,
                    falloffModel = BlastAttack.FalloffModel.None,
                    damageType = DamageType.Stun1s
                }.Fire();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (this.stopwatch <= Smokebomb.minimumStateDuration)
            {
                return InterruptPriority.PrioritySkill;
            }
            return InterruptPriority.Any;
        }

        public static float duration = 3f;
        public static float minimumStateDuration = 0.5f;
        public static string startCloakSoundString = "Play_bandit_shift_land";
        public static string stopCloakSoundString = "Play_bandit_shift_end";
        public static GameObject smokescreenEffectPrefab = Resources.Load<GameObject>("prefabs/effects/smokescreeneffect");
        public static float damageCoefficient = 1.4f;
        public static float radius = 10f;
        public static float forceMagnitude = 100f;
        private float stopwatch;
        private bool hasCastSmoke;
        private Animator animator;
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
}
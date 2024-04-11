using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using HarmonyLib;
using Objects.Pipes;
using UnityEngine;
using SysOpCodes = System.Reflection.Emit.OpCodes;

namespace StationeersAdiabatics
{
    [HarmonyPatch]
    public class MoveAtmosPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var candidateMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(DeviceAtmospherics)))
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            
            var methods = candidateMethods
                .Where(
                    method =>
                        method.Name.Equals("ReleaseToAtmos") ||
                        method.Name.Equals("HandleGasInput") ||
                        method.Name.Equals("MovePropellant") ||
                        method.Name.Equals("MoveAtmosphere")
                )
                .Cast<MethodBase>().Distinct();
            foreach (var m in methods)
            {
                Debug.Log($"Patching    {m.ReflectedType} \t {m.Name}");
            }

            return methods;
        }
        
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {

            foreach (var instruction in instructions)
            {

                if (instruction.opcode != SysOpCodes.Call)
                {
                    yield return instruction;
                    continue;
                }

                var method = (MethodInfo)instruction.operand;
                if (method.Name != "MoveVolume")
                {
                    yield return instruction;
                    continue;
                }
                Debug.Log($"{original.ReflectedType}.{original.Name} Patched");

                yield return new CodeInstruction(SysOpCodes.Ldarg_0);
                yield return new CodeInstruction(SysOpCodes.Call,
                    typeof(MoveAtmosPatch).GetMethod("PatchedMoveVolume",
                        BindingFlags.Static | BindingFlags.NonPublic));
            }
            
        }

        static Atmosphere mix(Atmosphere inputAtmos, Atmosphere outputAtmos, AtmosphereHelper.MatterState matterState)
        {
            Atmosphere ret = new Atmosphere();
            ret.Volume = inputAtmos.Volume + outputAtmos.Volume;
            float num1 = 0.0f;
            ret.GasMixture.Add(inputAtmos.GasMixture, matterState);
            float num2 = num1 + inputAtmos.GetVolume(matterState);
            ret.GasMixture.Add(outputAtmos.GasMixture, matterState);
            return ret;
        }

        static void PatchedMoveVolume(Atmosphere inputAtmos,
            Atmosphere outputAtmos,
            float volume,
            AtmosphereHelper.MatterState matterStateToMove,
            DeviceAtmospherics device)
        {
            float inputP0 = inputAtmos.PressureGassesAndLiquidsInPa;
            float inputn0 = inputAtmos.TotalMoles;
            float inputT0 = inputAtmos.Temperature;
            float outputP0 = inputAtmos.PressureGassesAndLiquidsInPa;
            float outputn0 = inputAtmos.TotalMoles;

            if (inputP0.IsDenormalOrZero())
            {
                device.UsedPower = 0f;
                return;
            }
            var g = inputAtmos.GasMixture.HeatCapacityRatio();

            AtmosphereHelper.MoveVolume(inputAtmos, outputAtmos, volume, matterStateToMove);
            
            
            double work;
            if (inputAtmos.PressureGassesAndLiquidsInPa > outputAtmos.PressureGassesAndLiquidsInPa)
            {
                work = 0f;
            }
            else if (inputP0 > outputP0 && inputAtmos.PressureGassesAndLiquidsInPa < outputAtmos.PressureGassesAndLiquidsInPa)
            {
                // Debug.Log("Partial Free");
                var equiPressure = mix(inputAtmos, outputAtmos, matterStateToMove);
                
                var compressedVolume = (device.OutputSetting / 1000f) *
                                       Math.Pow(equiPressure.PressureGassesAndLiquidsInPa / outputAtmos.PressureGassesAndLiquidsInPa, g);//Math.Pow(inputP0 / outputAtmos.PressureGassesAndLiquidsInPa, g);
                var movedMoles = equiPressure.TotalMoles - inputAtmos.TotalMoles;
                var Cv = inputAtmos.GasMixture.HeatCapacity / inputAtmos.TotalMoles;
                var squeezeWork = -movedMoles * Cv *
                                  (equiPressure.Temperature * ((outputAtmos.PressureGassesAndLiquidsInPa * compressedVolume) /
                                              (equiPressure.PressureGassesAndLiquidsInPa * (device.OutputSetting / 1000f)))
                                   - inputT0);
                var pushWork = outputAtmos.PressureGassesAndLiquidsInPa * compressedVolume;
                work = squeezeWork + pushWork;
            }
            else
            {
                var compressedVolume = (device.OutputSetting / 1000f) *
                                       Math.Pow(inputP0 / outputAtmos.PressureGassesAndLiquidsInPa, g);//Math.Pow(inputP0 / outputAtmos.PressureGassesAndLiquidsInPa, g);
                var movedMoles = inputn0 - inputAtmos.TotalMoles;
                var Cv = inputAtmos.GasMixture.HeatCapacity / inputAtmos.TotalMoles;
                var squeezeWork = -movedMoles * Cv *
                                  (inputT0 * ((outputAtmos.PressureGassesAndLiquidsInPa * compressedVolume) /
                                              (inputP0 * (device.OutputSetting / 1000f)))
                                   - inputT0);
                var pushWork = outputAtmos.PressureGassesAndLiquidsInPa * compressedVolume;
                work = squeezeWork + pushWork;
            }
            
            if (work > 0)
            {
                outputAtmos.GasMixture.AddEnergy((float)work);
            }
            else
            {
                
                outputAtmos.GasMixture.RemoveEnergy(-(float)work);
            }

            if (!(((float)work).IsDenormalOrZero() || float.IsNaN((float)work)))
                device.UsedPower = (float)work * 1.1f;
        }
    }
    
    [HarmonyPatch(typeof(AtmosphericsManager))]
    public class AtmosphericsManagerAddPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("DisplayBasicAtmosphere")]
        [HarmonyPatch(new Type[] { typeof(Atmosphere), typeof(StringBuilder), typeof(Pipe.ContentType) })]
        public static void PostfixAddMix(ref StringBuilder stringBuilder, Atmosphere atmosphere)
        {
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
                atmosphere.GasMixture.TotalEnergy.ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
                (atmosphere.GasMixture.TotalEnergy / atmosphere.GasMixture.TotalMolesGassesAndLiquids).ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
                atmosphere.GasMixture.HeatCapacity.ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
                atmosphere.GasMixture.TotalMolesGassesAndLiquids.ToString());
        }
    }
    
    [HarmonyPatch(typeof(VolumePump))]
    public class VolumePumpGetUsedPowerPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var candidateMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(DeviceAtmospherics)))
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));

            var methods = candidateMethods
                .Where(
                    method => method.ReflectedType.Name.Equals("VolumePump") ||
                              method.ReflectedType.Name.Equals("TurboVolumePump")
                )
                .Where(
                    method =>
                        method.Name.Equals("GetUsedPower")
                )
                .Cast<MethodBase>().Distinct();
            foreach (var m in methods)
            {
                Debug.Log($"Adiabatics Patching    {m.ReflectedType} \t {m.Name}");
            }

            return methods;
        }
        
        [HarmonyPostfix]
        public static void PostfixAddMix(ref float __result, VolumePump __instance)
        {
            if (__result != 0)
            {
                
                __result = __instance.UsedPower;
            }
        }
    }
}
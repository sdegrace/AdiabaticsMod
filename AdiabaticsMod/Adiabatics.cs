using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// using System.Reflection.Metadata;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Localization2;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Objects.Pipes;
using UnityEngine;
using SysOpCodes = System.Reflection.Emit.OpCodes;

// using Mono.Cecil;
// using Mono.Cecil.Cil;

namespace StationeersAdiabatics
{
    [HarmonyPatch]
    // [HarmonyPatch(typeof(VolumePump))]
    public class MoveAtmosPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var candidateMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(DeviceAtmospherics)))
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            // foreach (var candidate in candidateMethods)
            // {
            //     Debug.Log($"Candidate {candidate.ReflectedType} . {candidate.Name}");
            // }
            var methods = candidateMethods
                .Where(
                    method =>
                        method.Name.Equals("ReleaseToAtmos") ||
                        method.Name.Equals("HandleGasInput") ||
                        // method.Name.StartsWith("MoveVolume") ||
                        method.Name.Equals("MovePropellant") ||
                        method.Name.Equals("MoveAtmosphere")
                    // method.Name.StartsWith("MoveAtmosphere")
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
            // TODO: We should make a post-processor for HandleRollAudio and trigger roll audio if
            //  - It has not been triggered
            //  - we have a roll axis value

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
                // yield return instruction;
                // yield return new CodeInstruction(OpCodes.Call, typeof(MoveAtmosPatch).GetMethod("After", BindingFlags.Static | BindingFlags.NonPublic));
                // yield return new CodeInstruction(OpCodes.Stloc_0);
            }
            
        }

        // static double getAdiabaticIndex(Atmosphere atmosphere)
        // {
        //     float gassesAndLiquids = atmosphere.TotalMoles;
        //     return (double) gassesAndLiquids <= 1.0 / 1000.0 ? 0.0f : (float) ((double) atmosphere.GasMixture.Oxygen.HeatCapacityRatio() * ((double) atmosphere.GasMixture.Oxygen.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Nitrogen.ThermalEfficiency() * ((double) atmosphere.GasMixture.Nitrogen.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.CarbonDioxide.ThermalEfficiency() * ((double) atmosphere.GasMixture.CarbonDioxide.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Volatiles.ThermalEfficiency() * ((double) atmosphere.GasMixture.Volatiles.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Pollutant.ThermalEfficiency() * ((double) atmosphere.GasMixture.Pollutant.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Water.ThermalEfficiency() * ((double) atmosphere.GasMixture.Water.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.PollutedWater.ThermalEfficiency() * ((double) atmosphere.GasMixture.PollutedWater.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.NitrousOxide.ThermalEfficiency() * ((double) atmosphere.GasMixture.NitrousOxide.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidNitrogen.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidNitrogen.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidOxygen.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidOxygen.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidVolatiles.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidVolatiles.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Steam.ThermalEfficiency() * ((double) atmosphere.GasMixture.Steam.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidCarbonDioxide.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidCarbonDioxide.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidPollutant.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidPollutant.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidNitrousOxide.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidNitrousOxide.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.Hydrogen.ThermalEfficiency() * ((double) atmosphere.GasMixture.Hydrogen.Quantity / (double) gassesAndLiquids) + (double) atmosphere.GasMixture.LiquidHydrogen.ThermalEfficiency() * ((double) atmosphere.GasMixture.LiquidHydrogen.Quantity / (double) gassesAndLiquids));
        //
        // }

        static Atmosphere mix(Atmosphere inputAtmos, Atmosphere outputAtmos, AtmosphereHelper.MatterState matterState)
        {
            Atmosphere ret = new Atmosphere();
            ret.Volume = inputAtmos.Volume + outputAtmos.Volume;
            float num1 = 0.0f;
            ret.GasMixture.Add(inputAtmos.GasMixture, matterState);
            float num2 = num1 + inputAtmos.GetVolume(matterState);
            ret.GasMixture.Add(outputAtmos.GasMixture, matterState);
            // float num3 = num2 + outputAtmos.GetVolume(matterState);
            // GasMixture gasMixture2 = new GasMixture(ret.GasMixture);
            // gasMixture2.Scale(inputAtmos.GetVolume(matterState) / num3, matterState);
            // // inputAtmos.GasMixture.Set(gasMixture2, matterState);
            // GasMixture gasMixture3 = new GasMixture(ret.GasMixture);
            // gasMixture3.Scale(outputAtmos.GetVolume(matterState) / num3, matterState);
            // outputAtmos.GasMixture.Set(gasMixture3, matterState);
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
            
            
            // Debug.Log($"CompressedVolume {compressedVolume}");
            double work;
            if (inputAtmos.PressureGassesAndLiquidsInPa > outputAtmos.PressureGassesAndLiquidsInPa)
            {
                // Debug.Log("Free Expansion");
                work = 0f;
            }
            else if (inputP0 > outputP0 && inputAtmos.PressureGassesAndLiquidsInPa < outputAtmos.PressureGassesAndLiquidsInPa)
            {
                // Debug.Log("Partial Free");
                var equiPressure = mix(inputAtmos, outputAtmos, matterStateToMove);
                
                // var compressedVolume = Math.Pow(equiPressure.PressureGassesAndLiquidsInPa / outputAtmos.PressureGassesAndLiquidsInPa, g);
                // work = -(Math.Pow(compressedVolume, -g + 1) - Math.Pow(equiPressure.PressureGassesAndLiquidsInPa, -g + 1)) / (-g + 1);
                
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
                // Debug.Log($"Equipresure {equiPressure.PressureGassesAndLiquidsInPa} g {g}");
            }
            else
            {
                // Debug.Log("Total Work");
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
                // work = -(float)(Math.Pow(compressedVolume, -g + 1) - Math.Pow(device.OutputSetting, -g + 1)) / (-g + 1);
                // Debug.Log($"squeezeWork {squeezeWork} pushwork {pushWork} compressedVol {compressedVolume} molesMoved {movedMoles} Cv {Cv} g {g}");
                // Debug.Log($"SqueezeWork = {movedMoles} * {Cv} * ({inputT0} * (({outputAtmos.PressureGassesAndLiquidsInPa} * {compressedVolume}) / ({inputP0} * {device.OutputSetting/1000f})) - {inputT0})");
                // Debug.Log($"PushWork =  {outputAtmos.PressureGassesAndLiquidsInPa} * {compressedVolume}");
            }
            // Debug.Log($"Work {work}");
            //var work = -(float)(Math.Pow(compressedVolume, -g + 1) - Math.Pow(inputP0, -g + 1)) / (-g + 1);

            // float work;
            // if (inputAtmos.PressureGassesAndLiquidsInPa.IsDenormalOrZero())
            // {
            //     work = -Math.Abs(outputAtmos.GasMixture.TotalMolesGasses - outputn0) *
            //         8.3144f *
            //         outputAtmos.Temperature *
            //         (outputAtmos.PressureGassesAndLiquidsInPa - outputP0) / outputAtmos.PressureGassesAndLiquidsInPa;
            //     Debug.Log(
            //         $"Mix {work} = {outputAtmos.GasMixture.TotalMolesGasses}n * 8.3 * {outputAtmos.Temperature}T * ({outputAtmos.PressureGassesAndLiquidsInPa} - {outputP0})dP / {outputAtmos.PressureGassesAndLiquidsInPa}P");
            // }
            // else
            // {
            //     work = Math.Abs(inputAtmos.GasMixture.TotalMolesGasses - inputn0) *
            //         8.3144f *
            //         inputAtmos.Temperature *
            //         (inputAtmos.PressureGassesAndLiquidsInPa - inputP0) / inputAtmos.PressureGassesAndLiquidsInPa;
            //     Debug.Log(
            //         $"Mix {work} = {inputAtmos.GasMixture.TotalMolesGasses}n * 8.3 * {inputAtmos.Temperature}T * ({inputAtmos.PressureGassesAndLiquidsInPa} - {inputP0})dP / {inputAtmos.PressureGassesAndLiquidsInPa}P");
            // }

            // Debug.Log($"Postfix {inputAtmos.PressureGasses} {inputAtmos.PressureGassesAndLiquidsInPa} {inputP0}");
            if (work > 0)
            {
                outputAtmos.GasMixture.AddEnergy((float)work);
                // inputAtmos.GasMixture.RemoveEnergy(work);
            }
            else
            {
                
                outputAtmos.GasMixture.RemoveEnergy(-(float)work);
                // inputAtmos.GasMixture.AddEnergy(-work);
            }

            // Debug.Log($"PrePower {device.UsedPower}");
            if (!(((float)work).IsDenormalOrZero() || float.IsNaN((float)work)))
                device.UsedPower = (float)work * 1.1f;
            // Debug.Log($"PostPower {device.UsedPower}");
        }
        // [HarmonyPatch]
        // [HarmonyPrefix]
        // [HarmonyPatch("MoveVolume")]
        // [HarmonyPatch(new Type[] { typeof(Atmosphere), typeof(StringBuilder), typeof(Pipe.ContentType) })]
        // static void Before(MethodBase __originalMethod, Atmosphere inputAtmosphere, Atmosphere outputAtmosphere, out float[] __state)
        // {
        //
        //     __state = new float[]
        //     {
        //         inputAtmos.PressureGassesAndLiquidsInPa, inputAtmos.TotalMoles,
        //         outputAtmos.PressureGassesAndLiquidsInPa, outputAtmos.TotalMoles
        //     };
        // }


        // [HarmonyPatch]
        // [HarmonyPostfix]
        // [HarmonyPatch("MoveVolume")]
        // [HarmonyPatch(new Type[] { typeof(Atmosphere), typeof(StringBuilder), typeof(Pipe.ContentType) })]
        // static void After(Atmosphere inputAtmos, Atmosphere outputAtmos, float[] __state, DeviceAtmospherics __instance)
        // {
        //     var work = Math.Abs(inputAtmos.GasMixture.TotalMolesGasses - __state[1]) *
        //         8.3144f *
        //         inputAtmos.Temperature *
        //         (inputAtmos.PressureGassesAndLiquidsInPa - __state[0]) / inputAtmos.PressureGassesAndLiquidsInPa;
        //     Debug.Log($"Mix {work} = {inputAtmos.GasMixture.TotalMolesGasses} * 8.3 * {inputAtmos.Temperature} * ({inputAtmos.PressureGassesAndLiquidsInPa} - {__state}) / {inputAtmos.PressureGassesAndLiquidsInPa}");
        //     Debug.Log($"Postfix {inputAtmos.PressureGasses} {inputAtmos.PressureGassesAndLiquidsInPa} {__state}");
        //     if (work > 0)
        //     {
        //         inputAtmos.GasMixture.AddEnergy(work);
        //         outputAtmos.GasMixture.RemoveEnergy(work);
        //     }
        //     else
        //     {
        //         inputAtmos.GasMixture.RemoveEnergy(work);
        //         outputAtmos.GasMixture.AddEnergy(work);
        //     }
        //
        //     __instance.UsedPower = work * 1.1f;
        // }
    }
    // [HarmonyPatch]
    // public class AtmosphereHelperPatch
    // {
    //     [HarmonyTargetMethods]
    //     static IEnumerable<MethodBase> TargetMethods()
    //     {
    //         return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
    //             .SelectMany(type => type.GetMethods())
    //             .Where(method => method.DeclaringType.Name.Equals("AtmosphereHelper") && method.Name.Equals("Mix"))
    //             .Cast<MethodBase>();
    //     }
    //
    //     [HarmonyPatch]
    //     [HarmonyPrefix]
    //     static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere inputAtmos,
    //         Atmosphere outputAtmos, out float[] __state)
    //     {
    //         __state = new float[]
    //         {
    //             inputAtmos.PressureGassesAndLiquidsInPa, inputAtmos.TotalMoles,
    //             outputAtmos.PressureGassesAndLiquidsInPa, outputAtmos.TotalMoles
    //         };
    //     }
    //
    // [HarmonyPatch]
    //             [HarmonyPostfix]
    //             static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere inputAtmos, Atmosphere outputAtmos, float[] __state)
    //             {
    //                 var work = Math.Abs(inputAtmos.GasMixture.TotalMolesGasses - __state[1]) *
    //                     8.3144f *
    //                     inputAtmos.Temperature *
    //                     (inputAtmos.PressureGassesAndLiquidsInPa - __state[0]) / inputAtmos.PressureGassesAndLiquidsInPa;
    //                 Debug.Log($"Mix {work} = {inputAtmos.GasMixture.TotalMolesGasses} * 8.3 * {inputAtmos.Temperature} * ({inputAtmos.PressureGassesAndLiquidsInPa} - {__state}) / {inputAtmos.PressureGassesAndLiquidsInPa}");
    //                 Debug.Log($"Postfix {inputAtmos.PressureGasses} {inputAtmos.PressureGassesAndLiquidsInPa} {__state}");
    //                 if (work > 0)
    //                 {
    //                     inputAtmos.GasMixture.AddEnergy(work);
    //                     outputAtmos.GasMixture.RemoveEnergy(work);
    //                 }
    //                 else
    //                 {
    //                     inputAtmos.GasMixture.RemoveEnergy(work);
    //                     outputAtmos.GasMixture.AddEnergy(work);
    //                 }
    //             }
    // }

    // [HarmonyPatch]
    // public class AtmosphereAddPatch
    // {
    //     [HarmonyTargetMethods]
    //     static IEnumerable<MethodBase> TargetMethods()
    //     {
    //         return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
    //             .SelectMany(type => type.GetMethods())
    //             .Where(method => method.DeclaringType.Name.Equals("Atmosphere") && method.Name.Equals("Add"))
    //             .Cast<MethodBase>();
    //     }
    //
    //     [HarmonyPatch]
    //     [HarmonyPrefix]
    //     static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, out float[] __state)
    //     {
    //         __state = new float[] { __instance.PressureGassesAndLiquidsInPa, __instance.TotalMoles };
    //     }
    //
    //     [HarmonyPatch]
    //     [HarmonyPostfix]
    //     static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, float[] __state)
    //     {
    //         __instance.
    //         var work = Math.Abs(__instance.GasMixture.TotalMolesGasses - __state[1]) *
    //             8.3144f *
    //             __instance.Temperature *
    //             (__instance.PressureGassesAndLiquidsInPa - __state[0]) / __instance.PressureGassesAndLiquidsInPa;
    //         Debug.Log($"Add {work} = {__instance.GasMixture.TotalMolesGasses} * 8.3 * {__instance.Temperature} * ({__instance.PressureGassesAndLiquidsInPa} - {__state}) / {__instance.PressureGassesAndLiquidsInPa}");
    //         Debug.Log($"Postfix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa} {__state}");
    //         if (work > 0)
    //         {
    //             __instance.GasMixture.AddEnergy(work);
    //         }
    //         else
    //         {
    //             __instance.GasMixture.RemoveEnergy(work);
    //         }
    //     }
    // }

    // [HarmonyPatch]
    // public class AtmosphereRemovePatch
    // {
    //     [HarmonyTargetMethods]
    //     static IEnumerable<MethodBase> TargetMethods()
    //     {
    //         return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
    //             .SelectMany(type => type.GetMethods())
    //             .Where(method => method.DeclaringType.Name.Equals("Atmosphere") && method.Name.Equals("Remove"))
    //             .Cast<MethodBase>();
    //     }
    //
    //     [HarmonyPatch]
    //     [HarmonyPrefix]
    //     static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, out float[] __state)
    //     {
    //         Debug.Log($"Prefix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa}");
    //         __state = new float[] { __instance.PressureGassesAndLiquidsInPa, __instance.TotalMoles };
    //     }
    //     
    //     [HarmonyPatch]
    //     [HarmonyPostfix]
    //     static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, float[] __state)
    //     {
    //         var work = Math.Abs(__instance.GasMixture.TotalMolesGasses - __state[1]) *
    //             8.3144f *
    //             __instance.Temperature *
    //             (__instance.PressureGassesAndLiquidsInPa - __state[0]) / __instance.PressureGassesAndLiquidsInPa;
    //         Debug.Log($"Remove {work} = {__instance.GasMixture.TotalMolesGasses} * 8.3 * {__instance.Temperature} * ({__instance.PressureGassesAndLiquidsInPa} - {__state}) / {__instance.PressureGassesAndLiquidsInPa}");
    //         Debug.Log($"Postfix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa} {__state}");
    //         if (work > 0)
    //         {
    //             __instance.GasMixture.AddEnergy(work);
    //         }
    //         else
    //         {
    //             __instance.GasMixture.RemoveEnergy(work);
    //         }
    //     }
    // }
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
            // foreach (var candidate in candidateMethods)
            // {
            //     Debug.Log($"Candidate {candidate.ReflectedType} . {candidate.Name}");
            // }
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
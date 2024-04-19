using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Atmospherics;
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
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                    BindingFlags.NonPublic));

            var methods = candidateMethods
                .Where(
                    method =>
                        method.Name.Equals("ReleaseToAtmos") ||
                        method.Name.Equals("HandleGasInput") ||
                        method.Name.Equals("MovePropellant") ||
                        method.Name.Equals("MoveAtmosphere") ||
                        method.Name.Equals("AtmosphericsProcessing") ||
                        (method.ReflectedType.Name.Equals("PressureRegulator") &&
                         method.Name.Equals("OnAtmosphericTick"))
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
                if (method.Name != "MoveVolume" &&
                    method.Name != "MoveRegulatedGas")
                {
                    yield return instruction;
                    continue;
                }

                Debug.Log($"{original.ReflectedType}.{original.Name} Patched with ({"Patched" + method.Name})");

                yield return new CodeInstruction(SysOpCodes.Ldarg_0);
                yield return new CodeInstruction(SysOpCodes.Call,
                    typeof(MoveAtmosPatch).GetMethod("Patched" + method.Name,
                        BindingFlags.Static | BindingFlags.NonPublic));
            }
        }


        static void PatchedMoveRegulatedGas(
            Atmosphere input,
            Atmosphere output,
            float pressurePerTick,
            float setting,
            RegulatorType regulatorType,
            AtmosphereHelper.MatterState movedContent,
            DeviceAtmospherics device
        )

        {
            Atmosphere inputAtmos, outputAtmos;
            if (regulatorType == RegulatorType.Upstream)
            {
                inputAtmos = input;
                outputAtmos = output;
            }
            else
            {
                inputAtmos = output;
                outputAtmos = input;
            }

            float inputP0 = inputAtmos.PressureGassesAndLiquidsInPa;
            float inputn0 = inputAtmos.TotalMoles;
            float inputT0 = inputAtmos.Temperature;
            float outputP0 = outputAtmos.PressureGassesAndLiquidsInPa;
            float pumpVol = 10 / 1000;

            Debug.Log("regulator");

            if (inputP0.IsDenormalOrZero())
            {
                device.UsedPower = 10f;
                return;
            }

            var g = inputAtmos.GasMixture.HeatCapacityRatio();

            AtmosphereHelper.MoveRegulatedGas(inputAtmos, outputAtmos, pressurePerTick, setting, regulatorType,
                movedContent);

            float inputPf = inputAtmos.PressureGassesAndLiquidsInPa;
            float inputnf = inputAtmos.TotalMoles;
            float outputPf = outputAtmos.PressureGassesAndLiquidsInPa;
            var Cv = inputAtmos.GasMixture.HeatCapacity / inputAtmos.TotalMoles;

            double work;
            if (inputPf > outputPf)
            {
                work = 0f;
                Debug.Log("with Gradient 0");
            }
            else if (inputP0 > outputP0 &&
                     inputPf < outputPf)
            {
                var equiPressure = Helpers.Mix(inputAtmos, outputAtmos, movedContent);

                work = Helpers.getCompressiveWork(
                    equiPressure.PressureGassesAndLiquidsInPa,
                    equiPressure.TotalMoles,
                    equiPressure.Temperature, inputnf, outputPf, g, Cv, pumpVol);
                Debug.Log($"Against Gradient at first {work}");
            }
            else
            {
                work = Helpers.getCompressiveWork(inputP0, inputn0, inputT0, inputnf, outputPf, g, Cv,
                    pumpVol);
                Debug.Log($"Against Gradient ({inputP0} -> {outputP0}) {work}  {inputT0}, {inputnf}, {outputPf}, {g}, {Cv}, {pumpVol}"); }

            Debug.Log($"Final {work} J Work by moving {inputn0 - inputnf} Moles");

            if (work > 0)
            {
                outputAtmos.GasMixture.AddEnergy((float)work);
            }
            else
            {
                outputAtmos.GasMixture.RemoveEnergy(-(float)work);
            }

            // if (!(((float)work).IsDenormalOrZero() || float.IsNaN((float)work)))
            //     device.UsedPower = (float)work * 1.1f;
            // else
            // {
            //     device.UsedPower = 10f;
            // }
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
            float outputP0 = outputAtmos.PressureGassesAndLiquidsInPa;
            float pumpVol = device.OutputSetting / 1000;

            if (inputP0.IsDenormalOrZero())
            {
                device.UsedPower = 10f;
                return;
            }

            var g = inputAtmos.GasMixture.HeatCapacityRatio();

            AtmosphereHelper.MoveVolume(inputAtmos, outputAtmos, volume, matterStateToMove);

            float inputPf = inputAtmos.PressureGassesAndLiquidsInPa;
            float inputnf = inputAtmos.TotalMoles;
            float outputPf = outputAtmos.PressureGassesAndLiquidsInPa;
            var Cv = inputAtmos.GasMixture.HeatCapacity / inputAtmos.TotalMoles;

            double work;
            if (inputPf > outputPf)
            {
                work = 0f;
            }
            else if (inputP0 > outputP0 &&
                     inputPf < outputPf)
            {
                var equiPressure = Helpers.Mix(inputAtmos, outputAtmos, matterStateToMove);

                work = Helpers.getCompressiveWork(
                    equiPressure.PressureGassesAndLiquidsInPa,
                    equiPressure.TotalMoles,
                    equiPressure.Temperature, inputnf, outputPf, g, Cv, pumpVol);
            }
            else
            {
                work = Helpers.getCompressiveWork(inputP0, inputn0, inputT0, inputnf, outputPf, g, Cv,
                    pumpVol);
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
            else
            {
                device.UsedPower = 10f;
            }
        }
    }

    [HarmonyPatch(typeof(VolumePump))]
    public class VolumePumpGetUsedPowerPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            var candidateMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(DeviceAtmospherics)))
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                    BindingFlags.NonPublic));

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
        public static void PostfixGetUsedPower(ref float __result, VolumePump __instance)
        {
            if (!__result.IsDenormalOrZero())
            {
                __result = 10f + __instance.UsedPower;
            }
        }
    }

    [HarmonyPatch(typeof(ActiveVent))]
    public class ActiveVentPatch
    {
        private static float MoveGas(ActiveVent __instance,
            Atmosphere inputAtmo,
            Atmosphere outputAtmo,
            float totalTemperature)
        {
            double inputP0 = inputAtmo.PressureGassesAndLiquidsInPa;
            double inputn0 = inputAtmo.TotalMoles;
            double inputT0 = inputAtmo.Temperature;
            double outputP0 = outputAtmo.PressureGassesAndLiquidsInPa;
            double outputn0 = Math.Max(outputAtmo.TotalMoles, .0000001);
            var g = outputAtmo.GasMixture.HeatCapacityRatio();
            var Cv = inputAtmo.GasMixture.HeatCapacity / inputAtmo.TotalMoles;

            var pressureDiff = (inputP0 - outputP0) / 1000;
            double freeFlow;
            if (pressureDiff > 0)
            {
                var adjustedDiff = __instance.PressurePerTick - pressureDiff / (pressureDiff * pressureDiff);
                freeFlow = (adjustedDiff * inputAtmo.Volume) / (8.314399719238281 * Math.Max(inputT0, 0.0001f));
            }
            else
            {
                freeFlow = 0;
            }

            double transferMoles = Math.Abs(Helpers.getMolesMovedByWork(inputP0, inputT0, inputP0 + pressureDiff, g, Cv,
                __instance.UsedPower, .5f));
            ;
            // if (pressureDiff > 0)
            //     
            // else
            //     transferMoles = Math.Abs(Helpers.getMolesMovedByWork(inputP0, inputT0, inputP0+pressureDiff, g, Cv, __instance.UsedPower, .5f));
            transferMoles += freeFlow;
            float pressureGasses = outputAtmo.PressureGasses;
            outputAtmo.Add(inputAtmo.Remove((float)transferMoles, AtmosphereHelper.MatterState.All));
            outputAtmo.GasMixture.AddEnergy(__instance.UsedPower);
            return outputAtmo.PressureGasses - pressureGasses;

            // if (inputP0 > outputP0)
            // {
            //     // var pressureDiff = inputP0 - outputP0;
            //     
            //     
            //     float b = (float) (( inputP0 - __instance.InternalPressure)
            //         *  inputAtmo.Volume 
            //         / (8.314399719238281 
            //            *  inputT0));
            //     float transferMoles = Mathf.Min(
            //         (float) (Mathf.Clamp(
            //             Mathf.Min(
            //                 __instance.PressurePerTick,
            //                 __instance.ExternalPressure - (float) outputP0
            //                 ), 
            //             0.0f, 
            //             __instance.PressurePerTick) 
            //             * (double) outputAtmo.Volume / (8.314399719238281 * (double) totalTemperature)
            //             ),
            //         b);
            //     float pressureGasses = outputAtmo.PressureGasses;
            //     Debug.Log($"free {transferMoles} {b}");
            //     outputAtmo.Add(inputAtmo.Remove(transferMoles, AtmosphereHelper.MatterState.All));
            //     return outputAtmo.PressureGasses - pressureGasses;
            // }
            // else
            // {
            //     float transferMoles = Helpers.getMolesMovedByWork(inputP0, inputT0, outputP0, g, Cv, __instance.UsedPower, .5f);
            //     float pressureGasses = outputAtmo.PressureGasses;
            //     outputAtmo.Add(inputAtmo.Remove(transferMoles, AtmosphereHelper.MatterState.All));
            //     outputAtmo.GasMixture.AddEnergy(__instance.UsedPower);
            //     float b = (float) (((double) inputP0 - (double) __instance.InternalPressure) * (double) inputAtmo.Volume / (8.314399719238281 * (double) inputT0));
            //     float transferMolesAlt = Mathf.Min((float) ((double) Mathf.Clamp(Mathf.Min(__instance.PressurePerTick, __instance.ExternalPressure - outputP0), 0.0f, __instance.PressurePerTick) * (double) outputAtmo.Volume / (8.314399719238281 * (double) totalTemperature)), b);
            //     Debug.Log($"work {transferMoles} / {transferMolesAlt}");
            //     return outputAtmo.PressureGasses - pressureGasses;
            // }
        }

        [HarmonyPrefix]
        [HarmonyPatch("PumpGasToPipe")]
        public static bool PrefixPumpToPipe(ref float __result, ActiveVent __instance,
            Atmosphere worldAtmosphere,
            Atmosphere pipeAtmosphere,
            float totalTemperature)
        {
            Debug.Log("PUmpToPipe");
            __result = MoveGas(__instance, worldAtmosphere, pipeAtmosphere, totalTemperature);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PumpGasToWorld")]
        public static bool PrefixPumpToWorld(ref float __result, ActiveVent __instance,
            Atmosphere worldAtmosphere,
            Atmosphere pipeAtmosphere,
            float totalTemperature)
        {
            Debug.Log("PumpToWorld");
            __result = MoveGas(__instance, pipeAtmosphere, worldAtmosphere, totalTemperature);
            return false;
        }
    }

    // [HarmonyPatch(typeof(AtmosphericsManager))]
    // public class AtmosphericsManagerAddPatch
    // {
    //     [HarmonyPostfix]
    //     [HarmonyPatch("DisplayBasicAtmosphere")]
    //     [HarmonyPatch(new Type[] { typeof(Atmosphere), typeof(StringBuilder), typeof(Pipe.ContentType) })]
    //     public static void PostfixAddMix(ref StringBuilder stringBuilder, Atmosphere atmosphere)
    //     {
    //         StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
    //             atmosphere.GasMixture.TotalEnergy.ToString());
    //         StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
    //             (atmosphere.GasMixture.TotalEnergy / atmosphere.GasMixture.TotalMolesGassesAndLiquids).ToString());
    //         StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
    //             atmosphere.GasMixture.HeatCapacity.ToString());
    //         StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
    //             atmosphere.GasMixture.TotalMolesGassesAndLiquids.ToString());
    // }
    // }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Localization2;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using HarmonyLib;
using UnityEngine;

namespace StationeersAdiabatics
{
    [HarmonyPatch]
    public class AtmosphereHelperPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
                .SelectMany(type => type.GetMethods())
                .Where(method => method.DeclaringType.Name.Equals("AtmosphereHelper") && method.Name.Equals("Mix"))
                .Cast<MethodBase>();
        }
    
        [HarmonyPatch]
        [HarmonyPrefix]
        static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere inputAtmos,
            Atmosphere outputAtmos, out float[] __state)
        {
            __state = new float[]
            {
                inputAtmos.PressureGassesAndLiquidsInPa, inputAtmos.TotalMoles,
                outputAtmos.PressureGassesAndLiquidsInPa, outputAtmos.TotalMoles
            };
        }
    
        [HarmonyPatch]
        [HarmonyPostfix]
        static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere inputAtmos, Atmosphere outputAtmos, float[] __state)
        {
            var work = Math.Abs(inputAtmos.GasMixture.TotalMolesGasses - __state[1]) *
                8.3144f *
                inputAtmos.Temperature *
                (inputAtmos.PressureGassesAndLiquidsInPa - __state[0]) / inputAtmos.PressureGassesAndLiquidsInPa;
            Debug.Log($"Mix {work} = {inputAtmos.GasMixture.TotalMolesGasses} * 8.3 * {inputAtmos.Temperature} * ({inputAtmos.PressureGassesAndLiquidsInPa} - {__state}) / {inputAtmos.PressureGassesAndLiquidsInPa}");
            Debug.Log($"Postfix {inputAtmos.PressureGasses} {inputAtmos.PressureGassesAndLiquidsInPa} {__state}");
            if (work > 0)
            {
                inputAtmos.GasMixture.AddEnergy(work);
                outputAtmos.GasMixture.RemoveEnergy(work);
            }
            else
            {
                inputAtmos.GasMixture.RemoveEnergy(work);
                outputAtmos.GasMixture.AddEnergy(work);
            }
        }
    }
    
    [HarmonyPatch]
    public class AtmosphereAddPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
                .SelectMany(type => type.GetMethods())
                .Where(method => method.DeclaringType.Name.Equals("Atmosphere") && method.Name.Equals("Add"))
                .Cast<MethodBase>();
        }
    
        [HarmonyPatch]
        [HarmonyPrefix]
        static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, out float[] __state)
        {
            __state = new float[] { __instance.PressureGassesAndLiquidsInPa, __instance.TotalMoles };
        }
    
        [HarmonyPatch]
        [HarmonyPostfix]
        static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, float[] __state)
        {
            var work = Math.Abs(__instance.GasMixture.TotalMolesGasses - __state[1]) *
                8.3144f *
                __instance.Temperature *
                (__instance.PressureGassesAndLiquidsInPa - __state[0]) / __instance.PressureGassesAndLiquidsInPa;
            Debug.Log($"Add {work} = {__instance.GasMixture.TotalMolesGasses} * 8.3 * {__instance.Temperature} * ({__instance.PressureGassesAndLiquidsInPa} - {__state}) / {__instance.PressureGassesAndLiquidsInPa}");
            Debug.Log($"Postfix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa} {__state}");
            if (work > 0)
            {
                __instance.GasMixture.AddEnergy(work);
            }
            else
            {
                __instance.GasMixture.RemoveEnergy(work);
            }
        }
    }
    
    [HarmonyPatch]
    public class AtmosphereRemovePatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(Atmosphere)))
                .SelectMany(type => type.GetMethods())
                .Where(method => method.DeclaringType.Name.Equals("Atmosphere") && method.Name.Equals("Remove"))
                .Cast<MethodBase>();
        }
    
        [HarmonyPatch]
        [HarmonyPrefix]
        static void Prefix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, out float[] __state)
        {
            Debug.Log($"Prefix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa}");
            __state = new float[] { __instance.PressureGassesAndLiquidsInPa, __instance.TotalMoles };
        }
        
        [HarmonyPatch]
        [HarmonyPostfix]
        static void Postfix(object[] __args, MethodBase __originalMethod, Atmosphere __instance, float[] __state)
        {
            var work = Math.Abs(__instance.GasMixture.TotalMolesGasses - __state[1]) *
                8.3144f *
                __instance.Temperature *
                (__instance.PressureGassesAndLiquidsInPa - __state[0]) / __instance.PressureGassesAndLiquidsInPa;
            Debug.Log($"Remove {work} = {__instance.GasMixture.TotalMolesGasses} * 8.3 * {__instance.Temperature} * ({__instance.PressureGassesAndLiquidsInPa} - {__state}) / {__instance.PressureGassesAndLiquidsInPa}");
            Debug.Log($"Postfix {__instance.PressureGasses} {__instance.PressureGassesAndLiquidsInPa} {__state}");
            if (work > 0)
            {
                __instance.GasMixture.AddEnergy(work);
            }
            else
            {
                __instance.GasMixture.RemoveEnergy(work);
            }
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
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer, atmosphere.GasMixture.TotalEnergy.ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer,
                (atmosphere.GasMixture.TotalEnergy / atmosphere.GasMixture.TotalMolesGassesAndLiquids).ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer, atmosphere.GasMixture.HeatCapacity.ToString());
            StringManager.DisplayKeyValue(stringBuilder, GameStrings.HeatExchangerEnergyTransfer, atmosphere.GasMixture.TotalMolesGassesAndLiquids.ToString());
        }
    }
}
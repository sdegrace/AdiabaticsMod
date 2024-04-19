using System;
using Assets.Scripts.Atmospherics;
using UnityEngine;

namespace StationeersAdiabatics
{
    public static class Helpers
    {
        public static double getCompressiveWork(
            float inputP0,
            float inputn0,
            float inputT0,
            float inputnf,
            float outputPf,
            
            float g,
            float Cv,
            
            float pumpInternalVolume)
        {
            var movedMoles = inputn0 - inputnf;
            if (movedMoles == 0)
                return 0;
            var ratio = Math.Pow(inputP0 / outputPf, g);
            Debug.Log($"Ration {ratio} pressure {inputP0} / {outputPf}");
            return (Cv * outputPf * inputT0 * movedMoles * ratio) / inputP0 +
                outputPf * pumpInternalVolume * ratio - inputT0;
        }

        public static double getCompressiveWorkByMoles(
            double inputP0,
            double movedMoles,
            double inputT0,
            double outputPf,

            double g,
            double Cv,

            double pumpInternalVolume)
        {
            if (movedMoles == 0)
                return 0;
            var ratio = Math.Pow(inputP0 / outputPf, g);
            return (Cv * outputPf * inputT0 * movedMoles * ratio) / inputP0 +
                outputPf * pumpInternalVolume * ratio - inputT0;
        }
        
        public static float getMolesMovedByWork(
            double inputP0,
            double inputT0,
            double outputP0,
            
            double g,
            double Cv,
            
            double work,
            
            double pumpInternalVolume)
        {
            Debug.Log($"Helper {outputP0} {inputT0}");
            outputP0 = Math.Max(outputP0, .000001f);
            inputT0 = Math.Max(inputT0, .000001f);
            var n1 = Math.Pow(inputP0 / outputP0, g);
            Debug.Log($"Helper {outputP0} {inputT0} {n1}  {Cv} {Cv * outputP0 * inputT0 * n1}");
            return (float)(inputP0 * (-outputP0 * pumpInternalVolume * n1 + inputT0 + work) /
                           (Cv * outputP0 * inputT0 * n1));
        }
        
        public static Atmosphere Mix(Atmosphere inputAtmos, Atmosphere outputAtmos, AtmosphereHelper.MatterState matterState)
        {
            Atmosphere ret = new Atmosphere();
            ret.Volume = inputAtmos.Volume + outputAtmos.Volume;
            float num1 = 0.0f;
            ret.GasMixture.Add(inputAtmos.GasMixture, matterState);
            float num2 = num1 + inputAtmos.GetVolume(matterState);
            ret.GasMixture.Add(outputAtmos.GasMixture, matterState);
            return ret;
        }
    }
}
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts;
using UnityEngine;

namespace StationeersAdiabatics
{
    #region BepInEx
    [BepInEx.BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class StationeersAdiabatics : BepInEx.BaseUnityPlugin
    {
        public const string pluginGuid = "net.Tanngrisnir.stationeers.StationeersAdiabatics";
        public const string pluginName = "StationeersAdiabatics";
        public const string pluginVersion = "0.0";
        public static void Log(string line)
        {
            Debug.Log("[" + pluginName + "]: " + line);
        }
        void Awake()
        {
            try
            {
                ConsoleWindow.Print("Adiabatics", ConsoleColor.Red);
                var harmony = new Harmony(pluginGuid);
                harmony.PatchAll();
                ConsoleWindow.Print("Adiabatics", ConsoleColor.Red);
                Log("Patch succeeded");
            }
            catch (Exception e)
            {
                Log("Patch Failed");
                Log(e.ToString());
            }
        }
    }
    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using UnityModManagerNet;

namespace MicroPatches.Patches
{
    [MicroPatch("Fix UMM update check", Optional = true)]
    [HarmonyPatch]
    //[HarmonyPatchCategory(MicroPatch.Category.Optional)]
    internal static class FixUMMUpdateCheck
    {
        static bool CheckNetworkConnection()
        {
            try
            {
                using var sp = new Ping();

                var addresses = Dns.GetHostAddresses("www.google.com");
                var reply = sp.Send(addresses.First(ip => ip.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork), 3000);

                if (reply.Status is IPStatus.Success)
                    return true;

                Main.PatchLog(nameof(FixUMMUpdateCheck), $"Checking for network failed with status {reply.Status}");
            }
            catch (Exception e)
            {
                Main.PatchLogException(e);
            }

            return false;
        }

        [HarmonyPatch(typeof(UnityModManager), nameof(UnityModManager.HasNetworkConnection))]
        [HarmonyPostfix]
        static bool HasNetworkConnection_Postfix(bool __result)
        {
            if (__result)
                return true;

            return CheckNetworkConnection();
        }

        static bool triedCheckUpdates;

        [HarmonyPatch(typeof(UnityModManager.UI), nameof(UnityModManager.UI.ToggleWindow), [typeof(bool)])]
        [HarmonyPrefix]
        static void ToggleWindow_Prefix(bool open)
        {
            if (!open || triedCheckUpdates)
                return;

            try
            {
                AccessTools.Method(typeof(UnityModManager), "CheckModUpdates")?.Invoke(null, []);

                triedCheckUpdates = true;
            }
            catch (Exception e)
            {
                Main.PatchLogException(e);
            }
        }
    }
}

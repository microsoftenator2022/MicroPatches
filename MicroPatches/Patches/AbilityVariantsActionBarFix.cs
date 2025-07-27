using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using Kingmaker.BundlesLoading;
using Kingmaker.Code.UI.MVVM.View.ActionBar;
using Kingmaker.Code.UI.MVVM.View.ActionBar.PC;

using UnityEngine;
using UnityEngine.UI;

namespace MicroPatches.Patches;

[MicroPatch("Fix missing Ability Variants action bar", Optional = false)]
[HarmonyPatch]
internal static class AbilityVariantsActionBarFix
{
    static readonly string[] SpriteResourceNames = ["MicroPatches.Monitor_ArrowGreenUP.png", "MicroPatches.Monitor_ArrowGreen.png"];

    // Try to minimize lag when creating slots for Ability Variants
    [HarmonyPrepare]
    static void PreloadSprites() => _ = Sprites.Value;

    internal static readonly Lazy<Sprite[]> Sprites = new(() =>
    {
        var sprites = new List<Sprite>();

        foreach (var n in SpriteResourceNames)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(n);
            using var reader = new BinaryReader(stream);
            var texture = new Texture2D(0, 0, TextureFormat.RGBA32, false);
            texture.LoadImage(reader.ReadBytes((int)stream.Length));
            texture.Apply();
            var sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(0.5f, 0.5f));
            
            // Maybe not necessary? Need to test
            //UnityEngine.Object.DontDestroyOnLoad(sprite);

            sprites.Add(sprite);
        }
        
        return sprites.ToArray();
    });

    [HarmonyPatch(typeof(ActionBarSlotPCView), nameof(ActionBarSlotPCView.BindViewImplementation))]
    [HarmonyPostfix]
    static void ActionBarSlotPCView_BindViewImplementation_Postfix(ActionBarSlotPCView __instance)
    {
        if (__instance.m_ConvertedView == null)
            return;

        if (__instance.m_ConvertedView.m_SlotView != null)
            return;

        __instance.m_ConvertedView.m_SlotView =
            UnityEngine.Object.Instantiate(__instance.GetComponentInParent<ActionBarBaseSlotView>());

        if (__instance.m_ConvertedView.m_SlotView == null)
            __instance.m_ConvertedView.m_SlotView =
                UnityEngine.Object.Instantiate(UnityEngine.Object.FindAnyObjectByType<ActionBarBaseSlotView>());

        var convertUp = __instance.m_ConvertButton.transform.Find("ConvertUp");
        Image? convertUpImage = null;
        if (convertUp != null) convertUpImage = convertUp.GetComponent<Image>();

        var convertDown = __instance.m_ConvertButton.transform.Find("ConvertDown");
        Image? convertDownImage = null;
        if (convertDown != null) convertDownImage = convertDown.GetComponent<Image>();

        if (convertUpImage == null || convertDownImage == null)
            return;

        convertUpImage.sprite = Sprites.Value[0];
        convertDownImage.sprite = Sprites.Value[1];

        AddOutline(convertUpImage.gameObject);
        AddOutline(convertDownImage.gameObject);
    }

    static Outline AddOutline(GameObject gameObject)
    {
        var outline = gameObject.AddComponent<Outline>();
        outline.useGraphicAlpha = true;
        outline.effectColor = new(0f, 0.1f, 0f, 0.5f);
        outline.effectDistance = new(1f, 1f);

        return outline;
    }
}

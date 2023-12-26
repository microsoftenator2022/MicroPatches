using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Code.UI.MVVM.VM.Tooltip.Templates;
using Kingmaker.Code.UI.MVVM.VM.Tooltip.Utils;

using MicroPatches.Patches;
using MicroPatches.UGUI;

using Owlcat.Runtime.UI.Tooltips;
using Owlcat.Runtime.UI.Controls.Selectable;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using UniRx;
using Kingmaker.UI;

namespace MicroPatches
{
    internal partial class Main
    {
        static GameObject? UIWindow;
        static GameObject? UICanvas;

        static void CreateUI()
        {
            if (UICanvas == null)
            { 
                var canvasObject = new GameObject("MicroPatches GUI canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new(1280, 720);
                scaler.matchWidthOrHeight = 1f;

                UnityEngine.Object.DontDestroyOnLoad(canvasObject);

                UICanvas = canvasObject;
            }

            var obj = new GameObject("MicroPatches GUI", typeof(RectTransform), typeof(MicroPatchUGUIBehaviour));

            var t = (obj.transform as RectTransform)!;

            t.anchorMin = new(1, 1);
            t.anchorMax = new(1, 1);
            t.pivot = new(1, 1);

            t.anchoredPosition = new(-100, -100);

            obj.transform.SetParent(UICanvas.transform, false);

            UIWindow = obj;

            obj.AddComponent<CanvasGroup>().blocksRaycasts = true;
        }

        [MicroPatch("", Hidden = true)]
        [HarmonyPatch(typeof(GameStarter), nameof(GameStarter.FixTMPAssets))]
        static class FixTMP
        {
            static void Prefix()
            {
                if (UIWindow == null)
                    return;

                Main.PatchLog(nameof(FixTMP), "Fixing TMP");

                var defaultFont = BlueprintRoot.Instance.UIConfig.DefaultTMPFontAsset;

                foreach (var tmp in UIWindow.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    Main.PatchLog(nameof(FixTMP), $"Fixing {tmp.font} -> {defaultFont}");

                    tmp.font = defaultFont;

                    if (tmp.fontSharedMaterial != null)
                        tmp.fontSharedMaterial = defaultFont.material;

                    if (tmp.spriteAsset != null)
                        tmp.spriteAsset = BlueprintRoot.Instance.UIConfig.DefaultTMPSriteAsset;
                }
            }
        }
    }
}

namespace MicroPatches.UGUI
{
    internal class MicroPatchUGUIBehaviour : MonoBehaviour, IDisposable
    {
        readonly List<IDisposable> Subscriptions = [];

        Material GetUIMaterial() => UnityEngine.Object.Instantiate(Image.defaultGraphicMaterial);

        static class Colors
        {
            public static class MaterialColors
            {
                public static readonly Color Medium = new(0.5f, 0.5f, 0.5f);
                public static readonly Color Dark = new(0.15f, 0.15f, 0.15f);
            }

            public static readonly Color Background = new Color32(0x02, 0x08, 0x04, 0xE8);
            public static readonly Color Header = new Color32(0x1A, 0xFF, 0x45, 0xFF);
            public static readonly Color Text = new Color32(0xCD, 0xFF, 0xE5, 0xFF);
            public static readonly Color RedText = new Color32(0xFF, 0x72, 0x6E, 0xFF);
        }

        UIElement CreateHeader(UIElement parent)
        {
            var titleLayout = parent.AddUIObject("title");
            titleLayout.Element.minHeight = 20;
            titleLayout.Element.flexibleWidth = 1;

            var titleBackground = titleLayout.gameObject.AddComponent<Image>();
            titleBackground.material = GetUIMaterial();
            titleBackground.material.color = Colors.MaterialColors.Medium;
            titleBackground.color = Colors.Header;

            var titleText = titleLayout.AddTextObject("MicroPatches", Colors.Text);
            titleText.Layout.Element.minHeight = 20;

            var titleTmp = titleText.Element;
            titleTmp.fontSizeMin = 10;
            titleTmp.fontSizeMax = 16;
            titleTmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            titleTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            titleTmp.enableAutoSizing = true;

            var textFitter = titleText.AddUIElement<ContentSizeFitter>();
            textFitter.Element.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            textFitter.Element.verticalFit = ContentSizeFitter.FitMode.MinSize;

            var closeButton = titleLayout.AddUIObject<Button>("close window");
            closeButton.RectTransform.sizeDelta = new(20, 20);
            Utility.SetAnchors(closeButton.RectTransform, AnchorLocation.TopRight, AnchorType.Fixed);

            var closeButtonImage = closeButton.AddUIElement<Image>();
            closeButtonImage.Element.material = GetUIMaterial();
            closeButtonImage.Element.material.color = Colors.MaterialColors.Medium;
            closeButtonImage.Element.color = new(Colors.Header.g, Colors.Header.b, Colors.Header.r);

            closeButton.Element.image = closeButtonImage.Element;

            Subscriptions.Add(closeButton.Element.onClick.AsObservable().Subscribe(_ => UnityEngine.GameObject.Destroy(gameObject)));

            return titleLayout;
        }

        void Awake()
        {
            var rt = gameObject.transform as RectTransform;

            if (rt == null)
                return;

            Utility.SetAnchors(rt, AnchorLocation.Center, AnchorType.Fill);
            
            var csf = gameObject.AddUIElement<ContentSizeFitter>();

            csf.Layout.Element.minWidth = 200;
            csf.Layout.Element.minHeight = 200;
            csf.Element.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.Element.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vGroup = gameObject.AddUIElement<VerticalLayoutGroup>();
            vGroup.Element.childControlWidth = true;
            vGroup.Element.childControlHeight = true;
            vGroup.Element.childForceExpandWidth = true;
            vGroup.Element.childForceExpandHeight = false;

            CreateHeader(vGroup);

            var content = vGroup.AddUIObject();
            
            var cLayout = content.AddUIElement<VerticalLayoutGroup>();
            cLayout.Element.padding = new(10, 10, 10, 10);

            cLayout.Element.childControlWidth = true;
            cLayout.Element.childControlHeight = true;
            cLayout.Element.childForceExpandWidth = true;
            cLayout.Element.childForceExpandWidth = false;

            var windowBackground = content.gameObject.AddComponent<Image>();
            windowBackground.material = GetUIMaterial();
            windowBackground.material.color = Colors.MaterialColors.Dark;
            windowBackground.color = Colors.Background;

            var patchesList = content.AddUIObject<VerticalLayoutGroup>();
            patchesList.Element.childControlWidth = true;
            patchesList.Element.childControlHeight = true;
            patchesList.Element.childForceExpandWidth = true;
            patchesList.Element.childForceExpandHeight = false;

            var patchLines = PatchesSection(patchesList).ToArray();

            var footer = cLayout.AddUIObject();
        }

        IEnumerable<UIElement> PatchesSection(UIElement parent)
        {
            var hidden = Main.Patches.Where(p => p.IsHidden);
            var patches = Main.Patches.Where(p => !p.IsHidden);
            var experimental = patches.Where(p => p.IsExperimental);
            var optional = patches.Where(p => p.IsOptional && !p.IsExperimental);
            var forced = patches.Where(p => !p.IsExperimental && !p.IsOptional);
            
            IEnumerable<UIElement> tryGetPatchLine(UIElement parent, MicroPatch patch)
            {
                var maybeLine = PatchLine(parent, patch);

                if (maybeLine == null)
                    return [];

                return [maybeLine];
            }

            foreach (var line in forced.SelectMany(p => tryGetPatchLine(parent, p)))
            {
                yield return line;
            }

            foreach (var line in optional.SelectMany(p => tryGetPatchLine(parent, p)))
            {
                yield return line;
            }

            var experimentalHeader = parent.AddTextObject("Experimental", Colors.Text, TextAlignmentOptions.Center);
            experimentalHeader.Layout.Element.minHeight = 24;
            experimentalHeader.Element.fontStyle = FontStyles.Bold;
            experimentalHeader.Element.fontSize = 14;
            experimentalHeader.Element.fontSizeMin = 12;
            experimentalHeader.Element.fontSizeMax = 16;
            experimentalHeader.Element.enableAutoSizing = true;

            foreach (var line in experimental.SelectMany(p => tryGetPatchLine(parent, p)))
            {
                yield return line;
            }

            var hiddenFailed = hidden.Where(p => p.Failed());

            if (!hiddenFailed.Any() && !Main.IsDebug)
                yield break;

            var debugHeader = parent.AddTextObject("Debug", Colors.Text, TextAlignmentOptions.Center);
            debugHeader.Layout.Element.minHeight = 24;
            debugHeader.Element.fontStyle = FontStyles.Bold;
            debugHeader.Element.fontSize = 14;
            debugHeader.Element.fontSizeMin = 12;
            debugHeader.Element.fontSizeMax = 16;
            debugHeader.Element.enableAutoSizing = true;

#pragma warning disable CS0162 // Unreachable code detected
            if (!Main.IsDebug)
                hidden = hiddenFailed;
#pragma warning restore CS0162 // Unreachable code detected

            foreach (var line in hidden.SelectMany(p => tryGetPatchLine(parent, p)))
            {
                yield return line;
            }
        }

        public UIElement? PatchLine(UIElement parent, MicroPatch patch)
        {
            var displayName = string.IsNullOrEmpty(patch.DisplayName) ? patch.PatchType.Name : patch.DisplayName;

#if DEBUG
                Main.PatchLog("UGUI", $"Creating UI for {patch.PatchType.Name} Category = {patch.Patch.GetCategory() ?? "NULL"}");
#endif


            if (patch.IsHidden && patch.IsApplied())
            {
                Main.PatchLog("UGUI", $"{patch.PatchType.Name} is hidden");
#if !DEBUG
                return null;
#endif
            }

            var line = parent.AddUIObject<HorizontalLayoutGroup>(displayName);
            line.Layout.Element.minHeight = 20;

            line.Element.childControlWidth = true;
            line.Element.childControlHeight = true;
            line.Element.childForceExpandWidth = false;
            line.Element.childForceExpandHeight = false;
            line.Element.childAlignment = TextAnchor.MiddleCenter;

            var toggle = line.AddUIObject<Toggle>($"{displayName} toggle");
            toggle.Layout.Element.preferredWidth = 20;
            toggle.Layout.Element.preferredHeight = 20;
            toggle.Element.interactable = patch.IsOptional;
            toggle.Element.isOn = !patch.IsOptional || patch.IsEnabled();

            var t1 = toggle.AddUIObject<Image>();
            Utility.SetAnchors(t1.RectTransform, AnchorLocation.Center, AnchorType.Fixed);
            t1.RectTransform.sizeDelta = new(11, 11);
            t1.Element.material = GetUIMaterial();
            t1.Element.material.color = Colors.MaterialColors.Dark;
            t1.Element.color = Colors.Text;

            var t2 = toggle.AddUIObject<Image>();
            Utility.SetAnchors(t2.RectTransform, AnchorLocation.Center, AnchorType.Fixed);
            t2.RectTransform.sizeDelta = new(5, 5);
            t2.Element.material = GetUIMaterial();
            if (!toggle.Element.interactable)
                t2.Element.material.color = Colors.MaterialColors.Medium;
            t2.Element.color = Colors.Text;

            toggle.Element.targetGraphic = t1.Element;
            toggle.Element.graphic = t2.Element;

            Subscriptions.Add(toggle.Element.onValueChanged.AsObservable().Subscribe(enabled => Main.Instance.SetPatchEnabled(patch.PatchType.Name, enabled)));

            var nameText = line.AddTextObject(displayName, Colors.Text);
            nameText.Element.fontSize = 12;
            nameText.Element.alignment = TextAlignmentOptions.BottomLeft;
            nameText.Element.margin = new(2, 0, 10, 0);
            
            if (!patch.IsEnabled() && !patch.Failed())
                nameText.Element.color *= 0.5f;

            nameText.Layout.Element.flexibleWidth = 1;

            if (!string.IsNullOrEmpty(patch.Description))
            {
                nameText.Element.raycastTarget = true;

                Subscriptions.Add(nameText.gameObject.AddComponent<OwlcatSelectable>().SetTooltip(new TooltipTemplateHint(patch.Description)));
            }

            var statusText = line.AddTextObject("Disabled", nameText.Element.color);
            statusText.Element.fontSize = 12;
            statusText.Element.alignment = TextAlignmentOptions.Bottom;
            
            statusText.Layout.Element.preferredWidth = statusText.Element.preferredWidth;

            statusText.Element.text = patch switch
                {
                    _ when patch.IsApplied() => "OK",
                    _ when patch.IsEnabled() => "Failed",
                    _ => "Disabled"
                };

            if (patch.Failed())
                statusText.Element.color = Colors.RedText;

            return line;
        }

        public void Dispose()
        {
            foreach (var s in Subscriptions)
                s.Dispose();
        }

        void OnDestroy()
        {
            this.Dispose();
        }
    }
}

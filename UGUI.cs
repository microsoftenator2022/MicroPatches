using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;
using Kingmaker.Blueprints.Root;
using Kingmaker;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MicroPatches.UGUI;
using System.Reflection;

namespace MicroPatches
{
    internal partial class Main
    {
        static GameObject? UIWindow;
        static GameObject? UICanvas;

        static void CreateUI()
        {
            var canvasObject = new GameObject("MicroPatches GUI canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasObject.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

            UnityEngine.Object.DontDestroyOnLoad(canvasObject);

            UICanvas = canvasObject;

            var obj = new GameObject("MicroPatches GUI", typeof(RectTransform), typeof(MicroPatchUGUIBehaviour));

            var t = (obj.transform as RectTransform)!;

            t.anchorMin = new(0.5f, 0.5f);
            t.anchorMax = new(0.5f, 0.5f);
            t.pivot = new(0.5f, 0.5f);

            obj.transform.SetParent(UICanvas.transform, false);

            UIWindow = obj;
        }

        [HarmonyPatch(typeof(GameStarter), nameof(GameStarter.FixTMPAssets))]
        [HarmonyPatchCategory(Main.Category.Hidden)]
        static class FixTMP
        {
            static void Prefix()
            {
                if (UIWindow == null)
                    return;

                Main.PatchLog(nameof(FixTMP), "Fixing TMP");

                var defaultFont = BlueprintRoot.Instance.UIConfig.DefaultTMPFontAsset;

                foreach (var tmp in Main.UIWindow.GetComponentsInChildren<TextMeshProUGUI>())
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
    public enum AnchorLocation
    {
        BottomLeft,
        BottomCenter,
        BottomRight,
        CenterLeft,
        Center,
        CenterRight,
        TopLeft,
        TopCenter,
        TopRight
    }

    public enum AnchorType
    {
        Fixed,
        FillUp,
        FillVertical,
        FillDown,
        FillLeft,
        FillHorizontal,
        FillRight,
        Fill
    }

    static class Utility
    {
        static Color LinearToSRGB(Color c) => new(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), c.a);
        static Color SRGBToLinear(Color c) => new(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b), c.a);

        public static Vector2 ToPosition(this AnchorLocation anchorLocation) => Utility.AnchorToPosition(anchorLocation);

        public static Vector2 AnchorToPosition(AnchorLocation anchorLocation) =>
            anchorLocation switch
            {
                AnchorLocation.BottomLeft => new(0, 0),
                AnchorLocation.CenterLeft => new(0, 0.5f),
                AnchorLocation.TopLeft => new(0, 1),
                AnchorLocation.BottomCenter => new(0.5f, 0),
                AnchorLocation.Center => new(0.5f, 0.5f),
                AnchorLocation.TopCenter => new(0.5f, 1),
                AnchorLocation.BottomRight => new(1, 0),
                AnchorLocation.CenterRight => new(1, 0.5f),
                AnchorLocation.TopRight => new(1, 1),
                _ => AnchorToPosition(AnchorLocation.BottomLeft)
            };

        public static void SetAnchors(RectTransform transform, AnchorLocation origin, AnchorType anchorType)
        {
            var o = origin.ToPosition();
            transform.pivot = o;
            transform.anchorMin = o;
            transform.anchorMax = o;

            switch (anchorType)
            {
                case AnchorType.Fixed:
                    break;

                case AnchorType.Fill:
                    transform.anchorMin = new(0, 0);
                    transform.anchorMax = new(1, 1);
                    break;

                case AnchorType.FillUp:
                    transform.anchorMax = new(o.x, 1);
                    break;

                case AnchorType.FillVertical:
                    transform.anchorMax = new(o.x, 1);
                    transform.anchorMin = new(o.x, 0);
                    break;

                case AnchorType.FillDown:
                    transform.anchorMin = new(o.x, 0);
                    transform.anchorMax = new(o.x, o.y);
                    break;

                case AnchorType.FillRight:
                    transform.anchorMax = new(1, o.y);
                    break;

                case AnchorType.FillHorizontal:
                    transform.anchorMax = new(1, o.y);
                    transform.anchorMin = new(0, o.y);
                    break;

                case AnchorType.FillLeft:
                    transform.anchorMin = new(0, o.y);
                    transform.anchorMax = new(o.x, o.y);
                    break;
            }
        }

        public static void AddChild(this GameObject parent, GameObject child) =>
            child.transform.parent = parent.transform;
    }

    abstract class UIElement
    {
        private readonly Type ElementType;
        internal UIElement(Type elementType) => ElementType = elementType;

        protected abstract UIBehaviour UIBehaviour { get; }

        public GameObject gameObject => UIBehaviour.gameObject;
        public RectTransform RectTransform => (gameObject.transform as RectTransform)!;
        public UIElement<LayoutElement> Layout => new(gameObject.GetComponent<LayoutElement>());

        public static UIElement<LayoutElement> NewUIObject(string? name = null)
        {
            var go = new GameObject(name ?? $"LayoutElement", typeof(RectTransform), typeof(LayoutElement));

            return new UIElement<LayoutElement>(go.GetComponent<LayoutElement>());
        }

        public static UIElement<TElement> NewUIObject<TElement>(out UIElement<LayoutElement> layout, string? name = null)
            where TElement : UIBehaviour => NewUIObject(name ?? $"{typeof(TElement).Name}").AddUIElement<TElement>(out layout);

        public UIElement<TChild> AddUIElement<TChild>(out UIElement<LayoutElement> layout) where TChild : UIBehaviour
        {
            if (this.gameObject.TryGetComponent<LayoutElement>(out var element))
                layout = new(element);
            else
                layout = new(gameObject.AddComponent<LayoutElement>());

            if (ElementType == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(this.gameObject.AddComponent<TChild>());
        }

        public UIElement<TChild> AddUIElement<TChild>() where TChild : UIBehaviour =>
            AddUIElement<TChild>(out var _);

        public UIElement<LayoutElement> AddUIObject(string? name = null) =>
            this.gameObject.AddUIObject(name);

        public UIElement<TChild> AddUIObject<TChild>(out UIElement<LayoutElement> layout, string? name = null)
            where TChild : UIBehaviour =>
            this.gameObject.AddUIObject<TChild>(out layout, name);

        public UIElement<TChild> AddUIObject<TChild>(string? name = null) where TChild : UIBehaviour =>
            this.AddUIObject<TChild>(out var _, name);
    }

    class UIElement<TElement> : UIElement where TElement : UIBehaviour
    {
        public readonly TElement Element;

        protected override UIBehaviour UIBehaviour => Element;

        public UIElement(TElement element) : base(typeof(TElement))
        {
            Element = element;
        }
    }

    static class UIElementExtensions
    {
        public static UIElement<LayoutElement> AddUIObject(this GameObject parent, string? name = null)
        {
            var element = UIElement.NewUIObject(name ?? $"{parent.name}.LayoutElement");

            var go = element.gameObject;

            var rt = (go.transform as RectTransform)!;

            var parentTransform = parent.transform as RectTransform;

            if (parentTransform != null)
            {
                rt.anchorMin = parentTransform.anchorMin;
                rt.anchorMax = parentTransform.anchorMax;
                rt.pivot = parentTransform.pivot;
            }

            rt.SetParent(parent.transform, false);

            return element;
        }

        public static UIElement<TElement> AddUIObject<TElement>(this GameObject parent, out UIElement<LayoutElement> initLayout, string? name = null)
            where TElement : UIBehaviour
        {
            initLayout = AddUIObject(parent, name);
            initLayout.gameObject.name = name ?? $"{parent.name}.{typeof(TElement).Name}";

            if (typeof(TElement) == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(initLayout.gameObject.AddComponent<TElement>());
        }

        public static UIElement<TElement> AddUIObject<TElement>(this GameObject parent, string? name = null)
            where TElement : UIBehaviour =>
            AddUIObject<TElement>(parent, out var _, name);

        public static UIElement<LayoutElement> AddUIElement(this GameObject obj)
        {

            if (obj.TryGetComponent<LayoutElement>(out var _))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(obj.AddComponent<LayoutElement>());
        }

        public static UIElement<TElement> AddUIElement<TElement>(this GameObject obj, out UIElement<LayoutElement> layout)
            where TElement : UIBehaviour
        {
            if (obj.TryGetComponent<LayoutElement>(out var element))
                layout = new(element);
            else
                layout = new(obj.AddComponent<LayoutElement>());


            if (typeof(TElement) == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);


            return new(obj.AddComponent<TElement>());
        }

        public static UIElement<TElement> AddUIElement<TElement>(this GameObject obj) where TElement : UIBehaviour =>
            obj.AddUIElement<TElement>(out var _);

        public static UIElement<TextMeshProUGUI> AddTextElement(
            this UIElement parent,
            string? text = null,
            Color? color = null,
            TextAlignmentOptions? alignment = null)
        {
            var ui = parent.gameObject.AddUIElement<TextMeshProUGUI>();

            if (text != null) ui.Element.text = text;
            if (color != null) ui.Element.color = color.Value;
            if (alignment != null) ui.Element.alignment = alignment.Value;

            return ui;
        }

        public static UIElement<TextMeshProUGUI> AddTextObject(
            this UIElement parent,
            out UIElement<LayoutElement> layout,
            string? text = null,
            Color? color = null,
            TextAlignmentOptions? alignment = null)
        {
            layout = parent.gameObject.AddUIObject();
            return layout.AddTextElement(text, color, alignment);
        }
    }

    internal class MicroPatchUGUIBehaviour : MonoBehaviour
    {
        Material GetUIMaterial() => UnityEngine.Object.Instantiate(Image.defaultGraphicMaterial);

        static class Colors
        {
            public static class MaterialColors
            {
                public static readonly Color Medium = new(0.4f, 0.4f, 0.4f);
                public static readonly Color Dark = new(0.15f, 0.15f, 0.15f);
            }

            public static readonly Color Background = new Color32(0x02, 0x08, 0x04, 0xE8);
            //public static readonly Color Header = new Color32(0x24, 0xEC, 0x5C, 0xFF);
            public static readonly Color Header = new Color32(0x1A, 0xFF, 0x45, 0xFF);
            public static readonly Color Text = new Color32(0xCD, 0xFF, 0xE5, 0xFF);
        }

        void Awake()
        {
            var rt = gameObject.transform as RectTransform;

            if (rt == null)
                return;

            Utility.SetAnchors(rt, AnchorLocation.Center, AnchorType.Fill);
            
            var csf = gameObject.AddUIElement<ContentSizeFitter>(out var layout);

            layout.Element.minWidth = 200;
            layout.Element.minHeight = 200;
            csf.Element.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.Element.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vGroup = gameObject.AddUIElement<VerticalLayoutGroup>();
            vGroup.Element.childControlWidth = true;
            vGroup.Element.childControlHeight = true;
            vGroup.Element.childForceExpandWidth = true;
            vGroup.Element.childForceExpandHeight = false;

            // Header
            {
                var titleLayout = vGroup.AddUIObject("title");
                titleLayout.Element.minHeight = 20;
                titleLayout.Element.flexibleWidth = 1;

                var titleBackground = titleLayout.gameObject.AddComponent<Image>();
                titleBackground.material = GetUIMaterial();
                titleBackground.material.color = Colors.MaterialColors.Medium;
                titleBackground.color = Colors.Header;

                var titleText = titleLayout.AddTextObject(out var textLayout, "MicroPatches", Colors.Text);
                textLayout.Element.minHeight = 20;

                var titleTmp = titleText.Element;
                //titleTmp.text = "Title";
                titleTmp.fontSizeMin = 10;
                titleTmp.fontSizeMax = 16;
                titleTmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                titleTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                //titleTmp.color = Colors.Text;
                titleTmp.enableAutoSizing = true;

                var textFitter = titleText.AddUIElement<ContentSizeFitter>();
                textFitter.Element.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                textFitter.Element.verticalFit = ContentSizeFitter.FitMode.MinSize;
            }

            var content = vGroup.AddUIObject();
            
            var cLayout = content.AddUIElement<VerticalLayoutGroup>();
            cLayout.Element.padding = new(10, 10, 10, 10);

            cLayout.Element.childControlWidth = true;
            cLayout.Element.childControlHeight = true;
            cLayout.Element.childForceExpandWidth = true;
            cLayout.Element.childForceExpandWidth = false;

            {
                var windowBackground = content.gameObject.AddComponent<Image>();
                windowBackground.material = GetUIMaterial();
                windowBackground.material.color = Colors.MaterialColors.Dark;
                windowBackground.color = Colors.Background;
            }

            var patchesList = content.AddUIObject<VerticalLayoutGroup>(out var _);
            patchesList.Element.childAlignment = TextAnchor.MiddleLeft;
            patchesList.Element.childControlWidth = true;
            patchesList.Element.childControlHeight = true;
            patchesList.Element.childForceExpandWidth = true;
            patchesList.Element.childForceExpandHeight = false;

            var patchLines = Patches(patchesList).ToArray();

            var footer = cLayout.AddUIObject();
        }

        IEnumerable<UIElement> Patches(UIElement parent)
        {
            foreach (var (t, pc) in Main.PatchClasses)
            {
                var name = t.Name;

                Main.Instance.AppliedPatches.TryGetValue(t, out var applied);

                if (t.GetCustomAttribute<MicroPatchAttribute>() is { } attr)
                    name = attr.Name;

                var enabled = Main.Instance.GetPatchEnabled(t.Name);

                var line = parent.AddUIObject<HorizontalLayoutGroup>(out var layout, name);
                layout.Element.minHeight = 20;

                line.Element.childControlWidth = true;
                line.Element.childControlHeight = true;
                line.Element.childForceExpandWidth = false;
                line.Element.childForceExpandHeight = false;

                var text1 = line.AddTextObject(out var layout1, (applied ?? false) ? "OK" : "KO", Colors.Text);
                layout1.Element.minWidth = 30;
                text1.Element.fontSize = 12;
                text1.Element.alignment = TextAlignmentOptions.MidlineLeft;
                
                var text2 = line.AddTextObject(out var layout2, name, Colors.Text);
                text2.Element.fontSize = 12;
                text2.Element.alignment = TextAlignmentOptions.MidlineLeft;

                yield return line;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

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

        public static GameObject AddChild(this GameObject parent, GameObject child)
        {
            if (child.transform is RectTransform)
                ((RectTransform)child.transform).SetParent(parent.transform, false);
            else
                child.transform.parent = parent.transform;

            return child;
        }
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

        public UIElement<TChild> AddUIElement<TChild>() where TChild : UIBehaviour
        {
            UIElement<LayoutElement> layout;

            if (this.gameObject.TryGetComponent<LayoutElement>(out var element))
                layout = new(element);
            else
                layout = new(gameObject.AddComponent<LayoutElement>());

            if (typeof(TChild) == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(this.gameObject.AddComponent<TChild>());
        }

        public UIElement<LayoutElement> AddUIObject(string? name = null) =>
            this.gameObject.AddUIObject(name);

        public UIElement<TChild> AddUIObject<TChild>(string? name = null)
            where TChild : UIBehaviour =>
            this.gameObject.AddUIObject<TChild>(name);
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

        public static UIElement<TElement> AddUIObject<TElement>(this GameObject parent, string? name = null)
            where TElement : UIBehaviour
        {
            var initLayout = AddUIObject(parent, name);
            initLayout.gameObject.name = name ?? $"{parent.name}.{typeof(TElement).Name}";

            if (typeof(TElement) == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(initLayout.gameObject.AddComponent<TElement>());
        }

        public static UIElement<LayoutElement> AddUIElement(this GameObject obj)
        {

            if (obj.TryGetComponent<LayoutElement>(out var _))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);

            return new(obj.AddComponent<LayoutElement>());
        }

        public static UIElement<TElement> AddUIElement<TElement>(this GameObject obj)
            where TElement : UIBehaviour
        {
            UIElement<LayoutElement> layout;

            if (obj.TryGetComponent<LayoutElement>(out var element))
                layout = new(element);
            else
                layout = new(obj.AddComponent<LayoutElement>());


            if (typeof(TElement) == typeof(LayoutElement))
                Main.PatchError("UGUI", "Trying to add LayoutElement when has one already\n" + Environment.StackTrace);


            return new(obj.AddComponent<TElement>());
        }

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
            string? text = null,
            Color? color = null,
            TextAlignmentOptions? alignment = null)
        {
            var layout = parent.gameObject.AddUIObject();
            return layout.AddTextElement(text, color, alignment);
        }
    }
}

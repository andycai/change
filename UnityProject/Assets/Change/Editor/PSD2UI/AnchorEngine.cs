using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Anchor preset enumeration for automatic anchor deduction.
    /// Represents 6 common UI anchoring scenarios.
    /// </summary>
    public enum AnchorPreset
    {
        /// <summary>Full-screen stretch — anchors span all four edges with margins.</summary>
        StretchAll,

        /// <summary>Horizontal stretch — element spans full width with fixed height.</summary>
        HorizontalStretch,

        /// <summary>Vertical stretch — element spans full height with fixed width.</summary>
        VerticalStretch,

        /// <summary>Fixed to top-right corner.</summary>
        TopRight,

        /// <summary>Fixed to bottom-center edge.</summary>
        BottomCenter,

        /// <summary>Fixed to middle-center of parent.</summary>
        MiddleCenter
    }

    /// <summary>
    /// Engine for inferring Unity RectTransform anchor presets from PSD layer geometry.
    /// Uses heuristic rules based on size ratios and normalized center positions.
    /// </summary>
    public class AnchorEngine
    {
        /// <summary>
        /// Infers the best anchor preset for a child element based on its geometry
        /// relative to its parent.
        ///
        /// Rules (evaluated in priority order):
        /// 1. StretchAll — child fills &gt;95% of parent in both dimensions
        /// 2. HorizontalStretch — child spans &gt;90% width but &lt;30% height
        /// 3. VerticalStretch — child spans &gt;90% height but &lt;30% width
        /// 4. TopRight — child center is in the top-right corner (both &gt;85%)
        /// 5. BottomCenter — child center is near bottom-center (x 45-55%, y &lt;15%)
        /// 6. MiddleCenter — child center is near true center (both 45-55%)
        /// 7. Default fallback — MiddleCenter
        /// </summary>
        /// <param name="childRect">The child element's rectangle data.</param>
        /// <param name="parentRect">The parent container's rectangle data.</param>
        /// <returns>The inferred AnchorPreset.</returns>
        public AnchorPreset InferAnchor(RectData childRect, RectData parentRect)
        {
            if (childRect == null || parentRect == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.InferAnchor: childRect or parentRect is null. Returning MiddleCenter as fallback.");
                return AnchorPreset.MiddleCenter;
            }

            if (parentRect.Width <= 0f || parentRect.Height <= 0f)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.InferAnchor: parentRect has zero or negative dimensions. Returning MiddleCenter as fallback.");
                return AnchorPreset.MiddleCenter;
            }

            float widthRatio = childRect.Width / parentRect.Width;
            float heightRatio = childRect.Height / parentRect.Height;
            float centerX = (childRect.X + childRect.Width / 2f) / parentRect.Width;
            float centerY = (childRect.Y + childRect.Height / 2f) / parentRect.Height;

            // 1. 全屏拉伸: fills nearly the entire parent
            if (widthRatio > 0.95f && heightRatio > 0.95f)
                return AnchorPreset.StretchAll;

            // 2. 水平拉伸: wide but short
            if (widthRatio > 0.90f && heightRatio < 0.30f)
                return AnchorPreset.HorizontalStretch;

            // 3. 垂直拉伸: tall but narrow
            if (heightRatio > 0.90f && widthRatio < 0.30f)
                return AnchorPreset.VerticalStretch;

            // 4. 右上角固定: positioned in the top-right region
            if (centerX > 0.85f && centerY > 0.85f)
                return AnchorPreset.TopRight;

            // 5. 底部居中: horizontally centered near the bottom edge
            if (centerX >= 0.45f && centerX <= 0.55f && centerY < 0.15f)
                return AnchorPreset.BottomCenter;

            // 6. 完全居中: near the center of the parent
            if (centerX >= 0.45f && centerX <= 0.55f && centerY >= 0.45f && centerY <= 0.55f)
                return AnchorPreset.MiddleCenter;

            // 7. 默认: MiddleCenter fallback
            return AnchorPreset.MiddleCenter;
        }

        /// <summary>
        /// Applies the anchor preset to a RectTransform, setting anchorMin, anchorMax,
        /// pivot, anchoredPosition, sizeDelta, offsetMin, and offsetMax as appropriate.
        /// </summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="preset">The anchor preset to apply.</param>
        /// <param name="rect">The child element's rectangle data (position and size).</param>
        public void ApplyAnchor(RectTransform rt, AnchorPreset preset, RectData rect)
        {
            if (rt == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.ApplyAnchor: RectTransform is null.");
                return;
            }

            if (rect == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.ApplyAnchor: rect is null. Applying preset with zeroed rect.");
                rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 };
            }

            switch (preset)
            {
                case AnchorPreset.StretchAll:
                    ApplyStretchAll(rt, rect);
                    break;

                case AnchorPreset.HorizontalStretch:
                    ApplyHorizontalStretch(rt, rect);
                    break;

                case AnchorPreset.VerticalStretch:
                    ApplyVerticalStretch(rt, rect);
                    break;

                case AnchorPreset.TopRight:
                    ApplyTopRight(rt, rect);
                    break;

                case AnchorPreset.BottomCenter:
                    ApplyBottomCenter(rt, rect);
                    break;

                case AnchorPreset.MiddleCenter:
                    ApplyMiddleCenter(rt, rect);
                    break;

                default:
                    Debug.LogWarning($"[PSD2UI] AnchorEngine.ApplyAnchor: unknown preset '{preset}'. Falling back to MiddleCenter.");
                    ApplyMiddleCenter(rt, rect);
                    break;
            }
        }

        #region Anchor Application Methods

        /// <summary>
        /// StretchAll: element fills entire parent with margins defined by rect.X and rect.Y.
        /// anchorMin=(0,0), anchorMax=(1,1). Margins are applied symmetrically
        /// (left=right=rect.X, bottom=top=rect.Y).
        /// </summary>
        private void ApplyStretchAll(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(rect.X, rect.Y);
            rt.offsetMax = new Vector2(-rect.X, -rect.Y);
        }

        /// <summary>
        /// HorizontalStretch: element stretches across full width with fixed height.
        /// Anchored at bottom edge. offsetMin/offsetMax define left/right margins
        /// and bottom/top edges. Left and right margins are assumed symmetric (rect.X).
        /// </summary>
        private void ApplyHorizontalStretch(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.offsetMin = new Vector2(rect.X, rect.Y);
            rt.offsetMax = new Vector2(-rect.X, rect.Y + rect.Height);
        }

        /// <summary>
        /// VerticalStretch: element stretches across full height with fixed width.
        /// Anchored at left edge. offsetMin/offsetMax define left/right edges
        /// and bottom/top margins. Bottom and top margins are assumed symmetric (rect.Y).
        /// </summary>
        private void ApplyVerticalStretch(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.offsetMin = new Vector2(rect.X, rect.Y);
            rt.offsetMax = new Vector2(rect.X + rect.Width, -rect.Y);
        }

        /// <summary>
        /// TopRight: element fixed to the top-right corner.
        /// anchorMin=anchorMax=(1,1), pivot=(1,1).
        /// anchoredPosition offsets inward from the corner: (-rect.X, -rect.Y).
        /// </summary>
        private void ApplyTopRight(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-rect.X, -rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        /// <summary>
        /// BottomCenter: element fixed to the bottom-center edge.
        /// anchorMin=anchorMax=(0.5, 0), pivot=(0.5, 0).
        /// anchoredPosition.y = rect.Y offsets upward from the bottom edge.
        /// </summary>
        private void ApplyBottomCenter(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        /// <summary>
        /// MiddleCenter: element fixed to the center of the parent.
        /// anchorMin=anchorMax=(0.5, 0.5), pivot=(0.5, 0.5).
        /// anchoredPosition=(0,0) centers the element. sizeDelta sets fixed size.
        /// </summary>
        private void ApplyMiddleCenter(RectTransform rt, RectData rect)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        #endregion
    }
}

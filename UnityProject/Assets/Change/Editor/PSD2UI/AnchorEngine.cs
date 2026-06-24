using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Coordinate system convention for RectData:
    /// Unity convention — origin at bottom-left, X increases right, Y increases upward.
    /// RectData.X = distance from parent's left edge.
    /// RectData.Y = distance from parent's bottom edge.
    /// This convention matches Unity's RectTransform anchoredPosition coordinate system,
    /// so no Y-axis flip is needed when converting from PSD (which uses top-left origin).
    /// PSD-to-Unity Y conversion must be performed before constructing RectData instances.
    /// </summary>

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
    ///
    /// Coordinate convention: RectData uses Unity coordinates (origin bottom-left, Y up).
    /// PSD-to-Unity Y conversion (parentHeight - psdY - height) must be done upstream.
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
        /// 4. TopRight — child center is in the top-right region (centerX &gt;85%, centerY &gt;85%)
        /// 5. BottomCenter — child center is near bottom-center (centerX 45-55%, centerY &lt;15%)
        /// 6. MiddleCenter — child center is near true center (both 45-55%)
        /// 7. Default fallback — MiddleCenter
        ///
        /// Under Unity convention (Y up): centerY near 1 = top, centerY near 0 = bottom.
        /// </summary>
        /// <param name="childRect">The child element's rectangle data (Unity convention).</param>
        /// <param name="parentRect">The parent container's rectangle data (Unity convention).</param>
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

            // 1. StretchAll: fills nearly the entire parent in both dimensions
            if (widthRatio > 0.95f && heightRatio > 0.95f)
                return AnchorPreset.StretchAll;

            // 2. HorizontalStretch: wide but short
            if (widthRatio > 0.90f && heightRatio < 0.30f)
                return AnchorPreset.HorizontalStretch;

            // 3. VerticalStretch: tall but narrow
            if (heightRatio > 0.90f && widthRatio < 0.30f)
                return AnchorPreset.VerticalStretch;

            // 4. TopRight: positioned in the top-right region
            //    Under Unity convention (Y up): centerY > 0.85 means near the top
            if (centerX > 0.85f && centerY > 0.85f)
                return AnchorPreset.TopRight;

            // 5. BottomCenter: horizontally centered, near the bottom edge
            //    Under Unity convention (Y up): centerY < 0.15 means near the bottom
            if (centerX >= 0.45f && centerX <= 0.55f && centerY < 0.15f)
                return AnchorPreset.BottomCenter;

            // 6. MiddleCenter: near the center of the parent
            if (centerX >= 0.45f && centerX <= 0.55f && centerY >= 0.45f && centerY <= 0.55f)
                return AnchorPreset.MiddleCenter;

            // 7. Default fallback
            return AnchorPreset.MiddleCenter;
        }

        /// <summary>
        /// Applies the anchor preset to a RectTransform, setting anchorMin, anchorMax,
        /// pivot, anchoredPosition, sizeDelta, offsetMin, and offsetMax as appropriate.
        ///
        /// parentRect supplies the parent container dimensions needed to compute correct
        /// edge gaps for corner-anchored and stretch presets.
        /// </summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="preset">The anchor preset to apply.</param>
        /// <param name="rect">The child element's rectangle data (position and size, Unity convention).</param>
        /// <param name="parentRect">The parent container's rectangle data (dimensions, Unity convention).</param>
        public void ApplyAnchor(RectTransform rt, AnchorPreset preset, RectData rect, RectData parentRect)
        {
            if (rt == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.ApplyAnchor: RectTransform is null.");
                return;
            }

            if (rect == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.ApplyAnchor: rect is null. Applying preset with fallback rect.");
                rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 };
            }

            if (parentRect == null)
            {
                Debug.LogWarning("[PSD2UI] AnchorEngine.ApplyAnchor: parentRect is null. Using child rect dimensions as fallback.");
                parentRect = new RectData { X = 0, Y = 0, Width = rect.Width * 2f, Height = rect.Height * 2f };
            }

            switch (preset)
            {
                case AnchorPreset.StretchAll:
                    ApplyStretchAll(rt, rect, parentRect);
                    break;

                case AnchorPreset.HorizontalStretch:
                    ApplyHorizontalStretch(rt, rect, parentRect);
                    break;

                case AnchorPreset.VerticalStretch:
                    ApplyVerticalStretch(rt, rect, parentRect);
                    break;

                case AnchorPreset.TopRight:
                    ApplyTopRight(rt, rect, parentRect);
                    break;

                case AnchorPreset.BottomCenter:
                    ApplyBottomCenter(rt, rect, parentRect);
                    break;

                case AnchorPreset.MiddleCenter:
                    ApplyMiddleCenter(rt, rect, parentRect);
                    break;

                default:
                    Debug.LogWarning($"[PSD2UI] AnchorEngine.ApplyAnchor: unknown preset '{preset}'. Falling back to MiddleCenter.");
                    ApplyMiddleCenter(rt, rect, parentRect);
                    break;
            }
        }

        #region Anchor Application Methods

        /// <summary>
        /// StretchAll: element fills the entire parent with margins on all four edges.
        /// anchorMin=(0,0), anchorMax=(1,1).
        /// Margins are computed from actual rect position relative to parent dimensions,
        /// supporting non-centered elements (left margin != right margin).
        /// offsetMin = (leftMargin, bottomMargin).
        /// offsetMax = (rightMargin, topMargin) — negative values for margins inward from right/top.
        /// </summary>
        private void ApplyStretchAll(RectTransform rt, RectData rect, RectData parentRect)
        {
            float leftMargin   = rect.X;
            float bottomMargin = rect.Y;
            float rightMargin  = -(parentRect.Width  - rect.X - rect.Width);
            float topMargin    = -(parentRect.Height - rect.Y - rect.Height);

            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftMargin, bottomMargin);
            rt.offsetMax = new Vector2(rightMargin, topMargin);
        }

        /// <summary>
        /// HorizontalStretch: element stretches across the full width with fixed height.
        /// anchorMin=(0,0), anchorMax=(1,0) — stretch X, anchor Y at bottom edge.
        /// offsetMin/offsetMax define left/right margins and the vertical extent.
        /// </summary>
        private void ApplyHorizontalStretch(RectTransform rt, RectData rect, RectData parentRect)
        {
            float leftMargin  = rect.X;
            float rightMargin = -(parentRect.Width - rect.X - rect.Width);
            float bottomEdge  = rect.Y;
            float topEdge     = rect.Y + rect.Height;

            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.offsetMin = new Vector2(leftMargin, bottomEdge);
            rt.offsetMax = new Vector2(rightMargin, topEdge);
        }

        /// <summary>
        /// VerticalStretch: element stretches across the full height with fixed width.
        /// anchorMin=(0,0), anchorMax=(0,1) — stretch Y, anchor X at left edge.
        /// offsetMin/offsetMax define the horizontal extent and bottom/top margins.
        /// </summary>
        private void ApplyVerticalStretch(RectTransform rt, RectData rect, RectData parentRect)
        {
            float leftEdge    = rect.X;
            float rightEdge   = rect.X + rect.Width;
            float bottomMargin = rect.Y;
            float topMargin   = -(parentRect.Height - rect.Y - rect.Height);

            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.offsetMin = new Vector2(leftEdge, bottomMargin);
            rt.offsetMax = new Vector2(rightEdge, topMargin);
        }

        /// <summary>
        /// TopRight: element fixed to the top-right corner of the parent.
        /// anchorMin=anchorMax=(1,1), pivot=(1,1) — the element's top-right corner
        /// is the pivot point, aligned with the parent's top-right anchor.
        /// anchoredPosition offsets inward: X = -(right gap), Y = -(top gap).
        /// right gap = parentWidth - childLeft - childWidth.
        /// top gap   = parentHeight - childBottom - childHeight.
        /// </summary>
        private void ApplyTopRight(RectTransform rt, RectData rect, RectData parentRect)
        {
            float rightGap = parentRect.Width  - rect.X - rect.Width;
            float topGap   = parentRect.Height - rect.Y - rect.Height;

            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-rightGap, -topGap);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        /// <summary>
        /// BottomCenter: element fixed to the bottom-center edge of the parent.
        /// anchorMin=anchorMax=(0.5, 0), pivot=(0.5, 0).
        /// anchoredPosition.x = horizontal offset from parent center.
        /// anchoredPosition.y = distance from parent bottom edge (rect.Y).
        /// </summary>
        private void ApplyBottomCenter(RectTransform rt, RectData rect, RectData parentRect)
        {
            float horizontalOffset = rect.X + rect.Width / 2f - parentRect.Width / 2f;

            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(horizontalOffset, rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        /// <summary>
        /// MiddleCenter: element fixed to the center of the parent.
        /// anchorMin=anchorMax=(0.5, 0.5), pivot=(0.5, 0.5).
        /// anchoredPosition = offset from parent center to child center.
        /// </summary>
        private void ApplyMiddleCenter(RectTransform rt, RectData rect, RectData parentRect)
        {
            float offsetX = rect.X + rect.Width  / 2f - parentRect.Width  / 2f;
            float offsetY = rect.Y + rect.Height / 2f - parentRect.Height / 2f;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, offsetY);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        #endregion
    }
}

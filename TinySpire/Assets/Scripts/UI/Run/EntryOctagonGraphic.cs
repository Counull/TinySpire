using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Run
{
    /// <summary>绘制入口主菜单的对称双切角细描边，不持有文字或按钮行为。</summary>
    [DisallowMultipleComponent]
    public sealed class EntryOctagonGraphic : MaskableGraphic
    {
        private Color _borderColor = Color.black;
        private Color _topEdgeColor = Color.black;
        private float _cornerCut = 18f;
        private float _outlineWidth = 2f;
        private float _bottomSeparation = 2f;

        /// <summary>使用入口纸面参数配置八边形；内部保持透明以连续显示下方纸纹。</summary>
        internal void Configure(
            Color borderColor,
            Color topEdgeColor,
            float cornerCut,
            float outlineWidth,
            float bottomSeparation)
        {
            _borderColor = borderColor;
            _topEdgeColor = topEdgeColor;
            _cornerCut = Mathf.Max(0f, cornerCut);
            _outlineWidth = Mathf.Max(0f, outlineWidth);
            _bottomSeparation = Mathf.Max(0f, bottomSeparation);
            color = Color.white;
            raycastTarget = true;
            SetVerticesDirty();
        }

        /// <summary>以外环和透明内芯生成八边形网格，并让下缘比其余描边多约两像素分离。</summary>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect outerRect = GetPixelAdjustedRect();
            if (outerRect.width <= 0f || outerRect.height <= 0f)
                return;

            float maximumCut = 0.5f * Mathf.Min(outerRect.width, outerRect.height);
            float outerCut = Mathf.Min(_cornerCut, maximumCut);
            float inset = Mathf.Min(
                _outlineWidth,
                0.25f * Mathf.Min(outerRect.width, outerRect.height));
            Rect innerRect = Rect.MinMaxRect(
                outerRect.xMin + inset,
                outerRect.yMin + inset + _bottomSeparation,
                outerRect.xMax - inset,
                outerRect.yMax - inset);
            float innerCut = Mathf.Max(0f, outerCut - inset);

            Vector2[] outer = BuildCorners(outerRect, outerCut);
            Vector2[] inner = BuildCorners(innerRect, innerCut);
            AddRingVertices(vertexHelper, outerRect, outer, inner);
            AddRingTriangles(vertexHelper);
        }

        /// <summary>按顺时针顺序返回长八边形的八个顶点。</summary>
        private static Vector2[] BuildCorners(Rect rect, float cut)
        {
            return new[]
            {
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut),
                new Vector2(rect.xMin, rect.yMax - cut),
            };
        }

        /// <summary>添加八个外环顶点与八个透明内环顶点，顶边使用更弱的分离色。</summary>
        private void AddRingVertices(
            VertexHelper vertexHelper,
            Rect uvRect,
            Vector2[] outer,
            Vector2[] inner)
        {
            for (int index = 0; index < outer.Length; index++)
            {
                Color outerColor = index <= 1 ? _topEdgeColor : _borderColor;
                vertexHelper.AddVert(outer[index], outerColor, ToUv(uvRect, outer[index]));
            }

            Color transparent = new Color(1f, 1f, 1f, 0f);
            for (int index = 0; index < inner.Length; index++)
                vertexHelper.AddVert(inner[index], transparent, ToUv(uvRect, inner[index]));
        }

        /// <summary>连接外环与内环；透明内芯无需额外三角形即可透出连续纸面。</summary>
        private static void AddRingTriangles(VertexHelper vertexHelper)
        {
            const int cornerCount = 8;
            for (int index = 0; index < cornerCount; index++)
            {
                int next = (index + 1) % cornerCount;
                int innerCurrent = cornerCount + index;
                int innerNext = cornerCount + next;
                vertexHelper.AddTriangle(index, next, innerNext);
                vertexHelper.AddTriangle(index, innerNext, innerCurrent);
            }
        }

        /// <summary>把局部顶点坐标映射为稳定的零到一 UV。</summary>
        private static Vector2 ToUv(Rect rect, Vector2 point)
        {
            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
        }
    }
}

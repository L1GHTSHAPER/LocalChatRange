using System.Collections.Generic;
using UnityEngine;

namespace LocalChatRange
{
    internal struct AreaSettings
    {
        public float Radius;
        public int Segments;
        public int Rings;
        public int RefineSteps;
        public float UpdateInterval;
        public float MaxStepUp;
        public float SurfaceOffset;
        public float OutlineWidth;
        public int LayerMask;
    }

    /// <summary>
    /// Ground-hugging visualisation of the local chat range.
    ///
    /// The game delivers a local message when the distance between the sender's and the receiver's
    /// player roots is at most the radius (a sphere, not a cylinder). The area shows where a standing
    /// player would have to be to hear you: every surface point whose distance to your feet is within
    /// the radius. The surface is found with vertical raycasts on a polar grid; the outer edge in each
    /// direction is refined with a binary search, so slopes, stairs and drops shrink the circle exactly
    /// as the game's distance check does.
    /// </summary>
    internal sealed class RangeArea
    {
        // Vertical slack so that flat ground exactly at the radius still counts as inside.
        const float VerticalTolerance = 0.05f;
        // The shape is re-sampled even while standing still, to pick up moving objects.
        const float IdleRefreshInterval = 0.5f;
        const float MoveThresholdSqr = 0.03f * 0.03f;
        // Fill opacity at the centre relative to the edge.
        const float CenterAlpha = 0.35f;
        // Fill triangles that rise more than this and are steeper than TentMaxSlope would stretch like a tent
        // from the floor onto furniture; they are skipped. Stairs and ramps stay below the slope limit.
        const float TentMinHeight = 0.35f;
        static readonly float TentMaxSlopeCos = Mathf.Cos(50f * Mathf.Deg2Rad);

        readonly GameObject _root;
        readonly Mesh _mesh;
        readonly Material _fillMaterial;
        readonly Material _outlineMaterial;

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _fillTriangles = new List<int>();
        readonly List<int> _outlineTriangles = new List<int>();

        // Per (segment, ring) sample.
        Vector3[] _samples = new Vector3[0];
        bool[] _sampleHit = new bool[0];
        bool[] _vertexUsable = new bool[0];
        // Per segment.
        float[] _boundaryRadius = new float[0];
        Vector3[] _boundaryPoint = new Vector3[0];
        Vector3[] _innerPoint = new Vector3[0];
        int[] _outermostHit = new int[0];

        Vector3 _builtCenter;
        float _lastBuildTime = float.NegativeInfinity;
        bool _dirty = true;

        public RangeArea()
        {
            _root = new GameObject("LocalChatRange.Area");
            Object.DontDestroyOnLoad(_root);
            _root.SetActive(false);

            _mesh = new Mesh { name = "LocalChatRange.Area", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
            _root.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _fillMaterial = Materials.Create("LocalChatRange.Fill");
            _outlineMaterial = Materials.Create("LocalChatRange.Outline");
            var renderer = _root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _fillMaterial, _outlineMaterial };
            Materials.ConfigureRenderer(renderer);
        }

        public bool IsAlive => _root != null && _mesh != null;

        public bool HasMaterials => _fillMaterial != null && _outlineMaterial != null;

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
            {
                _root.SetActive(visible);
                if (visible)
                    _dirty = true;
            }
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void ApplyAppearance(Color fill, Color outline, bool outlineOnTop)
        {
            Materials.SetColor(_fillMaterial, fill);
            Materials.SetColor(_outlineMaterial, outline);
            Materials.SetAlwaysOnTop(_outlineMaterial, outlineOnTop);
        }

        public void Tick(Vector3 center, in AreaSettings settings)
        {
            float now = Time.unscaledTime;
            float sinceBuild = now - _lastBuildTime;
            bool moved = (center - _builtCenter).sqrMagnitude > MoveThresholdSqr;
            if (_dirty || sinceBuild >= IdleRefreshInterval || (moved && sinceBuild >= settings.UpdateInterval))
            {
                Build(center, settings);
                _builtCenter = center;
                _lastBuildTime = now;
                _dirty = false;
            }

            // Vertices are relative to the centre they were sampled around,
            // so between re-samples the shape glides along with the player.
            _root.transform.SetPositionAndRotation(center, Quaternion.identity);
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
            if (_mesh != null)
                Object.Destroy(_mesh);
            if (_fillMaterial != null)
                Object.Destroy(_fillMaterial);
            if (_outlineMaterial != null)
                Object.Destroy(_outlineMaterial);
        }

        void Build(Vector3 center, in AreaSettings s)
        {
            int segments = s.Segments;
            int rings = s.Rings;
            float radius = s.Radius;
            float angleStep = 2f * Mathf.PI / segments;
            EnsureCapacity(segments, rings);

            if (!Sample(center, 0f, 0f, 0f, s, out Vector3 centerPoint))
                centerPoint = center;

            // 1. Sample the surface and find the outer edge in every direction.
            for (int i = 0; i < segments; i++)
            {
                float dx = Mathf.Cos(i * angleStep);
                float dz = Mathf.Sin(i * angleStep);
                int first = i * rings;

                int outermost = 0;
                for (int j = 1; j <= rings; j++)
                {
                    int k = first + j - 1;
                    _sampleHit[k] = Sample(center, dx, dz, radius * j / rings, s, out _samples[k]);
                    if (_sampleHit[k])
                        outermost = j;
                }

                float boundary;
                Vector3 boundaryPoint;
                if (outermost == rings)
                {
                    boundary = radius;
                    boundaryPoint = _samples[first + rings - 1];
                }
                else
                {
                    // Binary search between the outermost hit and the miss right after it.
                    float inside = radius * outermost / rings;
                    float outside = radius * (outermost + 1) / rings;
                    boundary = inside;
                    boundaryPoint = outermost > 0 ? _samples[first + outermost - 1] : centerPoint;
                    for (int n = 0; n < s.RefineSteps; n++)
                    {
                        float middle = 0.5f * (inside + outside);
                        if (Sample(center, dx, dz, middle, s, out Vector3 point))
                        {
                            inside = middle;
                            boundary = middle;
                            boundaryPoint = point;
                        }
                        else
                        {
                            outside = middle;
                        }
                    }
                }

                _outermostHit[i] = outermost;
                _boundaryRadius[i] = boundary;
                _boundaryPoint[i] = boundaryPoint;
            }

            // 2. Vertices, relative to the sampling centre and lifted slightly off the surface.
            _vertices.Clear();
            _colors.Clear();
            _fillTriangles.Clear();
            _outlineTriangles.Clear();
            Vector3 toLocal = new Vector3(0f, s.SurfaceOffset, 0f) - center;

            _vertices.Add(centerPoint + toLocal);
            _colors.Add(new Color(1f, 1f, 1f, CenterAlpha));

            for (int i = 0; i < segments; i++)
            {
                float dx = Mathf.Cos(i * angleStep);
                float dz = Mathf.Sin(i * angleStep);
                int first = i * rings;
                float boundary = _boundaryRadius[i];
                float inner = Mathf.Max(0f, boundary - s.OutlineWidth);

                // Height of the outline's inner edge: interpolated between the last surface hit before it and the edge.
                Vector3 below = centerPoint;
                float belowRadius = 0f;
                for (int j = _outermostHit[i]; j >= 1; j--)
                {
                    float r = radius * j / rings;
                    int k = first + j - 1;
                    if (r <= inner && _sampleHit[k])
                    {
                        below = _samples[k];
                        belowRadius = r;
                        break;
                    }
                }
                float t = boundary - belowRadius > 1e-4f ? (inner - belowRadius) / (boundary - belowRadius) : 1f;
                _innerPoint[i] = new Vector3(
                    center.x + dx * inner,
                    Mathf.Lerp(below.y, _boundaryPoint[i].y, t),
                    center.z + dz * inner);

                for (int j = 1; j <= rings; j++)
                {
                    int k = first + j - 1;
                    float r = radius * j / rings;
                    if (r >= inner)
                    {
                        // Rings past the outline collapse onto its inner edge, so fill and outline never overlap.
                        _vertexUsable[k] = true;
                        _vertices.Add(_innerPoint[i] + toLocal);
                        _colors.Add(Color.white);
                    }
                    else if (_sampleHit[k])
                    {
                        _vertexUsable[k] = true;
                        float edge = r / Mathf.Max(boundary, 1e-4f);
                        _vertices.Add(_samples[k] + toLocal);
                        _colors.Add(new Color(1f, 1f, 1f, Mathf.Lerp(CenterAlpha, 1f, edge * edge)));
                    }
                    else
                    {
                        // No surface within range here (hole, inside a wall...): leave a gap.
                        _vertexUsable[k] = false;
                        _vertices.Add(center + toLocal);
                        _colors.Add(Color.clear);
                    }
                }
            }

            int outlineStart = _vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                _vertices.Add(_innerPoint[i] + toLocal);
                _vertices.Add(_boundaryPoint[i] + toLocal);
                _colors.Add(Color.white);
                _colors.Add(Color.white);
            }

            // 3. Triangles: a fan around the centre, quads between rings, and a band for the outline.
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = i * rings;
                int b = next * rings;

                if (_vertexUsable[a] && _vertexUsable[b])
                    AddFillTriangle(0, 1 + a, 1 + b);

                for (int j = 0; j < rings - 1; j++)
                {
                    int a0 = a + j, a1 = a + j + 1, b0 = b + j, b1 = b + j + 1;
                    if (_vertexUsable[a0] && _vertexUsable[a1] && _vertexUsable[b0] && _vertexUsable[b1])
                    {
                        AddFillTriangle(1 + a0, 1 + a1, 1 + b1);
                        AddFillTriangle(1 + a0, 1 + b1, 1 + b0);
                    }
                }

                int inner0 = outlineStart + 2 * i;
                int inner1 = outlineStart + 2 * next;
                AddQuad(_outlineTriangles, inner0, inner0 + 1, inner1 + 1, inner1);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(_fillTriangles, 0, false);
            _mesh.SetTriangles(_outlineTriangles, 1, false);
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// Finds the top-most surface at horizontal distance <paramref name="r"/> from the centre that lies
        /// inside the chat sphere. Only the part of the vertical line inside the sphere is tested, capped at
        /// MaxStepUp above the feet so ceilings and upper floors are not picked up.
        /// </summary>
        static bool Sample(Vector3 center, float dx, float dz, float r, in AreaSettings s, out Vector3 point)
        {
            float halfChord = Mathf.Sqrt(Mathf.Max(0f, s.Radius * s.Radius - r * r)) + VerticalTolerance;
            float above = Mathf.Min(halfChord, s.MaxStepUp);
            var origin = new Vector3(center.x + dx * r, center.y + above, center.z + dz * r);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, above + halfChord, s.LayerMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        void EnsureCapacity(int segments, int rings)
        {
            int count = segments * rings;
            if (_samples.Length != count)
            {
                _samples = new Vector3[count];
                _sampleHit = new bool[count];
                _vertexUsable = new bool[count];
            }
            if (_boundaryRadius.Length != segments)
            {
                _boundaryRadius = new float[segments];
                _boundaryPoint = new Vector3[segments];
                _innerPoint = new Vector3[segments];
                _outermostHit = new int[segments];
            }
        }

        void AddFillTriangle(int a, int b, int c)
        {
            Vector3 pa = _vertices[a], pb = _vertices[b], pc = _vertices[c];
            float rise = Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)) - Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y));
            if (rise > TentMinHeight)
            {
                Vector3 normal = Vector3.Cross(pb - pa, pc - pa);
                float length = normal.magnitude;
                if (length > 1e-6f && Mathf.Abs(normal.y) < length * TentMaxSlopeCos)
                    return;
            }
            AddTriangle(_fillTriangles, a, b, c);
        }

        static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            AddTriangle(triangles, a, b, c);
            AddTriangle(triangles, a, c, d);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace LocalChatRange
{
    /// <summary>Finds the other players who would receive your local messages.</summary>
    internal static class PlayersInRange
    {
        const float FallbackSearchInterval = 2f;

        static PlayerController[] _fallbackPlayers = new PlayerController[0];
        static float _nextFallbackSearch;

        /// <summary>Fills <paramref name="positions"/> with the root positions of other players within the radius.</summary>
        public static void Find(Transform localPlayer, float radius, List<Vector3> positions)
        {
            positions.Clear();
            Vector3 origin = localPlayer.position;
            float radiusSqr = radius * radius;

            IList<PlayerController> players = GetPlayers();
            for (int n = 0; n < players.Count; n++)
            {
                PlayerController player = players[n];
                if (player == null)
                    continue;
                Transform target = player.transform;
                if (target == localPlayer)
                    continue;

                // Same test the game runs on the receiving side.
                Vector3 position = target.position;
                if ((position - origin).sqrMagnitude <= radiusSqr)
                    positions.Add(position);
            }
        }

        static IList<PlayerController> GetPlayers()
        {
            PlayerPanelController panel = GameAccess.PlayerPanel;
            List<PlayerController> list = panel != null ? panel.PlayerControllers : null;
            if (list != null && list.Count > 0)
                return list;

            if (Time.unscaledTime >= _nextFallbackSearch)
            {
                _nextFallbackSearch = Time.unscaledTime + FallbackSearchInterval;
                _fallbackPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            }
            return _fallbackPlayers;
        }
    }

    /// <summary>Rings under the feet of other players who are inside your local chat range.</summary>
    internal sealed class PlayerMarkers
    {
        const float MarkerRadius = 0.45f;
        const float RingWidth = 0.2f;
        const int RingSegments = 40;

        readonly GameObject _container;
        readonly Mesh _ringMesh;
        readonly Material _material;
        readonly List<GameObject> _pool = new List<GameObject>();

        public PlayerMarkers()
        {
            _container = new GameObject("LocalChatRange.PlayerMarkers");
            Object.DontDestroyOnLoad(_container);
            _ringMesh = BuildRingMesh();
            _material = Materials.Create("LocalChatRange.Marker");
        }

        public bool IsAlive => _container != null && _ringMesh != null;

        public void SetColor(Color color)
        {
            Materials.SetColor(_material, color);
        }

        /// <summary>Shows one marker per position and hides the rest.</summary>
        public void Show(List<Vector3> playerPositions, in AreaSettings s)
        {
            for (int n = 0; n < playerPositions.Count; n++)
                Place(GetMarker(n), playerPositions[n], s);
            for (int n = playerPositions.Count; n < _pool.Count; n++)
            {
                if (_pool[n] != null && _pool[n].activeSelf)
                    _pool[n].SetActive(false);
            }
        }

        public void HideAll()
        {
            foreach (GameObject marker in _pool)
            {
                if (marker != null && marker.activeSelf)
                    marker.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (_container != null)
                Object.Destroy(_container);
            if (_ringMesh != null)
                Object.Destroy(_ringMesh);
            if (_material != null)
                Object.Destroy(_material);
        }

        static void Place(GameObject marker, Vector3 playerPosition, in AreaSettings s)
        {
            Vector3 position = playerPosition;
            Quaternion rotation = Quaternion.identity;
            if (Physics.Raycast(playerPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, s.LayerMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            // Slightly above the area fill so the ring is not hidden by it.
            marker.transform.SetPositionAndRotation(position + Vector3.up * (s.SurfaceOffset * 2f), rotation);
            if (!marker.activeSelf)
                marker.SetActive(true);
        }

        GameObject GetMarker(int index)
        {
            while (_pool.Count <= index)
                _pool.Add(null);
            if (_pool[index] != null)
                return _pool[index];

            var marker = new GameObject("LocalChatRange.Marker");
            marker.transform.SetParent(_container.transform, false);
            marker.transform.localScale = Vector3.one * MarkerRadius;
            marker.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            Materials.ConfigureRenderer(renderer);
            marker.SetActive(false);
            _pool[index] = marker;
            return marker;
        }

        /// <summary>Unit ring in the XZ plane with a faint filled centre.</summary>
        static Mesh BuildRingMesh()
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var colors = new List<Color> { new Color(1f, 1f, 1f, 0.1f) };
            var triangles = new List<int>();
            float inner = 1f - RingWidth;

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / RingSegments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * inner);   // 1 + 3i: edge of the faint disc
                vertices.Add(direction * inner);   // 2 + 3i: inner edge of the ring
                vertices.Add(direction);           // 3 + 3i: outer edge of the ring
                colors.Add(new Color(1f, 1f, 1f, 0.25f));
                colors.Add(Color.white);
                colors.Add(Color.white);
            }

            for (int i = 0; i < RingSegments; i++)
            {
                int current = 1 + 3 * i;
                int next = 1 + 3 * ((i + 1) % RingSegments);
                triangles.Add(0);
                triangles.Add(current);
                triangles.Add(next);

                triangles.Add(current + 1);
                triangles.Add(current + 2);
                triangles.Add(next + 2);
                triangles.Add(current + 1);
                triangles.Add(next + 2);
                triangles.Add(next + 1);
            }

            var mesh = new Mesh { name = "LocalChatRange.Marker", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

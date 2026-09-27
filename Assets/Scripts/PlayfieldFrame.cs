using UnityEngine;

namespace CandyCruisers
{
    [ExecuteAlways, RequireComponent(typeof(LineRenderer))]
    public sealed class PlayfieldFrame : MonoBehaviour
    {
        [SerializeField] private PlayerMovement player;
        private LineRenderer boundary;
        private LineRenderer outline;
        private readonly LineRenderer[] corners = new LineRenderer[4];

        public void Configure(PlayerMovement controller)
        {
            player = controller;
            boundary = GetComponent<LineRenderer>();
            outline = Line("Cabinet outline", .018f, true);
            float x = PlayerMovement.HalfWidth + .12f, y = 5.62f, bevel = .18f;
            outline.positionCount = 8;
            outline.SetPositions(new[] {
                new Vector3(-x + bevel, -y), new Vector3(x - bevel, -y),
                new Vector3(x, -y + bevel), new Vector3(x, y - bevel),
                new Vector3(x - bevel, y), new Vector3(-x + bevel, y),
                new Vector3(-x, y - bevel), new Vector3(-x, -y + bevel)
            });
            for (int i = 0; i < corners.Length; i++)
            {
                float side = i < 2 ? -1 : 1, end = i % 2 == 0 ? -1 : 1;
                corners[i] = Line("Cabinet corner " + i, .065f, false);
                corners[i].positionCount = 3;
                corners[i].SetPositions(new[] {
                    new Vector3(side * 2.52f, end * 5.43f),
                    new Vector3(side * 2.93f, end * 5.43f),
                    new Vector3(side * 2.93f, end * 5.02f)
                });
            }
            Refresh();
        }

        private LineRenderer Line(string name, float width, bool loop)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
            }
            var line = child.GetComponent<LineRenderer>();
            line.sharedMaterial = boundary.sharedMaterial;
            line.useWorldSpace = false;
            line.loop = loop;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = boundary.sortingOrder;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            return line;
        }

        private void OnEnable()
        {
            boundary = GetComponent<LineRenderer>();
            outline = transform.Find("Cabinet outline")?.GetComponent<LineRenderer>();
            for (int i = 0; i < corners.Length; i++)
                corners[i] = transform.Find("Cabinet corner " + i)?.GetComponent<LineRenderer>();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (boundary == null) return;
            var color = player != null ? player.DisplayColor : EnemyPalette.Get(EnemyColor.Blue);
            boundary.startWidth = boundary.endWidth = .045f;
            Tint(boundary, color, .85f);
            Tint(outline, color, .5f);
            foreach (var corner in corners) Tint(corner, color, 1);
        }

        private static void Tint(LineRenderer line, Color color, float alpha)
        {
            if (line == null) return;
            color.a = alpha;
            line.startColor = line.endColor = color;
        }
    }
}

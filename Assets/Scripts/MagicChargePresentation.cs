using UnityEngine;

namespace CandyCruisers
{
    // Presentation-only placeholder: replace without changing shot timing or collision.
    public sealed class MagicChargePresentation : MonoBehaviour
    {
        private LineRenderer ring;
        private readonly LineRenderer[] sparks = new LineRenderer[3];
        public bool IsVisible => ring != null && ring.enabled;

        public void Configure(LineRenderer tongue)
        {
            if (ring != null) return;
            ring = CreateLine("Charge focus", 33, tongue);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = CreateLine("Charge spark " + i, 3, tongue);
        }

        private LineRenderer CreateLine(string name, int count, LineRenderer tongue)
        {
            var child = new GameObject(name, typeof(LineRenderer));
            child.transform.SetParent(transform, false);
            var renderer = child.GetComponent<LineRenderer>();
            renderer.sharedMaterial = tongue.sharedMaterial;
            renderer.sortingLayerID = tongue.sortingLayerID;
            renderer.sortingOrder = tongue.sortingOrder + 1;
            renderer.useWorldSpace = false;
            renderer.positionCount = count;
            renderer.numCapVertices = 4;
            renderer.enabled = false;
            return renderer;
        }

        public void Draw(bool charging, float progress)
        {
            if (ring == null) return;
            ring.enabled = charging;
            foreach (var spark in sparks) spark.enabled = charging;
            if (!charging) return;
            progress = Mathf.Clamp01(progress);
            float phase = progress * Mathf.PI * 8;
            float radius = Mathf.Lerp(.34f, .075f, progress);
            var center = Vector3.up * .22f;
            var color = Color.Lerp(EnemyPalette.Get((EnemyColor)(Mathf.FloorToInt(progress * 15) % 6)), Color.white, .35f);
            ring.startColor = ring.endColor = color;
            ring.startWidth = ring.endWidth = .025f + .025f * progress + .008f * Mathf.Sin(phase * 2);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = phase + i * Mathf.PI * 2 / (ring.positionCount - 1);
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = -phase + i * Mathf.PI * 2 / sparks.Length;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var side = new Vector3(-direction.y, direction.x, 0);
                sparks[i].startWidth = .018f;
                sparks[i].endWidth = .04f;
                sparks[i].startColor = new Color(color.r, color.g, color.b, .25f);
                sparks[i].endColor = Color.white;
                sparks[i].SetPosition(0, center + direction * (radius + .12f));
                sparks[i].SetPosition(1, center + direction * radius * .65f + side * .06f);
                sparks[i].SetPosition(2, center + direction * radius * .2f);
            }
        }

        private void OnDisable() => Draw(false, 0);
    }
}

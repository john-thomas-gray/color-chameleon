using UnityEngine;

namespace CandyCruisers
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class GameplayFraming : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer starBackground;
        private Camera view;

        public void SetBackground(SpriteRenderer background)
        {
            starBackground = background;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Max(6f, 3.2f / Mathf.Max(0.01f, view.aspect));
            if (starBackground == null || starBackground.sprite == null) return;
            Vector2 size = starBackground.sprite.bounds.size;
            float scale = Mathf.Max(2f * view.orthographicSize * view.aspect / size.x,
                2f * view.orthographicSize / size.y);
            starBackground.transform.localScale = Vector3.one * scale;
        }
    }
}

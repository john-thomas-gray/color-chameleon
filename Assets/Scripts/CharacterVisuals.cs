using UnityEngine;
using UnityEngine.Events;

namespace CandyCruisers
{
    // Only this adapter knows where an actor's artwork lives.
    public sealed class CharacterVisuals : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Bounds hitBounds;
        [SerializeField] private bool boundsConfigured;
        [Header("Replaceable cues (empty uses the placeholder)")]
        [SerializeField] private PresentationCue firePrefab;
        [SerializeField] private PresentationCue matchPrefab;
        [SerializeField] private PresentationCue defeatPrefab;
        [SerializeField] private Animator animator;
        [SerializeField] private string fireTrigger = "Fire", matchTrigger = "Match", defeatTrigger = "Defeat";
        [Header("Presentation-only hooks")]
        public UnityEvent Fired = new UnityEvent();
        public UnityEvent Matched = new UnityEvent();
        public UnityEvent Defeated = new UnityEvent();
        public UnityEvent AnimationFinished = new UnityEvent();
        public SpriteRenderer Body => body;
        public Transform Root => visualRoot;
        public Bounds HitBounds
        {
            get
            {
                var size = Vector3.Scale(hitBounds.size, transform.lossyScale);
                return new Bounds(transform.TransformPoint(hitBounds.center),
                    new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            }
        }

        public static CharacterVisuals Ensure(GameObject actor)
        {
            var result = actor.GetComponent<CharacterVisuals>();
            if (result == null) result = actor.AddComponent<CharacterVisuals>();
            if (result.body == null) result.Configure(actor.GetComponentInChildren<SpriteRenderer>());
            return result;
        }

        public void Configure(SpriteRenderer renderer, Transform root = null)
        {
            body = renderer;
            if (root != null) visualRoot = root;
            if (visualRoot == null) visualRoot = body != null && body.transform != transform ? body.transform.parent : transform;
            if (body == null || body.sprite == null || boundsConfigured) return;
            var bounds = body.bounds;
            hitBounds = new Bounds(transform.InverseTransformPoint(bounds.center),
                transform.InverseTransformVector(bounds.size));
            boundsConfigured = true;
        }

        public void SetCuePrefabs(PresentationCue firing, PresentationCue matching, PresentationCue defeat)
        { firePrefab = firing; matchPrefab = matching; defeatPrefab = defeat; }

        public void CopyVisualScale(CharacterVisuals other)
        {
            var size = other.Root.lossyScale;
            var parent = Root.parent != null ? Root.parent.lossyScale : Vector3.one;
            Root.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
        }

        public PresentationCue Fire(EnemyColor color)
        {
            Trigger(fireTrigger); Fired.Invoke();
            return Spawn(firePrefab, PresentationCue.Kind.Fire, color, 1);
        }
        public PresentationCue Match(EnemyColor color, int depth, int? multiplier = null)
        {
            Trigger(matchTrigger); Matched.Invoke();
            return Spawn(matchPrefab, PresentationCue.Kind.Match, color, depth, multiplier);
        }
        public PresentationCue Defeat(EnemyColor color, int depth, int? multiplier = null)
        {
            Trigger(defeatTrigger); Defeated.Invoke();
            return Spawn(defeatPrefab, PresentationCue.Kind.Defeat, color, depth, multiplier);
        }
        public PresentationCue PlayerDefeat(EnemyColor color)
        {
            Trigger(defeatTrigger); Defeated.Invoke();
            return Spawn(defeatPrefab, PresentationCue.Kind.PlayerDefeat, color, 1);
        }
        private PresentationCue Spawn(PresentationCue prefab, PresentationCue.Kind kind, EnemyColor color, int depth, int? multiplier = null)
        {
            if (body == null || !Application.isPlaying) return null;
            return PresentationCue.Spawn(prefab, kind, body, color, depth, multiplier);
        }
        private void Trigger(string trigger)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(trigger)) return;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger)
                { animator.SetTrigger(trigger); break; }
        }
        // Optional clip event; never fires a projectile or commits a combat result.
        public void OnAnimationFinished() => AnimationFinished.Invoke();
    }
}

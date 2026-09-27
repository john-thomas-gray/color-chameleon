using UnityEngine;

namespace CandyCruisers
{
    // Put the Animator on the same visual child as this clip-event receiver.
    public sealed class VisualAnimationEvents : MonoBehaviour
    {
        [SerializeField] private CharacterVisuals owner;
        public void Configure(CharacterVisuals actor) => owner = actor;
        public void OnAnimationFinished()
        {
            var cue = GetComponentInParent<PresentationCue>();
            if (cue != null) cue.Finish();
            else if (owner != null) owner.OnAnimationFinished();
        }
    }
}

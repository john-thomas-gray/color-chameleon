using UnityEngine;

namespace CandyCruisers
{
    // Presentation-only effects. Gameplay never depends on animation completion callbacks.
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private SpriteRenderer body;
        private SpriteRenderer imitation;
        private LineRenderer tendril, portal, streak;
        private float phaseRemaining, shiftRemaining, clock;
        private EnemyColor phaseColor;
        private bool warpArrival;
        private bool growing;
        private Vector3 restingBodyScale;
        public bool IsWarping => IsPhasing && warpArrival;
        public void GrowIn(EnemyColor color)
        {
            if (!growing) restingBodyScale = body.transform.localScale;
            growing = true;
            warpArrival = false;
            phaseRemaining = .8f;
            Tick(0, color, 0);
        }
        private int shiftDirection;
        private GridEnemy target;
        private Vector3 targetPosition;
        private float imitationRemaining;
        private Sprite oldSprite;
        private EnemyColor imitationColor;
        private bool linkedImitation;
        private float revealRemaining;
        private float movementPulseRemaining;
        public const float MovementPulseSeconds = .38f;
        private LineRenderer[] surgeGlow, surgeCore;
        public void MovementPulse()
        {
            if (body == null) return;
            EnsureSurge();
            movementPulseRemaining = MovementPulseSeconds;
            Tick(0, EnemyColor.Green, 0);
        }
        private Quaternion restingRotation;
        private bool aimingApplied;
        public const float GreenSpinSeconds = .35f;
        private float greenSpinRemaining;
        private Quaternion greenRestingRotation;
        public void SpinGreen()
        {
            if (body == null) return;
            if (greenSpinRemaining <= 0) greenRestingRotation = body.transform.localRotation;
            greenSpinRemaining = GreenSpinSeconds;
        }
        public void AimRed(Vector3 direction, bool aiming, float seconds, bool firing = false)
        {
            if (body == null || !aiming && !aimingApplied) return;
            if (!aimingApplied) restingRotation = body.transform.localRotation;
            aimingApplied = true;
            float angle = aiming ? Vector3.SignedAngle(Vector3.down, direction, Vector3.forward) : 0;
            var desired = restingRotation * Quaternion.Euler(0, 0, angle);
            body.transform.localRotation = firing ? desired : Quaternion.RotateTowards(
                body.transform.localRotation, desired, 540f * Mathf.Max(0, seconds));
            if (!aiming && Quaternion.Angle(body.transform.localRotation, restingRotation) < .001f)
            { body.transform.localRotation = restingRotation; aimingApplied = false; }
        }
        public void BeginImitation(GridEnemy neighbor, Sprite previousSprite, bool linked = true)
        {
            linkedImitation = linked;
            target = neighbor;
            targetPosition = neighbor.transform.position;
            oldSprite = previousSprite;
            imitationRemaining = EnemyAbilities.ImitationSeconds;
            imitationColor = neighbor.Color;
            body.sprite = neighbor.Visuals.Body.sprite;
        }
        public void DetachImitation()
        {
            linkedImitation = false;
            target = null;
            if (tendril != null) tendril.enabled = false;
        }
        public void RevealDisguise() => revealRemaining = .5f;
        public void Configure(SpriteRenderer renderer)
        {
            body = renderer;
            if (tendril != null) return;
            tendril = Line("Assimilation tendril", 17, .035f);
            portal = Line("Summoning rift", 33, .045f);
            streak = Line("Speed wake", 7, .055f);
            var overlay = new GameObject("Imitation overlay", typeof(SpriteRenderer));
            overlay.transform.SetParent(GetComponent<GridEnemy>().Visuals.Root, false);
            imitation = overlay.GetComponent<SpriteRenderer>();
            imitation.sortingOrder = body.sortingOrder + 2;
            imitation.enabled = false;
        }
        private LineRenderer Line(string name, int count, float width)
        {
            var child = new GameObject(name, typeof(LineRenderer));
            child.transform.SetParent(GetComponent<GridEnemy>().Visuals.Root, false);
            var line = child.GetComponent<LineRenderer>();
            line.sharedMaterial = body.sharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = body.sortingOrder + 3;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }
        public bool IsPhasing => phaseRemaining > 0;
        public float SpawnOpacity => 1 - phaseRemaining / .8f;
        public void PhaseIn(EnemyColor color, EnemyColor? effectColor = null)
        {
            if (growing) body.transform.localScale = restingBodyScale;
            growing = false;
            warpArrival = true;
            phaseColor = effectColor ?? color;
            phaseRemaining = .8f;
            Tick(0, color, 0);
        }
        public void SpeedShift(int direction) { shiftDirection = direction; shiftRemaining = .35f; }
        public void Tick(float seconds, EnemyColor color, float warning)
        {
            if (body == null) return;
            if (greenSpinRemaining > 0)
            {
                greenSpinRemaining = Mathf.Max(0, greenSpinRemaining - Mathf.Max(0, seconds));
                float turn = 1 - greenSpinRemaining / GreenSpinSeconds;
                body.transform.localRotation = greenSpinRemaining > 0 ?
                    greenRestingRotation * Quaternion.Euler(0, 0, -360 * turn) : greenRestingRotation;
            }
            clock += seconds;
            phaseRemaining = Mathf.Max(0, phaseRemaining - seconds);
            shiftRemaining = Mathf.Max(0, shiftRemaining - seconds);
            imitationRemaining = Mathf.Max(0, imitationRemaining - seconds);
            revealRemaining = Mathf.Max(0, revealRemaining - seconds);
            movementPulseRemaining = Mathf.Max(0, movementPulseRemaining - seconds);
            float assimilation = 1 - imitationRemaining / EnemyAbilities.ImitationSeconds;
            Color tint = EnemyPalette.Get(color);
            Color brilliant = color == EnemyColor.Purple ? new Color(1, .3f, 1) :
                color == EnemyColor.Green ? new Color(.65f, 1, .75f) : Color.white;
            var music = GetComponentInParent<GameplayMusicPlayer>();
            float pulsePhase = music != null && music.InGameplayRun ? music.BeatPosition * Mathf.PI * 4 : clock * 28;
            body.color = Color.Lerp(tint, brilliant, warning * (.6f + .4f * Mathf.Cos(pulsePhase)));
            if (color == EnemyColor.Green && movementPulseRemaining > 0)
                body.color = Color.Lerp(body.color, new Color(.85f, 1, .9f),
                    Mathf.Min(1, movementPulseRemaining * 8));
            PresentMovementSurge(color);
            if (phaseRemaining > 0) body.color = new Color(body.color.r, body.color.g, body.color.b, SpawnOpacity);
            if (growing)
            {
                float t = SpawnOpacity - 1;
                float scale = 1 + 2.2f * t * t * t + 1.2f * t * t;
                body.transform.localScale = restingBodyScale * Mathf.Max(0, scale);
                if (phaseRemaining <= 0) { body.transform.localScale = restingBodyScale; growing = false; }
            }
            portal.enabled = phaseRemaining > 0 && warpArrival;
            if (portal.enabled)
            {
                Color riftColor = Color.Lerp(EnemyPalette.Get(phaseColor), Color.white, .25f);
                riftColor.a = Mathf.Min(1, phaseRemaining * 3);
                portal.startColor = portal.endColor = riftColor;
                for (int i = 0; i < portal.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / (portal.positionCount - 1);
                    float radius = .36f + .045f * Mathf.Sin(i * 7 + clock * 36);
                    portal.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius * .7f, Mathf.Sin(angle) * radius, 0));
                }
            }
            streak.enabled = shiftRemaining > 0;
            if (streak.enabled)
            {
                streak.startColor = new Color(.3f, 1, .6f, shiftRemaining * 2);
                streak.endColor = new Color(.3f, 1, .6f, 0);
                for (int i = 0; i < streak.positionCount; i++)
                    streak.SetPosition(i, transform.position + new Vector3(-shiftDirection * i * .10f, i % 2 == 0 ? -.06f : .06f, 0));
            }
            tendril.enabled = imitationRemaining > 0 && linkedImitation;
            imitation.enabled = imitationRemaining > 0;
            if (imitationRemaining > 0)
            {
                Color destination = EnemyPalette.Get(imitationColor);
                Color yellow = EnemyPalette.Get(EnemyColor.Yellow);
                body.color = Color.Lerp(yellow, destination, assimilation);
                imitation.sprite = oldSprite;
                imitation.color = new Color(yellow.r, yellow.g, yellow.b, 1 - assimilation);
                float size = body.sprite.bounds.size.x / imitation.sprite.bounds.size.x;
                imitation.transform.localScale = Vector3.one * size;
                tendril.startColor = yellow;
                tendril.endColor = destination;
                if (target != null && target.isActiveAndEnabled) targetPosition = target.transform.position;
                Vector3 delta = targetPosition - transform.position;
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.forward);
                for (int i = 0; i < tendril.positionCount; i++)
                {
                    float t = (float)i / (tendril.positionCount - 1);
                    float reach = Mathf.Min(1, assimilation * 4);
                    float ripple = Mathf.Sin(t * 24 - clock * 9) * Mathf.Sin(t * Mathf.PI) * .06f;
                    tendril.SetPosition(i, transform.position + delta * t * reach + side * ripple);
                }
            }
            if (revealRemaining > 0)
                body.color = Color.Lerp(EnemyPalette.Get(EnemyColor.Yellow), Color.white,
                    .5f + .5f * Mathf.Cos((.5f - revealRemaining) * Mathf.PI * 12));
        }
        private void EnsureSurge()
        {
            if (surgeGlow != null) return;
            surgeGlow = new LineRenderer[5];
            surgeCore = new LineRenderer[5];
            for (int i = 0; i < surgeGlow.Length; i++)
            {
                surgeGlow[i] = Line("Green surge glow " + i, i == 0 ? 25 : 7, .065f);
                surgeCore[i] = Line("Green surge core " + i, i == 0 ? 25 : 7, .018f);
                surgeGlow[i].useWorldSpace = surgeCore[i].useWorldSpace = false;
                surgeCore[i].sortingOrder++;
            }
        }

        private void PresentMovementSurge(EnemyColor color)
        {
            if (surgeGlow == null) return;
            bool active = color == EnemyColor.Green && movementPulseRemaining > 0;
            float age = MovementPulseSeconds - movementPulseRemaining;
            float fade = Mathf.Min(1, movementPulseRemaining / .12f);
            float radius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.y);
            int frame = Mathf.FloorToInt(age * 24);
            for (int arc = 0; arc < surgeGlow.Length; arc++)
            {
                var glow = surgeGlow[arc];
                var core = surgeCore[arc];
                glow.enabled = core.enabled = active;
                if (!active) continue;
                glow.startColor = glow.endColor = new Color(.05f, 1, .2f, .9f * fade);
                core.startColor = core.endColor = new Color(.8f, 1, .65f, fade);
                for (int i = 0; i < glow.positionCount; i++)
                {
                    float t = (float)i / (glow.positionCount - 1);
                    // Deterministic flicker never consumes the gameplay random sequence.
                    float jitter = Mathf.Sin((i % 24) * 17.7f + arc * 9.3f + frame * 13.1f);
                    Vector3 offset;
                    if (arc == 0)
                    {
                        float angle = t * Mathf.PI * 2;
                        float reach = radius * (1.14f + .22f * jitter);
                        offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                    }
                    else
                    {
                        float angle = arc * Mathf.PI * .5f + .35f * Mathf.Sin(frame * 2 + arc);
                        var outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
                        var side = new Vector3(-outward.y, outward.x);
                        offset = outward * radius * (.8f + t * 1.05f) + side * radius * jitter * .27f;
                    }
                    Vector3 point = glow.transform.InverseTransformPoint(body.bounds.center + offset);
                    glow.SetPosition(i, point);
                    core.SetPosition(i, point);
                }
            }
        }

        public void Clear()
        {
            if (growing && body != null) body.transform.localScale = restingBodyScale;
            growing = false;
            if (greenSpinRemaining > 0 && body != null) body.transform.localRotation = greenRestingRotation;
            greenSpinRemaining = 0;
            if (aimingApplied && body != null) body.transform.localRotation = restingRotation;
            aimingApplied = false;
            phaseRemaining = shiftRemaining = imitationRemaining = revealRemaining = movementPulseRemaining = 0;
            linkedImitation = false;
            target = null;
            if (tendril == null) return;
            tendril.enabled = portal.enabled = streak.enabled = imitation.enabled = false;
            if (surgeGlow != null)
                for (int i = 0; i < surgeGlow.Length; i++) surgeGlow[i].enabled = surgeCore[i].enabled = false;
        }
        private void OnDisable() => Clear();
    }
}

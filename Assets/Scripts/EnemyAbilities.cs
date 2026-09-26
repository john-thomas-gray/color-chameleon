using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyAbilities : MonoBehaviour
    {
        private GridEnemy enemy;
        private GameObject missilePrefab;
        private SpriteRenderer shield;
        private SpriteRenderer body;
        private EnemyColor currentColor;
        private float cooldown;
        private EnemyGrid grid;
        private EnemyPresentation presentation;
        private float castRemaining;
        private float burstRemaining;
        public bool IsCasting => castRemaining > 0;
        public bool Suspended { get; set; }
        public bool ShieldActive { get; private set; }
        public bool Warning => enemy != null && enemy.Color != EnemyColor.Blue && (cooldown <= 0.6f || IsCasting);
        public Bounds ShieldBounds => shield.bounds;
        public void BeginSpawnEffect()
        {
            presentation.PhaseIn(enemy.Color);
            RefreshShield();
        }

        public void Configure(GridEnemy owner, GameObject projectile, Sprite shieldSprite)
        {
            enemy = owner;
            missilePrefab = projectile;
            body = GetComponent<SpriteRenderer>();
            grid = GetComponentInParent<EnemyGrid>();
            presentation = GetComponent<EnemyPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<EnemyPresentation>();
            presentation.Configure(body);
            if (shield == null)
            {
                var visual = new GameObject("Shield", typeof(SpriteRenderer));
                visual.transform.SetParent(transform, false);
                shield = visual.GetComponent<SpriteRenderer>();
                shield.sprite = shieldSprite;
                shield.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                shield.sortingOrder = body.sortingOrder + 1;
                visual.transform.localScale = Vector3.one *
                    (Mathf.Max(body.sprite.bounds.size.x, body.sprite.bounds.size.y) * 1.25f / shieldSprite.bounds.size.x);
            }
            ResetAbility();
        }

        private void ResetAbility()
        {
            currentColor = enemy.Color;
            castRemaining = burstRemaining = 0;
            presentation.Clear();
            ShieldActive = currentColor == EnemyColor.Blue;
            cooldown = NextCooldown();
            if (shield != null && shield.sprite != null)
                shield.transform.localScale = Vector3.one *
                    (Mathf.Max(body.sprite.bounds.size.x, body.sprite.bounds.size.y) * 1.25f / shield.sprite.bounds.size.x);
            RefreshVisuals();
        }

        private float NextCooldown()
        {
            var range = CombatBalance.CooldownRange(currentColor, grid.GetComponent<GameSession>()?.Progress.Level ?? 1);
            return Random.Range(range.x, range.y);
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (Suspended || enemy == null || !isActiveAndEnabled) return;
            if (currentColor != enemy.Color) ResetAbility();
            seconds = Mathf.Max(0, seconds);
            if (burstRemaining > 0)
            {
                float used = Mathf.Min(seconds, burstRemaining);
                burstRemaining -= used;
                grid.GetComponent<EnemyGridMovement>()?.AdvanceDistance(used / .18f * .1f);
                if (Suspended || !isActiveAndEnabled) return;
            }
            if (IsCasting)
            {
                castRemaining = Mathf.Max(0, castRemaining - seconds);
                if (castRemaining <= 0) CompleteCast();
            }
            else cooldown -= seconds;
            if (enemy.Color == EnemyColor.Red && cooldown <= 0)
            {
                var missile = Instantiate(missilePrefab, transform.position + Vector3.down * 0.3f, Quaternion.identity);
                missile.GetComponent<EnemyMissile>().SetTarget(FindFirstObjectByType<PlayerMovement>());
                cooldown = NextCooldown();
            }
            else if (enemy.Color == EnemyColor.Blue && !ShieldActive && cooldown <= 0)
                ShieldActive = true;
            else if (enemy.Color >= EnemyColor.Green && cooldown <= 0 && !IsCasting)
            {
                cooldown = NextCooldown();
                if (enemy.Color == EnemyColor.Yellow)
                {
                    var targets = new System.Collections.Generic.List<GridEnemy>();
                    foreach (var neighbor in grid.Model.Neighbors(enemy.Column, enemy.Row))
                        if (neighbor.Color != EnemyColor.Yellow)
                            targets.Add(grid.View(neighbor.Id));
                    if (targets.Count > 0)
                    {
                        var target = targets[Random.Range(0, targets.Count)];
                        var oldSprite = body.sprite;
                        grid.SetColor(enemy.Id, target.Color);
                        ResetAbility();
                        presentation.BeginImitation(target, oldSprite);
                    }
                }
                else if (enemy.Color == EnemyColor.Purple) CompleteCast();
                else castRemaining = .8f;
            }
            RefreshVisuals();
            presentation.Tick(seconds, enemy.Color, Warning ? 1 : 0);
            RefreshShield();
        }

        private void CompleteCast()
        {
            if (enemy.Color == EnemyColor.Green)
            {
                burstRemaining = .18f;
                var movement = grid.GetComponent<EnemyGridMovement>();
                foreach (var visual in grid.GetComponentsInChildren<EnemyPresentation>()) visual.SpeedShift(movement.Direction);
            }
            else if (enemy.Color == EnemyColor.Purple)
            {
                grid.GetComponent<EnemyRowSpawner>()?.TrySummon(enemy);
            }
        }

        public bool Absorb(EnemyColor shotColor)
        {
            if (enemy == null || enemy.Color != EnemyColor.Blue || !ShieldActive || shotColor == EnemyColor.Blue) return false;
            ShieldActive = false;
            cooldown = NextCooldown();
            RefreshVisuals();
            return true;
        }

        private void RefreshVisuals()
        {
            RefreshShield();
            body.color = Warning ? Color.Lerp(EnemyPalette.Get(enemy.Color), Color.white, 0.65f) : EnemyPalette.Get(enemy.Color);
        }
        private void RefreshShield()
        {
            shield.enabled = ShieldActive && enemy.Color == EnemyColor.Blue;
            shield.color = new Color(.3f, .85f, 1f, .9f * presentation.SpawnOpacity);
        }
        private void OnDisable()
        {
            ShieldActive = false;
            if (shield != null) shield.enabled = false;
            if (body != null && enemy != null) body.color = EnemyPalette.Get(enemy.Color);
            castRemaining = burstRemaining = 0;
            if (presentation != null) presentation.Clear();
        }
    }
}

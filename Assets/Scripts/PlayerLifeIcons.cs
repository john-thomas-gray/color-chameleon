using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerLifeIcons
    {
        public const float TravelSeconds = ComboBreakAnimation.TravelSeconds;
        public const float DrainSeconds = ComboBreakAnimation.DrainSeconds;
        public const float SlashSeconds = ComboBreakAnimation.SlashSeconds;
        public const float SplitSeconds = ComboBreakAnimation.SplitSeconds;
        public const float DefaultStartDelay = FullSetCelebration.StepDuration;
        public const float GrowthSeconds = TravelSeconds;
        public const float BurstSeconds = SplitSeconds;
        public const float Duration = TravelSeconds + DrainSeconds + SlashSeconds + SplitSeconds;
        public const int FragmentCount = 12;
        private const int LayoutSlots = 3;
        public int ConsumedSlot { get; private set; } = -1;
        public bool Active => ConsumedSlot >= 0;
        public PlayerLifeGainAnimation Gain { get; } = new PlayerLifeGainAnimation();
        private readonly List<int> pendingGains = new List<int>();
        private readonly HashSet<int> reservedGains = new HashSet<int>();
        public bool Animating => Active || Gain.Active || pendingGains.Count > 0 || reservedGains.Count > 0;
        public bool Started { get; private set; }
        public float Age { get; private set; }
        private bool timingPrepared;
        private AudioClip musicClip;
        private float musicSeconds;
        private float travelStartsAt;
        private float travelEndsAt = TravelSeconds;
        private float slashStartsAt = TravelSeconds + DrainSeconds;
        public float PlaybackDuration => slashStartsAt + SlashSeconds + SplitSeconds;
        public float RemainingSeconds => Active ? Mathf.Max(0, PlaybackDuration - Age) : 0;
        public float TravelProgress => Started ? Mathf.Clamp01((Age - travelStartsAt) / TravelSeconds) : 0;
        public float DrainProgress => Mathf.Clamp01((Age - travelEndsAt) / Mathf.Max(.0001f, slashStartsAt - travelEndsAt));
        public float SlashProgress => Mathf.Clamp01((Age - slashStartsAt) / SlashSeconds);
        public float SlashOpacity => Active && Age >= slashStartsAt ?
            1 - Mathf.Clamp01((Age - slashStartsAt - SlashSeconds) / .14f) : 0;
        public float SplitProgress => Mathf.Clamp01((Age - slashStartsAt - SlashSeconds) / SplitSeconds);
        public float Opacity => Active ? 1 - SplitProgress * SplitProgress : 0;
        public float GrowthScale => Mathf.Lerp(1, 3, Mathf.SmoothStep(0, 1, TravelProgress));
        public float BurstProgress => SplitProgress;
        private Transform artwork;
        private SpriteRenderer[] pieces;

        public void BeginLoss(int slot)
        {
            ResetLoss();
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives) return;
            ConsumedSlot = slot;
            pendingGains.RemoveAll(pending => pending >= slot);
            reservedGains.RemoveWhere(reserved => reserved >= slot);
            // A death takes centerstage; any still-owned reward can enter after the loss finishes.
            if (Gain.Active && Gain.Slot < slot) pendingGains.Insert(0, Gain.Slot);
            Gain.Reset();
        }

        public void Reset()
        {
            ResetLoss();
            Gain.Reset();
            pendingGains.Clear();
            reservedGains.Clear();
        }

        public void BeginGain(int slot, GameplayMusicPlayer music = null)
        {
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives || IsGainPending(slot)) return;
            pendingGains.Add(slot);
            StartNextGain(music);
        }

        public void ReserveGain(int slot)
        {
            if (slot >= 0 && slot < PlayerMovement.MaxExtraLives && !IsGainPending(slot)) reservedGains.Add(slot);
        }

        public void ReleaseGain(int slot, GameplayMusicPlayer music = null, float? appearanceBeat = null)
        {
            if (!reservedGains.Remove(slot)) return;
            if (Active || Gain.Active || pendingGains.Count > 0) BeginGain(slot, music);
            else Gain.Begin(slot, music, appearanceBeat);
        }

        public bool IsGainPending(int slot) => Gain.Active && Gain.Slot == slot ||
            pendingGains.Contains(slot) || reservedGains.Contains(slot);

        private void StartNextGain(GameplayMusicPlayer music)
        {
            if (Active || Gain.Active || pendingGains.Count == 0) return;
            int slot = pendingGains[0];
            pendingGains.RemoveAt(0);
            Gain.Begin(slot, music);
        }

        private void ResetLoss()
        {
            ConsumedSlot = -1; Age = 0; Started = false;
            timingPrepared = false;
            musicClip = null;
            travelStartsAt = DefaultStartDelay;
            travelEndsAt = DefaultStartDelay + TravelSeconds;
            slashStartsAt = DefaultStartDelay * 2;
        }

        public void Tick(float seconds, GameplayMusicPlayer music = null)
        {
            TickLoss(seconds, music);
            Gain.Tick(seconds, music);
            StartNextGain(music);
        }

        private void TickLoss(float seconds, GameplayMusicPlayer music)
        {
            if (!Active) return;
            seconds = Mathf.Max(0, seconds);
            if (!timingPrepared)
            {
                timingPrepared = true;
                if (music != null && music.Source.isPlaying && music.Source.clip != null)
                {
                    musicClip = music.Source.clip;
                    musicSeconds = music.PlaybackSeconds;
                    // The first tick is measured from the hit frame: the spent spare enters on the next beat.
                    float began = musicSeconds - seconds;
                    float startBeat = Mathf.Floor(music.BeatPositionAtTime(began)) + 1;
                    travelStartsAt = Mathf.Max(0, music.SecondsAtBeat(startBeat) - began);
                    travelEndsAt = travelStartsAt + TravelSeconds;
                    slashStartsAt = Mathf.Max(travelEndsAt + .0001f, music.SecondsAtBeat(startBeat + 1) - began);
                }
            }
            else if (musicClip != null)
            {
                if (music != null && music.Source.clip == musicClip && music.Source.isPlaying &&
                    music.PlaybackSeconds >= musicSeconds)
                {
                    float now = music.PlaybackSeconds;
                    seconds = now - musicSeconds;
                    musicSeconds = now;
                }
                else musicClip = null;
            }
            Age = Mathf.Min(PlaybackDuration, Age + seconds);
            Started = Age >= travelStartsAt;
            if (Age >= PlaybackDuration) ResetLoss();
        }

        public static float BeatScale(float beat) => 1 + .09f * GameplayMusicPlayer.BeatPulse(beat, true);

        public static Rect RowRect(float x, float y, int viewHeight)
        {
            float size = viewHeight < 400 ? 10 : viewHeight < 600 ? 14 : 18;
            float center = viewHeight < 400 ? 33 : viewHeight < 600 ? 38 : 50;
            return new Rect(x + 2, y + center - size / 2, 96, size);
        }

        public static float ProgressOffset(int viewHeight) => viewHeight < 400 ? 42 : viewHeight < 600 ? 58 : 78;

        public static Rect IconRect(Rect row, int slot, float scale = 1)
        {
            float cell = row.width / LayoutSlots;
            float size = Mathf.Min(20, Mathf.Min(row.height, cell - 8));
            var center = new Vector2(row.x + cell * .5f + slot * (size + 4), row.center.y);
            size *= scale;
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        public static Rect FragmentRect(Rect icon, int fragment, float progress)
        {
            float angle = fragment * Mathf.PI * 2 / FragmentCount;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float scale = icon.width / 20;
            var center = icon.center + (direction * Mathf.Lerp(7, 22, progress) + Vector2.up * progress * progress * 3) * scale;
            float size = Mathf.Lerp(5, 0, progress) * scale;
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        public static Rect LossDisplayRect(Rect resting, float screenWidth, float screenHeight, float age)
        {
            float travel = Mathf.SmoothStep(0, 1, age / TravelSeconds);
            float scale = Mathf.Min(3, screenWidth * .8f / resting.width);
            var size = Vector2.Lerp(resting.size, resting.size * scale, travel);
            var center = Vector2.Lerp(resting.center, new Vector2(screenWidth / 2, screenHeight / 2), travel);
            return new Rect(center - size / 2, size);
        }

        public Vector2 HalfOffset(float height, bool lower)
        {
            float direction = lower ? 1 : -1;
            float split = SplitProgress;
            return new Vector2(direction * height * .6f * split,
                direction * height * .45f * split + height * split * split);
        }

        public Color LossTint(Color source) =>
            Color.Lerp(source, Color.white, Mathf.SmoothStep(0, 1, DrainProgress));

        public void Draw(PlayerMovement player, Rect row, float opacity) =>
            Draw(player, row, opacity, Screen.width, Screen.height);

        public void Draw(PlayerMovement player, Rect row, float opacity, float screenWidth, float screenHeight)
        {
            var visuals = CharacterVisuals.Ensure(player.gameObject);
            if (artwork != visuals.Root || pieces == null)
            {
                artwork = visuals.Root;
                pieces = artwork.GetComponentsInChildren<SpriteRenderer>(true);
                Array.Sort(pieces, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            }
            var music = player.Music;
            float beat = music != null && music.InGameplayRun ? BeatScale(music.BeatPosition) : 1;
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                bool consuming = Active && slot == ConsumedSlot;
                if (!consuming && slot >= player.ExtraLives) continue;
                if (!consuming && IsGainPending(slot)) continue;
                if (consuming && Started)
                    DrawBreakingPlayer(IconRect(row, slot), visuals.Body, player.DisplayColor, opacity, screenWidth, screenHeight);
                else DrawPlayer(IconRect(row, slot, beat), visuals.Body, player.DisplayColor, opacity);
            }
            if (Gain.Visible)
                DrawPlayer(Gain.DisplayRect(IconRect(row, Gain.Slot), screenWidth, screenHeight),
                    visuals.Body, player.DisplayColor, opacity * Gain.Opacity);
        }

        private void DrawPlayer(Rect rect, SpriteRenderer body, Color color, float opacity, bool drainAll = false)
        {
            if (body == null || body.sprite == null) return;
            var bounds = body.bounds;
            foreach (var piece in pieces)
                if (piece != null && piece.sprite != null) bounds.Encapsulate(piece.bounds);
            float scale = Mathf.Min(rect.width / Mathf.Max(.001f, bounds.size.x), rect.height / Mathf.Max(.001f, bounds.size.y));
            foreach (var piece in pieces)
            {
                if (piece == null || piece.sprite == null) continue;
                var part = piece.bounds;
                var offset = (Vector2)(part.center - bounds.center) * scale;
                var size = (Vector2)part.size * scale;
                var target = new Rect(rect.center.x + offset.x - size.x / 2, rect.center.y - offset.y - size.y / 2, size.x, size.y);
                var tint = piece == body ? color : piece.color;
                if (drainAll) tint = Color.Lerp(tint, Color.white, Mathf.SmoothStep(0, 1, DrainProgress));
                tint.a *= opacity;
                DrawSprite(target, piece.sprite, tint);
            }
        }

        private void DrawBreakingPlayer(Rect resting, SpriteRenderer body, Color color, float opacity,
            float screenWidth, float screenHeight)
        {
            var rect = LossDisplayRect(resting, screenWidth, screenHeight, Age - travelStartsAt);
            float alpha = opacity * Opacity;
            if (Age < slashStartsAt + SlashSeconds)
                DrawPlayer(rect, body, color, alpha, true);
            else
            {
                DrawBreakingHalf(rect, body, color, alpha, false);
                DrawBreakingHalf(rect, body, color, alpha, true);
            }
            DrawSlash(rect, opacity);
        }

        private void DrawSlash(Rect rect, float opacity)
        {
            if (SlashOpacity <= 0 || SlashProgress <= 0) return;
            var previous = GUI.matrix;
            var previousColor = GUI.color;
            var from = new Vector2(rect.x - rect.width * .2f, rect.y + rect.height * .752f);
            var to = new Vector2(rect.xMax + rect.width * .2f, rect.y + rect.height * .248f);
            var delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = new Color(1, 1, 1, opacity * SlashOpacity);
            float thickness = Mathf.Max(2, rect.height * .035f);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness / 2, delta.magnitude * SlashProgress, thickness), Texture2D.whiteTexture);
            GUI.matrix = previous;
            GUI.color = previousColor;
        }

        private void DrawBreakingHalf(Rect rect, SpriteRenderer body, Color color, float opacity, bool lower)
        {
            rect.position += HalfOffset(rect.height, lower);
            int strips = Mathf.Clamp(Mathf.CeilToInt(rect.width), 16, 512);
            for (int i = 0; i < strips; i++)
            {
                float cut = rect.height * Mathf.Lerp(.68f, .32f, (i + .5f) / strips);
                float top = lower ? cut + 1 : 0;
                float height = lower ? rect.height - top : cut - 1;
                if (height <= 0) continue;
                float left = Mathf.Round(rect.x + rect.width * i / strips) - rect.x;
                float right = Mathf.Round(rect.x + rect.width * (i + 1) / strips) - rect.x;
                GUI.BeginGroup(new Rect(rect.x + left, rect.y + top, right - left, height));
                DrawPlayer(new Rect(-left, -top, rect.width, rect.height), body, color, opacity, true);
                GUI.EndGroup();
            }
        }

        private static void DrawSprite(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            var texture = sprite.texture;
            var source = sprite.rect;
            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width,
                source.y / texture.height, source.width / texture.width, source.height / texture.height), true);
            GUI.color = previous;
        }
    }
}

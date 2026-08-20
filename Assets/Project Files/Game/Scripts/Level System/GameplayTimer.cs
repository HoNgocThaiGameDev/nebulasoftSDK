using System;
using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    public class GameplayTimer
    {
        public float MaxTime { get; private set; }
        public float CurrentTime { get; private set; }
        public TimeSpan CurrentTimeSpan { get; private set; }
        public bool IsActive { get; private set; }
        public event SimpleCallback OnTimerFinished;
        public event SimpleCallback OnPaused;
        public event SimpleCallback OnResumed;
        public delegate void TimeSpanCallback(TimeSpan timespan);
        public event TimeSpanCallback OnTimeSpanChanged;
        private readonly Dictionary<string, int> pauseReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private TweenCase adjustTweenCase;

        public void Start() { IsActive = true; CurrentTime = MaxTime; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); }
        public void Update()
        {
            if (!IsActive) return;
            CurrentTime -= Time.deltaTime;
            if (CurrentTime <= 0) { IsActive = false; CurrentTime = 0; CurrentTimeSpan = TimeSpan.Zero; OnTimerFinished?.Invoke(); }
            else { int prevSeconds = CurrentTimeSpan.Seconds; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); if (CurrentTimeSpan.Seconds != prevSeconds) OnTimeSpanChanged?.Invoke(CurrentTimeSpan); }
        }
        public void Pause(string reason = "default") { if (pauseReasons.TryGetValue(reason, out var count)) pauseReasons[reason] = count + 1; else pauseReasons[reason] = 1; UpdateActiveState(); }
        public void Resume(string reason = "default") { if (pauseReasons.TryGetValue(reason, out var count)) { if (count <= 1) pauseReasons.Remove(reason); else pauseReasons[reason] = count - 1; UpdateActiveState(); } }
        private void UpdateActiveState(bool forceActive = false)
        {
            bool shouldBeActive = forceActive || pauseReasons.Count == 0;
            if (IsActive == shouldBeActive) return;
            IsActive = shouldBeActive;
            if (IsActive) { adjustTweenCase?.CompleteActive(); OnResumed?.Invoke(); } else OnPaused?.Invoke();
        }
        public void AdjustTime(float time, float duration, Ease.Type easingType)
        {
            adjustTweenCase?.CompleteActive();
            float prevT = 0, addedTime = 0;
            adjustTweenCase = Tween.DoFloat(0, 1.0f, duration, t => { t = Mathf.Clamp01(t); float step = t - prevT; prevT = t; float adjustedTime = time * step; addedTime += adjustedTime; CurrentTime += adjustedTime; MaxTime += adjustedTime; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); OnTimeSpanChanged?.Invoke(CurrentTimeSpan); if (IsActive && !adjustTweenCase.IsCompleted) adjustTweenCase.Complete(); }).SetEasing(easingType).OnComplete(() => { float timeDiff = time - addedTime; if (timeDiff > 0) { CurrentTime += timeDiff; MaxTime += timeDiff; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); OnTimeSpanChanged?.Invoke(CurrentTimeSpan); } });
        }
        public void AdjustTime(float time) { CurrentTime += time; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); }
        public void SetMaxTime(float maxTime) { MaxTime = maxTime; CurrentTime = MaxTime; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); }
        public void Reset() { IsActive = false; CurrentTime = MaxTime; CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime); }
    }
}

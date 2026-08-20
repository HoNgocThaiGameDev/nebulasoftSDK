using UnityEngine;
using System.Collections.Generic;
using System;

namespace NebulaSoft
{
    public class TweensHolder
    {
        protected TweenCase[] tweens;
        public TweenCase[] Tweens => tweens;

        protected int tweensCount;

        protected bool hasActiveTweens = false;

        protected bool requiresActiveReorganization = false;
        protected int reorganizeFromID = -1;
        protected int maxActiveLookupID = -1;

        protected List<TweenCase> killingTweens = new List<TweenCase>();

#if UNITY_EDITOR
        protected int maxTweensAmount = 0;
#endif

        protected UpdateMethod updateMethod;
        
        public TweensHolder(UpdateMethod updateMethod, int defaultAmount)
        {
            this.updateMethod = updateMethod;

            tweens = new TweenCase[defaultAmount];
        }

        public void AddTween(TweenCase tween)
        {
            if (tweensCount >= tweens.Length)
            {
                Array.Resize(ref tweens, tweens.Length + 50);

                Debug.LogWarning("[Tween]: The amount of the tweens (update) was adjusted. Current size - " + tweens.Length + ". Change the default amount to prevent performance leak!");
            }

            if (requiresActiveReorganization)
                ReorganizeUpdateActiveTweens();

            tween.IsActive = true;
            tween.ActiveID = (maxActiveLookupID = tweensCount);

            tweens[tweensCount] = tween;
            tweensCount++;

            hasActiveTweens = true;

#if UNITY_EDITOR
            if (maxTweensAmount < tweensCount)
                maxTweensAmount = tweensCount;
#endif
        }

        private void ReorganizeUpdateActiveTweens()
        {
            if (tweensCount <= 0)
            {
                maxActiveLookupID = -1;
                reorganizeFromID = -1;
                requiresActiveReorganization = false;

                return;
            }

            if (reorganizeFromID == maxActiveLookupID)
            {
                maxActiveLookupID--;
                reorganizeFromID = -1;
                requiresActiveReorganization = false;

                return;
            }

            int defaultOffset = 1;
            int tweensTempCount = maxActiveLookupID + 1;

            maxActiveLookupID = reorganizeFromID - 1;

            for (int i = reorganizeFromID + 1; i < tweensTempCount; i++)
            {
                TweenCase tween = tweens[i];
                if (tween != null)
                {
                    tween.ActiveID = (maxActiveLookupID = i - defaultOffset);

                    tweens[i - defaultOffset] = tween;
                    tweens[i] = null;
                }
                else
                {
                    defaultOffset++;
                }
            }

            requiresActiveReorganization = false;
            reorganizeFromID = -1;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (!hasActiveTweens)
                return;

            if (requiresActiveReorganization)
                ReorganizeUpdateActiveTweens();

            for (int i = 0; i < tweensCount; i++)
            {
                TweenCase tween = tweens[i];
                if (tween != null)
                {
                    if (!tween.Validate())
                    {
                        tween.Kill();
                    }
                    else
                    {
                        if (tween.IsActive && !tween.IsPaused)
                        {
                            if (!tween.IsUnscaled)
                            {
                                if (Time.timeScale == 0)
                                    continue;

                                if (tween.Delay > 0 && tween.Delay > tween.CurrentDelay)
                                {
                                    tween.UpdateDelay(deltaTime);
                                }
                                else
                                {
                                    tween.UpdateState(deltaTime);

                                    tween.Invoke(deltaTime);
                                }
                            }
                            else
                            {
                                if (tween.Delay > 0 && tween.Delay > tween.CurrentDelay)
                                {
                                    tween.UpdateDelay(unscaledDeltaTime);
                                }
                                else
                                {
                                    tween.UpdateState(unscaledDeltaTime);

                                    tween.Invoke(unscaledDeltaTime);
                                }
                            }

                            if (tween.IsCompleted)
                            {
                                tween.DefaultComplete();

                                tween.InvokeCompleteEvent();

                                tween.Kill();
                            }
                        }
                    }
                }
            }

            int killingTweensCount = killingTweens.Count - 1;
            for (int i = killingTweensCount; i > -1; i--)
            {
                RemoveActiveTween(killingTweens[i]);
            }
            killingTweens.Clear();
        }

        private void RemoveActiveTween(TweenCase tween)
        {
            int activeId = tween.ActiveID;
            tween.ActiveID = -1;

            requiresActiveReorganization = true;

            if (reorganizeFromID == -1 || reorganizeFromID > activeId)
            {
                reorganizeFromID = activeId;
            }

            tweens[activeId] = null;

            tweensCount--;
            hasActiveTweens = (tweensCount > 0);
        }

        public void Pause()
        {
            for (int i = 0; i < tweensCount; i++)
            {
                TweenCase tween = tweens[i];
                if (tween != null)
                {
                    tween.Pause();
                }
            }
        }

        public void Resume()
        {
            for (int i = 0; i < tweensCount; i++)
            {
                TweenCase tween = tweens[i];
                if (tween != null)
                {
                    tween.Resume();
                }
            }
        }

        public void Kill()
        {
            for (int i = 0; i < tweensCount; i++)
            {
                TweenCase tween = tweens[i];
                if (tween != null)
                {
                    tween.Kill();
                }
            }
        }

        public void MarkForKilling(TweenCase tween)
        {
            killingTweens.Add(tween);
        }

        public void Unload()
        {
            if(!tweens.IsNullOrEmpty())
            {
                for (int i = 0; i < tweens.Length; i++)
                {
                    tweens[i] = null;
                }
            }

            killingTweens.Clear();

            tweensCount = 0;
            hasActiveTweens = false;

            requiresActiveReorganization = false;
            reorganizeFromID = -1;
            maxActiveLookupID = -1;
        }
    }
}
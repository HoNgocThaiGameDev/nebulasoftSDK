using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    public abstract class TweenCase
    {
        // System variables
        public int ActiveID;
        public bool IsActive;

        protected float currentDelay;
        public float CurrentDelay => currentDelay;

        protected float delay;
        public float Delay => delay;

        protected float state;
        public float State => state;

        protected int updateMethodIndex;
        public int UpdateMethodIndex => updateMethodIndex;
        public UpdateMethod UpdateMethod => (UpdateMethod)updateMethodIndex;

        protected float duration;
        public float Duration => duration;

        protected bool isPaused;
        public bool IsPaused => isPaused;

        protected bool isUnscaled;
        public bool IsUnscaled => isUnscaled;

        protected bool isCompleted;
        public bool IsCompleted => isCompleted;

        protected bool isKilling;
        public bool IsKilling => isKilling;

        protected Ease.IEasingFunction easeFunction;

        protected event SimpleCallback tweenCompleted;

        protected GameObject parentObject;
        public GameObject ParentObject => parentObject;

        private List<CallbackData> callbackData;

        public TweenCase()
        {
            SetEasing(Ease.Type.Linear);
        }

        public virtual TweenCase StartTween()
        {
            Tween.AddTween(this);

            return this;
        }

        public abstract bool Validate();

        /// <summary>
        /// Stop and remove tween
        /// </summary>
        public TweenCase Kill()
        {
            if (!isKilling)
            {
                IsActive = false;

                Tween.MarkForKilling(this);

                isKilling = true;
            }

            return this;
        }

        /// <summary>
        /// Complete tween
        /// </summary>
        public TweenCase Complete()
        {
            if (isPaused)
                isPaused = false;

            state = 1;

            isCompleted = true;

            return this;
        }

        /// <summary>
        /// Pause current coroutine.
        /// </summary>
        public TweenCase Pause()
        {
            isPaused = true;

            return this;
        }

        /// <summary>
        /// Play tween if it was paused.
        /// </summary>
        public TweenCase Resume()
        {
            isPaused = false;

            return this;
        }


        /// <summary>
        /// Reset tween state
        /// </summary>
        public void Reset()
        {
            state = 0;
        }

        /// <summary>
        /// Set tween easing based on animation curve
        /// </summary>
        public TweenCase SetCurveEasing(AnimationCurve easingCurve)
        {
            easeFunction = new AnimationCurveEasingFunction(easingCurve);

            return this;
        }

        /// <summary>
        /// Set tween easing
        /// </summary>
        public TweenCase SetCustomEasing(Ease.IEasingFunction easeFunction)
        {
            this.easeFunction = easeFunction;

            return this;
        }

        /// <summary>
        /// Interpolate current easing function.
        /// </summary>
        public float Interpolate(float p)
        {
            return easeFunction.Interpolate(p);
        }

        #region Set
        public TweenCase SetDelay(float delay)
        {
            this.delay = delay;

            currentDelay = 0;

            return this;
        }

        /// <summary>
        /// Update method can be set only before StartTween method is called.
        /// </summary>
        public TweenCase SetUpdateMethod(UpdateMethod updateMethod)
        {
            this.updateMethodIndex = (int)updateMethod;

            return this;
        }

        public TweenCase SetUnscaledMode(bool isUnscaled)
        {
            this.isUnscaled = isUnscaled;

            return this;
        }

        /// <summary>
        /// Set tween easing function.
        /// </summary>
        public TweenCase SetEasing(Ease.Type ease)
        {
            easeFunction = Ease.GetFunction(ease);

            return this;
        }

        /// <summary>
        /// Change tween duration.
        /// </summary>
        public TweenCase SetDuration(float duration)
        {
            this.duration = duration;

            return this;
        }
        #endregion

        /// <summary>
        /// System method. Update state value.
        /// </summary>
        public void UpdateState(float deltaTime)
        {
            state += Mathf.Min(1.0f, deltaTime / duration);

            if (state >= 1)
            {
                isCompleted = true;
            } 
            else if(!callbackData.IsNullOrEmpty())
            {
                for(int i = 0; i < callbackData.Count; i++)
                {
                    var data = callbackData[i];

                    if(state >= data.t)
                    {
                        data.callback?.Invoke();
                        callbackData.RemoveAt(i);
                        i--;
                    }
                }
            }
                
        }

        /// <summary>
        /// System method. Update delay value.
        /// </summary>
        public void UpdateDelay(float deltaTime)
        {
            currentDelay += deltaTime;
        }

        /// <summary>
        /// Init function that called when it will completed.
        /// </summary>
        /// <param name="callback">Complete function.</param>
        public TweenCase OnComplete(SimpleCallback callback)
        {
            tweenCompleted += callback;

            return this;
        }

        /// <summary>
        /// Set the method that is called once the tween reaches a point in time
        /// </summary>
        /// <param name="t">0-1</param>
        /// <param name="callback">method</param>
        /// <returns></returns>
        public TweenCase OnTimeReached(float t, SimpleCallback callback)
        {
            if (callbackData == null) callbackData = new List<CallbackData>();

            callbackData.Add(new CallbackData(t, callback));

            return this;
        }

        public void InvokeCompleteEvent()
        {
            tweenCompleted?.Invoke();
        }

        public abstract void Invoke(float deltaTime);
        public abstract void DefaultComplete();

        private class CallbackData
        {
            public SimpleCallback callback;
            public float t;

            public CallbackData(float t, SimpleCallback callback)
            {
                this.t = t;
                this.callback = callback;
            }
        }
    }
}
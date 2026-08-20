using System;

namespace NebulaSoft
{
    public abstract class LoadingTask
    {
        public bool IsActive { get; private set; }
        public bool IsFinished { get; private set; }

        public CompleteStatus Status { get; private set; }

        public event Action<CompleteStatus> OnTaskCompleted;

        public LoadingTask()
        {
            IsActive = false;
            IsFinished = false;
        }

        public void CompleteTask(CompleteStatus status)
        {
            if (IsFinished) return;

            Status = status;
            IsFinished = true;

            OnTaskCompleted?.Invoke(status);
        }

        public void Activate()
        {
            IsActive = true;

            try
            {
                OnTaskActivated();
            }
            catch
            {
                CompleteTask(CompleteStatus.Failed);
            }
        }

        public abstract void OnTaskActivated();

        public enum CompleteStatus { Skipped, Completed, Failed }
    }
}

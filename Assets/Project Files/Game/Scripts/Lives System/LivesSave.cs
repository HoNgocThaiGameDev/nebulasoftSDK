using System;

namespace NebulaSoft
{
    [System.Serializable]
    public class LivesSave : ISaveObject
    {
        public int LivesCount = -1;
        public int MaxLivesCount = -1;

        public bool LifeLocked = false;
        public long NewLifeDateBinary;

        public bool InfiniteLives = false;
        public long InfiniteLivesDateBinary;

        [NonSerialized] LivesStatus status;

        public void Init(LivesStatus status)
        {
            this.status = status;
        }

        public void Flush()
        {
            if (status == null) return;

            LivesCount = status.LivesCount;

            InfiniteLives = status.InfiniteMode;
            InfiniteLivesDateBinary = status.InfiniteModeDate.ToBinary();

            if(status.NewLifeTimerEnabled)
            {
                NewLifeDateBinary = status.NewLifeDate.ToBinary();
            }
            else
            {
                NewLifeDateBinary = (DateTime.Now + LivesSystem.OneLifeSpan).ToBinary();
            }
        }
    }
}
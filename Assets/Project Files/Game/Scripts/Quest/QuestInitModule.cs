using UnityEngine;

namespace NebulaSoft
{
    [RegisterModule("Quest", core: false)]
    public sealed class QuestInitModule : InitModule
    {
        public override string ModuleName => "Quest";

        [SerializeField] QuestDatabase questDatabase;
        public QuestDatabase Database => questDatabase;

        public override void CreateComponent()
        {
            QuestService.Init(questDatabase);
        }
    }
}

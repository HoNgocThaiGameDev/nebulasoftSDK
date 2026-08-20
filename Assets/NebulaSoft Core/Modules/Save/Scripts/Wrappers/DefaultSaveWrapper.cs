namespace NebulaSoft
{
    public sealed class DefaultSaveWrapper : BaseSaveWrapper
    {
        public override GlobalSave Load(string fileName)
        {
            return Serializer.Deserialize<GlobalSave>(fileName, logIfFileNotExists: false);
        }

        public override void Save(GlobalSave globalSave, string fileName)
        {
            Serializer.Serialize(globalSave, fileName);
        }

        public override void Delete(string fileName)
        {
            Serializer.DeleteFileAtPDP(fileName);
        }

        public override bool UseThreads()
        {
            return true;
        }
    }
}

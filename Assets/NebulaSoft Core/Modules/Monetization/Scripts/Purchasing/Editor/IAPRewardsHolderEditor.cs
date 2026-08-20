using UnityEditor;

namespace NebulaSoft
{
    [CustomEditor(typeof(IAPRewardsHolder), true)]
    public class IAPRewardsHolderEditor : RewardsHolderEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            UpdateRewardsSet();
        }

        protected override void OnSettingsPropertyChanges()
        {
            UpdateRewardsSet();
        }

        private void UpdateRewardsSet()
        {
            SerializedProperty iapTypeProperty = serializedObject.FindProperty("productKey");
            if (iapTypeProperty != null)
            {
                ProductKeyType type = (ProductKeyType)iapTypeProperty.intValue;

                IAPSettings settings = EditorUtils.GetAsset<IAPSettings>();
                if (settings != null)
                {
                    IAPItem[] storeItems = settings.StoreItems;
                    foreach (IAPItem item in storeItems)
                    {
                        if (item == null) continue;
                        if (item.ProductKeyType == type)
                        {
                            if(rewardSetProp.objectReferenceValue != item.RewardsSet)
                            {
                                int choice = 1;
                                if(rewardsViewProp.arraySize > 0)
                                {
                                    choice = EditorUtility.DisplayDialogComplex(
                                        "Rewards Update",
                                        "You are assigning a new Reward Set. What would you like to do with the current Reward Views list?",
                                        "Clear and repopulate",   // 0
                                        "Keep existing",          // 1
                                        "Cancel"                  // 2
                                    );
                                }

                                // Cancel
                                if(choice == 2)
                                    return;

                                rewardSetProp.objectReferenceValue = item.RewardsSet;

                                // Clear
                                if (choice == 0)
                                    rewardsViewProp.arraySize = 0;

                                serializedObject.ApplyModifiedProperties();
                                serializedObject.Update();

                                RevalidateViewsAgainstRewardSet();
                                AutoPopulateFromRewardSet();

                                Repaint();

                                EditorUtility.SetDirty(target);
                            }

                            break;
                        }
                    }
                }
            }
        }

        protected override bool DisableRewardsSetProperty() => true;
    }
}

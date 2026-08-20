using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if FIREBASE
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
#endif

#if FACEBOOK
using Facebook.Unity;
#endif

namespace NebulaSoft
{
    public enum FirebaseFacebookSignInStatus
    {
        Success,
        NeedsConflictResolution,
        Cancelled,
        Failed
    }

    public sealed class FirebaseFacebookSignInResult
    {
        public FirebaseFacebookSignInStatus Status;
        public string UserId;
        public bool LinkedAnonymousUser;
        public string Error;

        public bool IsSignedIn
        {
            get
            {
                return Status == FirebaseFacebookSignInStatus.Success
                    || Status == FirebaseFacebookSignInStatus.NeedsConflictResolution;
            }
        }
    }

    public static class FirebaseAuthHandler
    {
#if FIREBASE
        public static FirebaseAuth Auth { get; private set; }
        public static FirebaseFirestore Firestore { get; private set; }
#endif

        private static bool initialized;
        private static bool dependencyAvailable;
        private static int sessionGeneration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sessionGeneration++;
            initialized = false;
            dependencyAvailable = false;
#if FIREBASE
            Auth = null;
            Firestore = null;
#endif
        }

        public static bool IsReady
        {
            get
            {
#if FIREBASE
                return initialized && dependencyAvailable && Auth != null && Firestore != null;
#else
                return false;
#endif
            }
        }

        public static string CurrentUserId
        {
            get
            {
#if FIREBASE
                return Auth != null && Auth.CurrentUser != null ? Auth.CurrentUser.UserId : null;
#else
                return null;
#endif
            }
        }

        public static bool IsCurrentUserAnonymous
        {
            get
            {
#if FIREBASE
                return Auth != null && Auth.CurrentUser != null && Auth.CurrentUser.IsAnonymous;
#else
                return true;
#endif
            }
        }

        public static bool IsCurrentFacebookUser(string uid)
        {
#if FIREBASE
            return Auth != null
                && Auth.CurrentUser != null
                && !Auth.CurrentUser.IsAnonymous
                && !string.IsNullOrWhiteSpace(uid)
                && string.Equals(Auth.CurrentUser.UserId, uid.Trim(), System.StringComparison.Ordinal);
#else
            return false;
#endif
        }

        public static bool IsFacebookSyncSignedIn
        {
            get
            {
#if FIREBASE
                if (Auth != null && Auth.CurrentUser != null)
                    return !Auth.CurrentUser.IsAnonymous;
#endif

                if (SaveController.IsSaveLoaded)
                {
                    PlayerProfileSave save = SaveController.GetSaveObject<PlayerProfileSave>("Player Profile Save");
                    if (save != null && !string.IsNullOrWhiteSpace(save.FirebaseUid))
                        return true;
                }

                return false;
            }
        }

        public static async Task<bool> EnsureInitializedAsync()
        {
#if FIREBASE
            int runGeneration = sessionGeneration;
            if (initialized)
                return dependencyAvailable;

            try
            {
                DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (runGeneration != sessionGeneration)
                    return false;

                dependencyAvailable = status == DependencyStatus.Available;
                if (!dependencyAvailable)
                {
                    Debug.LogWarning("[Firebase] Dependencies are not available: " + status);
                    initialized = true;
                    return false;
                }

                Auth = FirebaseAuth.DefaultInstance;
                Firestore = FirebaseFirestore.DefaultInstance;
                initialized = true;
                return true;
            }
            catch (System.Exception exception)
            {
                if (runGeneration != sessionGeneration)
                    return false;

                Debug.LogWarning("[Firebase] Initialization failed: " + exception.Message);
                initialized = true;
                dependencyAvailable = false;
                return false;
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public static async Task<bool> EnsureSignedInAsync()
        {
#if FIREBASE
            int runGeneration = sessionGeneration;
            if (!await EnsureInitializedAsync() || runGeneration != sessionGeneration)
                return false;

            FirebaseAuth auth = Auth;
            if (auth == null)
                return false;

            if (auth.CurrentUser == null)
                await auth.SignInAnonymouslyAsync();

            if (runGeneration != sessionGeneration || Auth != auth || auth.CurrentUser == null)
                return false;

            User.SetCustomId(auth.CurrentUser.UserId);
            FirebaseNoAdsEntitlementSync.RestoreForCurrentUser();
            return true;
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public static async Task<FirebaseFacebookSignInResult> SignInWithFacebookAsync()
        {
            int runGeneration = sessionGeneration;
            Debug.Log("[Facebook] Requesting an access token.");
            string accessToken = await RequestFacebookAccessTokenAsync();
            if (runGeneration != sessionGeneration)
            {
                return new FirebaseFacebookSignInResult
                {
                    Status = FirebaseFacebookSignInStatus.Failed,
                    Error = "The authentication session changed."
                };
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogWarning("[Facebook] No access token returned. Login was cancelled or failed before Firebase authentication.");
                return new FirebaseFacebookSignInResult
                {
                    Status = FirebaseFacebookSignInStatus.Cancelled,
                    Error = "Facebook login was cancelled or no access token was returned."
                };
            }

            return await SignInWithFacebookAsync(accessToken);
        }

        public static async Task<FirebaseFacebookSignInResult> SignInWithFacebookAsync(string accessToken)
        {
#if FIREBASE
            int runGeneration = sessionGeneration;
            FirebaseFacebookSignInResult result = new FirebaseFacebookSignInResult
            {
                Status = FirebaseFacebookSignInStatus.Failed
            };

            if (string.IsNullOrEmpty(accessToken))
            {
                result.Error = "Facebook access token is empty.";
                return result;
            }

            try
            {
                Debug.Log("[Firebase] Starting Facebook credential authentication.");
                if (!await EnsureInitializedAsync() || runGeneration != sessionGeneration)
                {
                    result.Error = "Firebase is not initialized.";
                    Debug.LogWarning("[Firebase] Facebook authentication stopped because Firebase initialization failed.");
                    return result;
                }

                FirebaseAuth auth = Auth;
                if (auth == null)
                {
                    result.Error = "Firebase Auth is unavailable.";
                    return result;
                }

                Credential credential = FacebookAuthProvider.GetCredential(accessToken);
                bool hadAnonymousUser = auth.CurrentUser != null && auth.CurrentUser.IsAnonymous;
                Debug.Log("[Firebase] Current user before Facebook authentication: "
                    + (auth.CurrentUser != null ? auth.CurrentUser.UserId : "none")
                    + ", anonymous=" + hadAnonymousUser + ".");

                if (auth.CurrentUser != null)
                {
                    try
                    {
                        FirebaseUser currentUser = auth.CurrentUser;
                        AuthResult linkResult = await currentUser.LinkWithCredentialAsync(credential);
                        if (runGeneration != sessionGeneration || Auth != auth)
                        {
                            result.Error = "The authentication session changed.";
                            return result;
                        }

                        FirebaseUser linkedUser = linkResult != null ? linkResult.User : auth.CurrentUser;
                        ApplySignedInUser(linkedUser);
                        result.Status = FirebaseFacebookSignInStatus.Success;
                        result.UserId = linkedUser != null ? linkedUser.UserId : CurrentUserId;
                        result.LinkedAnonymousUser = hadAnonymousUser;
                        MirrorCurrentUserToLocalSave();
                        Debug.Log("[Firebase] Facebook credential linked successfully. UID=" + result.UserId + ".");
                        return result;
                    }
                    catch (System.Exception exception)
                    {
                        if (runGeneration != sessionGeneration || Auth != auth)
                        {
                            result.Error = "The authentication session changed.";
                            return result;
                        }

                        if (!IsCredentialAlreadyInUse(exception))
                            throw;

                        Debug.LogWarning("[Firebase] Facebook credential belongs to an existing Firebase user. Signing in to that account. Link error type="
                            + exception.GetType().Name + ", message=" + exception.Message + ".");
                    }
                }

                FirebaseUser signedInUser = await auth.SignInWithCredentialAsync(credential);
                if (runGeneration != sessionGeneration || Auth != auth)
                {
                    result.Error = "The authentication session changed.";
                    return result;
                }

                ApplySignedInUser(signedInUser);
                result.Status = hadAnonymousUser
                    ? FirebaseFacebookSignInStatus.NeedsConflictResolution
                    : FirebaseFacebookSignInStatus.Success;
                result.UserId = signedInUser != null ? signedInUser.UserId : CurrentUserId;
                result.LinkedAnonymousUser = false;
                MirrorCurrentUserToLocalSave();
                Debug.Log("[Firebase] Facebook sign-in completed. UID=" + result.UserId
                    + ", status=" + result.Status + ".");
                return result;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Firebase] Facebook sign-in failed: " + exception.Message);
                result.Status = FirebaseFacebookSignInStatus.Failed;
                result.Error = exception.Message;
                return result;
            }
#else
            await Task.CompletedTask;
            return new FirebaseFacebookSignInResult
            {
                Status = FirebaseFacebookSignInStatus.Failed,
                Error = "Firebase is not enabled."
            };
#endif
        }

        public static async Task<bool> SignOutFacebookSyncAsync()
        {
            bool success = true;
            bool firebaseAccountDetached = false;

            await CoinSafeCloudSync.FlushActiveFacebookAsync(CoinSafeCloudSync.LogoutFlushTimeout);

#if FACEBOOK
            try
            {
                if (FB.IsInitialized && FB.IsLoggedIn)
                {
                    FB.LogOut();
                    Debug.Log("[Facebook] Logged out from Facebook SDK.");
                }
            }
            catch (System.Exception exception)
            {
                success = false;
                Debug.LogWarning("[Facebook] Logout failed: " + exception.Message);
            }
#endif

#if FIREBASE
            try
            {
                if (!await EnsureInitializedAsync() || Auth == null)
                {
                    success = false;
                    Debug.LogWarning("[Firebase] Facebook sync sign-out could not verify the current Firebase session.");
                }
                else
                {
                    Auth.SignOut();
                    firebaseAccountDetached = true;
                    Debug.Log("[Firebase] Signed out from Facebook-linked Firebase user.");

                    await Auth.SignInAnonymouslyAsync();
                    FirebaseUser anonymousUser = Auth.CurrentUser;
                    if (anonymousUser == null || !anonymousUser.IsAnonymous)
                    {
                        success = false;
                        firebaseAccountDetached = anonymousUser == null;
                        Debug.LogWarning("[Firebase] Anonymous session was not established after Facebook sign-out.");
                    }
                    else
                    {
                        ApplySignedInUser(anonymousUser);
                        Debug.Log("[Firebase] Signed in anonymously after Facebook sign-out. UID="
                            + anonymousUser.UserId + ".");
                    }
                }
            }
            catch (System.Exception exception)
            {
                success = false;
                firebaseAccountDetached = Auth == null
                    || Auth.CurrentUser == null
                    || Auth.CurrentUser.IsAnonymous;
                Debug.LogWarning("[Firebase] Facebook sync sign-out failed: " + exception.Message);
            }
#else
            await Task.CompletedTask;
            firebaseAccountDetached = true;
#endif

            if (firebaseAccountDetached)
            {
                ClearFacebookSyncFromLocalSave();
            }
            else
            {
                success = false;
                Debug.LogWarning("[Firebase] Keeping the current local progress active because Firebase sign-out was not confirmed.");
            }

            return success && firebaseAccountDetached;
        }

        private static async Task<string> RequestFacebookAccessTokenAsync()
        {
#if FACEBOOK
            TaskCompletionSource<string> tokenSource = new TaskCompletionSource<string>();

            System.Action login = () =>
            {
                Debug.Log("[Facebook] Opening Facebook login.");
                FB.LogInWithReadPermissions(new List<string> { "public_profile" }, result =>
                {
                    if (result == null)
                    {
                        Debug.LogWarning("[Facebook] Login callback returned no result.");
                        tokenSource.TrySetResult(null);
                        return;
                    }

                    if (result.Cancelled)
                    {
                        Debug.LogWarning("[Facebook] Login was cancelled by the user.");
                        tokenSource.TrySetResult(null);
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Debug.LogError("[Facebook] Login failed: " + result.Error);
                        tokenSource.TrySetResult(null);
                        return;
                    }

                    AccessToken token = AccessToken.CurrentAccessToken;
                    Debug.Log(token != null
                        ? "[Facebook] Access token received successfully."
                        : "[Facebook] Login succeeded but AccessToken.CurrentAccessToken is null.");
                    tokenSource.TrySetResult(token != null ? token.TokenString : null);
                });
            };

            if (FB.IsInitialized)
            {
                Debug.Log("[Facebook] SDK is already initialized.");
                login();
            }
            else
            {
                Debug.Log("[Facebook] Initializing SDK.");
                FB.Init(() =>
                {
                    if (!FB.IsInitialized)
                    {
                        Debug.LogError("[Facebook] SDK initialization callback completed, but FB.IsInitialized is false.");
                        tokenSource.TrySetResult(null);
                        return;
                    }

                    Debug.Log("[Facebook] SDK initialized successfully.");
                    login();
                }, isGameShown => { });
            }

            return await tokenSource.Task;
#else
            await Task.CompletedTask;
            Debug.LogWarning("[Facebook] SDK is not enabled. Import Facebook SDK for Unity and add the FACEBOOK scripting define.");
            return null;
#endif
        }

#if FIREBASE
        private static bool IsCredentialAlreadyInUse(System.Exception exception)
        {
            if (exception == null)
                return false;

            FirebaseException firebaseException = exception as FirebaseException;
            if (firebaseException != null && firebaseException.ErrorCode == (int)AuthError.CredentialAlreadyInUse)
                return true;

            string message = exception.Message ?? string.Empty;
            return message.IndexOf("already", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("associated with a different user account", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("credential is already associated", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("credential-already-in-use", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplySignedInUser(FirebaseUser user)
        {
            if (user != null)
            {
                User.SetCustomId(user.UserId);
                FirebaseNoAdsEntitlementSync.RestoreForCurrentUser();
            }
        }
#endif

        public static void MirrorCurrentUserToLocalSave()
        {
#if FIREBASE
            if (Auth == null || Auth.CurrentUser == null || !SaveController.IsSaveLoaded)
                return;

            PlayerProfileSave save = SaveController.GetSaveObject<PlayerProfileSave>("Player Profile Save");
            User.SetCustomId(Auth.CurrentUser.UserId);
            save.FirebaseUid = Auth.CurrentUser.IsAnonymous ? null : Auth.CurrentUser.UserId;
            SaveController.MarkAsSaveIsRequired();
#endif
        }

        private static void ClearFacebookSyncFromLocalSave()
        {
            if (!SaveController.IsSaveLoaded)
                return;

            PlayerProfileSave save = SaveController.GetSaveObject<PlayerProfileSave>("Player Profile Save");
            if (save == null)
                return;

            save.FirebaseUid = null;
            SaveController.MarkAsSaveIsRequired();
            Debug.Log("[Profile] Facebook sync UID cleared from local save.");
        }
    }
}

namespace GokuTrap
{
    public sealed class AccountProfileManager
    {
        public long? AuthenticatedUserId { get; private set; }
        public AccountProfile? ActiveProfile => AuthenticatedUserId is long id
            ? App.Settings.Prop.AccountProfiles.FirstOrDefault(x => x.RobloxUserId == id) : null;
        public string Status { get; private set; } = FeatureText.Get("AccountNotValidated");
        public void InvalidateSession()
        {
            AuthenticatedUserId = null;
            foreach (var profile in App.Settings.Prop.AccountProfiles) profile.ValidationStatus = "NotValidated";
            Status = FeatureText.Get("AccountNotValidated");
        }

        public async Task<AccountProfile?> AddCurrentAuthenticatedAsync()
        {
            await ValidateSessionAsync();
            if (AuthenticatedUserId is not long id) return null;
            AccountProfile? existing = App.Settings.Prop.AccountProfiles.FirstOrDefault(x => x.RobloxUserId == id);
            AccountProfile profile = existing ?? new AccountProfile { RobloxUserId = id };
            await RefreshAsync(profile);
            profile.LastValidatedUtc = DateTimeOffset.UtcNow;
            profile.ValidationStatus = "Authenticated";
            if (existing is null) App.Settings.Prop.AccountProfiles.Add(profile);
            Select(profile);
            return profile;
        }

        public async Task ValidateSessionAsync()
        {
            InvalidateSession();
            if (!App.Settings.Prop.AllowCookieAccess)
            {
                App.Cookies.Clear();
                Status = FeatureText.Get("CookieOptInRequired");
                return;
            }
            await App.Cookies.LoadCookies(forceRefresh: true);
            AuthenticatedUser? user = App.Cookies.Loaded ? await App.Cookies.GetAuthenticated() : null;
            if (user is null || user.Id <= 0)
            {
                Status = FeatureText.Get("SessionExpired");
                App.Settings.Prop.Overlay.Enabled = false;
                App.Settings.Save();
                App.Overlay.Dispose();
                return;
            }
            AuthenticatedUserId = user.Id;
            var matched = App.Settings.Prop.AccountProfiles.FirstOrDefault(x => x.RobloxUserId == user.Id);
            if (matched is not null)
            {
                matched.Username = user.Username;
                matched.DisplayName = user.Displayname;
                matched.ValidationStatus = "Authenticated";
                matched.LastValidatedUtc = DateTimeOffset.UtcNow;
            }
            Status = string.Format(FeatureText.Get("SessionValidated"), user.Displayname, user.Id);
        }

        public async Task<bool> RefreshAsync(AccountProfile profile)
        {
            try
            {
                UserDetails details = await UserDetails.Fetch(profile.RobloxUserId);
                profile.Username = details.Data.Name;
                profile.DisplayName = details.Data.DisplayName;
                string? url = details.Thumbnail.ImageUrl;
                profile.AvatarThumbnailUrl = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                    uri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase) ? url! : string.Empty;
                // A public profile lookup is not evidence of a valid session.
                return true;
            }
            catch (Exception ex)
            {
                profile.ValidationStatus = "Unavailable";
                Status = FeatureText.Get("AccountUnavailable");
                App.Logger.WriteLine("Accounts", "Public profile refresh failed: " + ex.GetType().Name);
                return false;
            }
        }

        public void Select(AccountProfile? profile) => App.Settings.Prop.SelectedAccountProfileId = profile?.Id;
        public void Remove(AccountProfile profile)
        {
            App.Settings.Prop.AccountProfiles.Remove(profile);
            if (App.Settings.Prop.SelectedAccountProfileId == profile.Id) Select(null);
        }
        public void BeginOfficialSignIn() => Utilities.ShellExecute("https://www.roblox.com/login");
    }
}

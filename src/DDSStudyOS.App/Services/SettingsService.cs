using Windows.Storage;

namespace DDSStudyOS.App.Services;

public static class SettingsService
{
    private const string KeyDownloadsOrganizerEnabled = "DownloadsOrganizerEnabled";

    public static bool DownloadsOrganizerEnabled
    {
        get
        {
            var v = ApplicationData.Current.LocalSettings.Values[KeyDownloadsOrganizerEnabled];
            return v is null ? true : (bool)v;
        }
        set
        {
            ApplicationData.Current.LocalSettings.Values[KeyDownloadsOrganizerEnabled] = value;
        }
    }
}

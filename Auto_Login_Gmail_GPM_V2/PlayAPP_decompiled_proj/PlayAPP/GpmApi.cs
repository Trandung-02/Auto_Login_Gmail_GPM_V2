using System;

namespace PlayAPP;

/// <summary>Đường dẫn cố định GPM Login API (cổng 19995).</summary>
public static class GpmApi
{
	public const string BaseUrl = "http://127.0.0.1:19995";

	public static string GroupsEndpoint => BaseUrl + "/api/v3/groups";

	public static string ProfilesListUrl(string groupIdOrNull, int sort = 2, int perPage = 500)
	{
		if (string.IsNullOrEmpty(groupIdOrNull))
		{
			return $"{BaseUrl}/api/v3/profiles?sort={sort}&per_page={perPage}";
		}
		return $"{BaseUrl}/api/v3/profiles?group_id={Uri.EscapeDataString(groupIdOrNull)}&sort={sort}&per_page={perPage}";
	}

	public static string ProfileStartPath(string profileId) => "/api/v3/profiles/start/" + Uri.EscapeDataString(profileId);

	public static string ProfileStartUrl(string profileId) => BaseUrl + ProfileStartPath(profileId);

	public static string ProfileCloseUrl(string profileId) => BaseUrl + "/api/v3/profiles/close/" + Uri.EscapeDataString(profileId);

	public static string ProfileDeleteUrl(string profileId, int mode = 2) => BaseUrl + "/api/v3/profiles/delete/" + Uri.EscapeDataString(profileId) + "?mode=" + mode;

	public static string ProfileUpdateUrl(string profileId) => BaseUrl + "/api/v3/profiles/update/" + Uri.EscapeDataString(profileId);
}

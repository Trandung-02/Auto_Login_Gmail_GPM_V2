using Xunit;

namespace PlayAPP.Tests;

public class GpmApiTests
{
	[Fact]
	public void GroupsEndpoint_UsesBaseAndPath()
	{
		Assert.StartsWith(GpmApi.BaseUrl, GpmApi.GroupsEndpoint, StringComparison.Ordinal);
		Assert.Contains("/api/v3/groups", GpmApi.GroupsEndpoint, StringComparison.Ordinal);
	}

	[Fact]
	public void ProfilesListUrl_WithoutGroup_HasSortAndPerPage()
	{
		string u = GpmApi.ProfilesListUrl(null);
		Assert.Contains("sort=2", u, StringComparison.Ordinal);
		Assert.Contains("per_page=500", u, StringComparison.Ordinal);
		Assert.DoesNotContain("group_id=", u, StringComparison.Ordinal);
	}

	[Fact]
	public void ProfilesListUrl_WithGroup_EncodesId()
	{
		string u = GpmApi.ProfilesListUrl("22");
		Assert.Contains("group_id=22", u, StringComparison.Ordinal);
	}

	[Fact]
	public void ProfileStartUrl_EncodesProfileId()
	{
		string u = GpmApi.ProfileStartUrl("abc/def");
		Assert.Contains(Uri.EscapeDataString("abc/def"), u, StringComparison.Ordinal);
	}
}

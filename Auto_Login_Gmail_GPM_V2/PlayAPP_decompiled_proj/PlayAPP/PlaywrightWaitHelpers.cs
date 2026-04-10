using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlayAPP;

/// <summary>Chờ trên Playwright theo lát nhỏ để CancellationToken batch có hiệu lực giữa các bước.</summary>
internal static class PlaywrightWaitHelpers
{
	public static async Task PageWaitAsync(IPage page, float totalMs, CancellationToken cancellationToken = default, int sliceMs = 250)
	{
		if (page == null || totalMs <= 0f)
		{
			return;
		}
		int slice = Math.Clamp(sliceMs, 50, 2000);
		int remaining = (int)System.Math.Ceiling((double)totalMs);
		while (remaining > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int step = System.Math.Min(slice, remaining);
			await page.WaitForTimeoutAsync(step);
			remaining -= step;
		}
	}
}

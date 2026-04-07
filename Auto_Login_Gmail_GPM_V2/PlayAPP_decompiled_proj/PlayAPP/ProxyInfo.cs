namespace PlayAPP;

public class ProxyInfo
{
	public string Server { get; set; }

	public string Username { get; set; }

	public string Password { get; set; }

	/// <summary>Dòng gốc host:port hoặc host:port:user:pass — gửi GPM raw_proxy.</summary>
	public string RawLineForGpm { get; set; }
}

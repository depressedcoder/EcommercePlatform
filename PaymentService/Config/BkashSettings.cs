namespace PaymentService.Config;

public class BkashSettings
{
    public string BaseUrl { get; set; } = null!;
    public string GrantTokenUrl { get; set; } = null!;
    public string CreatePaymentUrl { get; set; } = null!;
    public string ExecutePaymentUrl { get; set; } = null!;
    public string AppKey { get; set; } = null!;
    public string AppSecret { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

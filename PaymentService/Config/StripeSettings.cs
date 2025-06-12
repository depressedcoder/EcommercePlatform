namespace PaymentService.Config;

public class StripeSettings
{
    public string SecretKey { get; set; } = default!;
    public string PublishableKey { get; set; } = default!;
}

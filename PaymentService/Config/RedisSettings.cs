namespace PaymentService.Config;
 
public class RedisSettings
{
    public string InstanceName { get; set; } = string.Empty;
    public int DefaultExpirationMinutes { get; set; }
} 
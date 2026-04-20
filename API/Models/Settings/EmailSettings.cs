namespace API.Models.Settings
{
    /// <summary>Bound from configuration section EmailSettings.</summary>
    public class EmailSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? From { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>StartTls (587), SslOnConnect (465), or Auto.</summary>
        public string? SmtpSslMode { get; set; }

        public string? FarmerPortalBaseUrl { get; set; }

        /// <summary>When true, SMTP is skipped but flows still succeed (optional).</summary>
        public bool Disabled { get; set; }
    }
} 
 
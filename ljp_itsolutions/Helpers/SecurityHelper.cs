namespace ljp_itsolutions.Helpers
{
    public static class SecurityHelper
    {
        public static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return email;

            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 2)
                return name[0] + "****@" + domain;

            return name.Substring(0, 2) + "****@" + domain;
        }

        public static string MaskIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return "Unknown";
            var parts = ip.Split('.');
            if (parts.Length != 4) return ip; // Return as is if IPv6/other

            return $"{parts[0]}.{parts[1]}.***.***";
        }
    }
}

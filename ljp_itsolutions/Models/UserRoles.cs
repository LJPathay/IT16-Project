namespace ljp_itsolutions.Models
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string SuperAdmin = "SuperAdmin";
        public const string Manager = "Manager";
        public const string Cashier = "Cashier";
        public const string MarketingStaff = "MarketingStaff";

        public static readonly List<string> AllExceptSuper = new() { Admin, Manager, Cashier, MarketingStaff };
    }
}

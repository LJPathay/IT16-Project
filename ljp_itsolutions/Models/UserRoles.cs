namespace ljp_itsolutions.Models
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string SuperAdmin = "SuperAdmin";
        public const string Manager = "Manager";
        public const string Cashier = "Cashier";
        public const string MarketingStaff = "MarketingStaff";
        //test
        public static readonly IReadOnlyList<string> AllExceptSuper = new List<string> { Admin, Manager, Cashier, MarketingStaff }.AsReadOnly();
    }
}

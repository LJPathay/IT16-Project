namespace ljp_itsolutions.Models
{
    public static class AppConstants
    {
        public static class Actions
        {
            public const string Dashboard = "Dashboard";
            public const string Index = "Index";
            public const string Login = "Login";
            public const string VerifyMfa = "VerifyMfa";
            public const string Error = "Error";
            public const string Shared = "Shared";
            public const string Home = "Home";
            public const string Account = "Account";
            public const string Inventory = "Inventory";
            public const string Products = "Products";
            public const string Transactions = "Transactions";
            public const string ShiftManagement = "ShiftManagement";
            public const string Users = "Users";
            public const string SystemSettings = "SystemSettings";
            public const string Reports_Cashier = "Reports_Cashier";
            public const string InventoryOverview = "InventoryOverview";
            public const string Backups = "Backups";
            public const string AuditLogs = "AuditLogs";
            public const string SecurityLogs = "SecurityLogs";
        }

        public static class SessionKeys
        {
            public const string UserRole = "UserRole";
            public const string Username = "Username";
            public const string MfaUserId = "MfaUserId";
            public const string Message = "Message";
            public const string SuccessMessage = "SuccessMessage";
            public const string ErrorMessage = "ErrorMessage";
            public const string TempDataInfo = "Message";
        }

        public static class Controllers
        {
            public const string Account = "Account";
            public const string Home = "Home";
            public const string SuperAdmin = "SuperAdmin";
            public const string Admin = "Admin";
            public const string Manager = "Manager";
            public const string Marketing = "Marketing";
            public const string POS = "Pos";
            public const string Cashier = "Cashier";
        }
    }
}

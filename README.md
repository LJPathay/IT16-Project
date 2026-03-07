## COFFEE SHOP ERP SYSTEM
# This project is developed for the subject IT15 – Integrative Programming and Technologies.
# LJP IT Solutions: A Web-Based Coffee Shop ERP System with Customer Engagement and Marketing Analytics.

# The system is designed to manage coffee shop operations including:
  - User and role management
  - Product and category management
  - Order and payment processing
  - Inventory monitoring
  - Promotions and loyalty system
  - Marketing analytics and reports
  - Audit logging and archiving

The system follows role-based access control to ensure secure and authorized usage.

# HOW TO USE

git clone https://github.com/LJPathay/ERP_LJP_ITSOLUTIONS.git
cd ljp_itsolutions
create appsetings.json and appsettings.Development.json

add connection string to appsettings.json
      e.g
      "ConnectionStrings": {
        "DefaultConnection": "input your connection string here"
      }
add cloudinary api and web name to appsettings.json
      e.g
      "CloudinarySettings": {
        "CloudName": "input your cloud name here",
        "ApiKey": "input your api key here",
        "ApiSecret": "input your api secret here"
      }
add paymongo secret API and webhook api to appsettings.json
      e.g
      "PaymongoSettings": {
        "SecretKey": "input your secret key here",
        "WebhookSecret": "input your webhook secret here"
      }

dotnet ef database update --project ljp_itsolutions.csproj
dotnet run

# ROLES
 - Super Admin
 - Admin
 - Manager
 - Cashier
 - Marketing Staff

 




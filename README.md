# ☕ LJP IT Solutions: Coffee Shop ERP System
### *Integrative Programming and Technologies (IT16)*

[![Security Level](https://img.shields.io/badge/Security-Information%20Assurance%20Grade-brightgreen)](https://github.com/LJPathay/IT16-Project)
[![Code Quality](https://img.shields.io/badge/SonarQube-A%20Rating-success)](https://github.com/LJPathay/IT16-Project)
[![Language](https://img.shields.io/badge/Stack-ASP.NET%20Core%20MVC%20%7C%20EF%20Core%20%7C%20SQL%20Server-blue)](https://github.com/LJPathay/IT16-Project)

**LJP IT Solutions** is a robust, web-based Enterprise Resource Planning (ERP) system designed specifically for the modern coffee shop industry. It integrates real-time inventory management, secure financial transactions, and advanced marketing analytics into a unified, high-security dashboard.

---

## 🛡️ Truthful Security Architecture & Hardening
*This system has been hardened following strict Information Assurance and Security standards.*

### 🔐 1. Identity & Access Management (IAM)
- **Role-Based Access Control (RBAC)**: Distinct permissions for SuperAdmin, Admin, Manager, Cashier, and Marketing Staff.
- **Cryptographic Hardening**: Passwords are mathematically shielded using PBKDF2 with **100,000 algorithmic iterations**.
- **Adaptive Brute-Force Immunity**: Failed login attempts trigger automatic tier escalation lockouts, leading up to a Permanent Block (100-year timeout) requiring administrative unblocking and clearance at 15 attempts.
- **Zero-Knowledge Enumeration**: The "Forgot Password" algorithms are built to prevent attackers from determining if an email or username exists in the system.
- **Strict Password Integrity**: Enforced minimum 16-character complexity (Upper, Lower, Numeric, Special) along with **Password History Limitation** to verify users aren't reusing old passwords.
- **Multi-Factor Authentication (MFA)**: Built-in TOTP verification for privileged logins.
- **Session Termination Policy**: Absolute 20-minute inactivity thresholds with dynamic session fixation-prevention explicitly built into the login process.

### 🧱 2. Perimeter & Payload Defense
- **Parameter Tampering Prevention**: The system enforces "Zero-Trust Client" principles. Pricing and data payloads sent from the frontend POS are ignored entirely in favor of strict, server-side database verification.
- **File Upload Mitigation**: Image uploads run through explicit perimeter checks: hard-capped at **5MB** limits, parsed for explicitly allowed extensions (`.jpg, .png, .jpeg, .webp`), and inspected for `image/` MIME constraints prior to ingestion.
- **Content Security Policy (CSP)**: Hardened headers to significantly mitigate DOM-based Cross-Site Scripting (XSS) and Content Injection. Includes `X-Frame-Options` for Click-Jacking protection.
- **CSRF Tokens**: All critical action forms natively implement ASP.NET Anti-Forgery cryptograms.
- **Google reCAPTCHA v2**: Deeply integrated into the login gateways.

### 🕵️ 3. Auditing & Compliance
- **Unified Security Logging**: Complete logging of all threat factors (Unauthorized route access, lockouts, parameter tampering).
- **Omnipresent Visitor Tracking**: Fully capable middleware identifying and logging all Anonymous and Authenticated users traversing routes, gathering their metadata structure.
- **Cryptographic Webhook Verification**: Ensures remote connections (like PayMongo) actually originate from the vendor via secure streaming HMAC byte verification.

---

## 📂 Project File Structure
The architecture leverages the Model-View-Controller (MVC) paradigm:

```text
ljp_itsolutions/
├── 📁 Controllers/        # Business logic & Route end-point handling (Account, Admin, Cashier, POS, etc.)
├── 📁 Models/             # Entity Framework database structures & ViewModel definitions
├── 📁 Views/              # Razor (.cshtml) HTML UI components separated by Roles
├── 📁 ViewComponents/     # Reusable modular UI elements (like Notifications)
├── 📁 Services/           # Independent infrastructure bindings (EmailSender, PhotoService, InventoryService)
├── 📁 Data/               # Entity Framework context (ApplicationDbContext) & Initializers
├── 📁 Helpers/            # Extension algorithms (SecurityHelper, Encryption logic)
├── 📁 Migrations/         # EF Core SQL Database History
├── 📁 Backups/            # Automated database snapshot storage
├── 📁 wwwroot/            # Static Assets (Images, centralized CSS, nonced JS scripts)
├── appsettings.json       # Secure System Configuration
└── Program.cs             # Application Entry point, Middleware configurations & Security pipelines
```

---

## 🚀 Core Features
- **POS & Operations**: Secure payment processing with **PayMongo** integration and digitized receipting.
- **Inventory Management**: Real-time stock alerts and ingredient-level recipe tracking.
- **Marketing Analytics**: Data-driven insights for customer engagement and campaign monitoring.
- **System Maintenance & Resilience**: Global automated backup policies (Configurable execution for Daily, Weekly, or Monthly snapshots) ensuring absolute data recovery assurance.

---

## 🛠️ Installation & Setup

### Prerequisites
- .NET 8.0 SDK / SQL Server
- Cloudinary & PayMongo API Keys

### Configuration
Update your `appsettings.json` with your private credentials:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_SQL_SERVER_CONNECTION_STRING"
  },
  "CloudinarySettings": {
    "CloudName": "...", "ApiKey": "...", "ApiSecret": "..."
  },
  "PayMongo": {
    "SecretKey": "...", "WebhookSecretKey": "..."
  },
  "ReCaptcha": {
    "SecretKey": "..."
  }
}
```

### Deployment
```bash
# Clone the repository
git clone https://github.com/LJPathay/IT16-Project.git

# Initialize Database
dotnet ef database update

# Run Application
dotnet run
```

---

## 📁 Submission Details
> [!IMPORTANT]
> **Project Defense Status**: Phase 3 Final Integration (Security Hardened)

**Prepared by:** Lebron James Pathay  
**Course & Section:** IT16 -- INFORMATION ASSURANCE AND SECURITY 1  
**Subject:**  IT16  


© 2026 LJP IT SOLUTIONS. All Rights Reserved.

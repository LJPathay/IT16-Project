# ☕ LJP IT Solutions: Coffee Shop ERP System
### *Integrative Programming and Technologies (IT16) 

[![Security Level](https://img.shields.io/badge/Security-Enterprise%20Grade-brightgreen)](https://github.com/LJPathay/IT16-Project)
[![Code Quality](https://img.shields.io/badge/SonarQube-Zero%20Vulnerabilities-success)](https://github.com/LJPathay/IT16-Project)
[![Language](https://img.shields.io/badge/Stack-ASP.NET%20Core%20%7C%20EF%20Core%20%7C%20SQL%20Server-blue)](https://github.com/LJPathay/IT16-Project)

**LJP IT Solutions** is a robust, web-based Enterprise Resource Planning (ERP) system designed specifically for the modern coffee shop industry. It integrates real-time inventory management, secure financial transactions, and advanced marketing analytics into a unified, high-security dashboard.

---

## 🛡️ Security Architecture & Hardening
*This system has been hardened following industry best practices for data protection and access control.*

### 🔐 1. Identity & Access Management (IAM)
- **Role-Based Access Control (RBAC)**: Distinct permissions for SuperAdmin, Admin, Manager, Cashier, and Marketing Staff.
- **Advanced Multi-Factor Authentication (MFA)**: Built-in TOTP verification (Google Authenticator/Microsoft Authenticator) for sensitive accounts.
- **Strict Password Policy**: Mandatory 16-character minimum complexity (Upper, Lower, Numeric, Special) with **Password History** to prevent reuse.
- **Session Protection**: 20-minute inactivity timeout with Session Fixation prevention.

### 🤖 2. Perimeter Defense
- **Google reCAPTCHA v2**: Integrated into the login gateway to mitigate brute-force and dictionary attacks.
- **Rate Limiting**: Intelligent IP-based throttling on all authentication endpoints.
- **Secure Headers**: Hardened **Content Security Policy (CSP)**, X-Frame-Options (Click-jacking protection), and HSTS.
- **Reverse Proxy Resiliency**: Dynamic `ForwardedHeaders` mapped exclusively to handle secure cloud edge proxies (runasp.net) without cyclic SSL redirect loops.

### 🕵️ 3. Auditing & Privacy
- **Real-Time Security Monitoring**: Automated logging of all security events (login attempts, PII access, role changes).
- **Unified Audit Interface**: Complete 1:1 design synchronization between Security Logs and Activity Tracking, utilizing streamlined avatar-based operator formatting for rapid threat identification and data consumption.
- **Visitor Tracking**: Comprehensive audit trail capturing IP addresses and User-Agents for accountability.
- **Data Masking (Privacy-by-Design)**: Redaction of sensitive PII (Emails, IP segments) in administrative dashboards to comply with privacy standards.

### 📈 4. Technical Debt & Code Quality
- **SonarQube Hardened**: Extirpated 'under-posting' vulnerabilities, enforced explicit Nullable type mapping, and slashed cognitive complexity across major controllers.
- **Performance Optimized**: Substituted legacy DOM manipulations with modern `globalThis` constraints and optimized backend LINQ operations (e.g., converting redundant `.Count()` structures to `.Any()`).

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

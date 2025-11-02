# 🏛️ Gov2Biz - Multi-Tenant License Management System

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://www.docker.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?logo=redis)](https://redis.io/)

> A production-ready, enterprise-grade multi-tenant license management system built with microservices architecture, featuring complete tenant isolation, JWT authentication, and automated payment processing.

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Architecture](#-architecture)
- [Technology Stack](#-technology-stack)
- [Development Philosophy](#-development-philosophy)
- [Quick Start](#-quick-start)
- [Features](#-features)
- [API Documentation](#-api-documentation)
- [Security](#-security)
- [Testing](#-testing)

---

## 🎯 Overview

**Gov2Biz** is a comprehensive license management platform designed for government agencies and businesses that need to manage licenses for multiple organizations (tenants). The system ensures complete data isolation between tenants while providing powerful administrative capabilities and seamless payment integration.

### Why Gov2Biz?

- **True Multi-Tenancy**: Complete data isolation at database level with row-level security
- **Microservices Architecture**: Loosely coupled services for scalability and maintainability
- **Enterprise Security**: JWT-based authentication with role-based access control (RBAC)
- **Payment Integration**: Razorpay integration with automated license activation
- **Production Ready**: Dockerized deployment with health checks and monitoring

## 🏗️ Architecture

Gov2Biz follows a **microservices architecture** with API Gateway pattern, ensuring scalability, maintainability, and fault isolation.

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Browser                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
                ┌────────────▼────────────┐
                │   MVC Frontend (5005)   │
                │  - Razor Views          │
                │  - Bootstrap 5 UI       │
                │  - JWT Token Management │
                └────────────┬────────────┘
                             │
                ┌────────────▼────────────┐
                │  API Gateway (5000)     │
                │  - Ocelot Gateway       │
                │  - Request Routing      │
                │  - Load Balancing       │
                └────────────┬────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
   ┌────▼─────┐      ┌──────▼──────┐     ┌──────▼──────┐
   │ License  │      │  Payment    │     │  Document   │
   │ Service  │      │  Service    │     │  Service    │
   │  (5001)  │      │  (5002)     │     │  (5003)     │
   └────┬─────┘      └──────┬──────┘     └──────┬──────┘
        │                   │                    │
        └───────────────────┼────────────────────┘
                            │
                   ┌────────▼─────────┐
                   │   SQL Server     │
                   │   - Multi-Tenant │
                   │   - Row Security │
                   └──────────────────┘
```

### Service Responsibilities

| Service | Port | Technology | Purpose |
|---------|------|------------|---------|
| **MVC Frontend** | 5005 | ASP.NET Core MVC 8.0 | User interface, session management |
| **API Gateway** | 5000 | Ocelot | Request routing, load balancing |
| **License Service** | 5001 | ASP.NET Core Web API | License CRUD, approval workflow |
| **Payment Service** | 5002 | ASP.NET Core Web API | Razorpay integration, payment processing |
| **Document Service** | 5003 | ASP.NET Core Web API | Document upload/download |
| **Notification Service** | 5004 | ASP.NET Core Web API | Email/SMS notifications (Hangfire) |
| **SQL Server** | 1433 | SQL Server 2022 | Relational database with multi-tenant support |
| **Redis** | 6379 | Redis 7 | Caching, Hangfire job storage |

---

## 💻 Technology Stack

### Backend Technologies

| Technology | Version | Usage | Key Features |
|------------|---------|-------|--------------|
| **.NET** | 8.0 | Core framework | Minimal APIs, dependency injection, middleware pipeline |
| **ASP.NET Core MVC** | 8.0 | Frontend | Razor views, tag helpers, model binding |
| **Entity Framework Core** | 8.0 | ORM | LINQ queries, migrations, change tracking |
| **Dapper** | 2.1+ | Micro-ORM | Stored procedure execution, performance optimization |
| **JWT Bearer** | 8.0 | Authentication | HS256 signing, claims-based identity |
| **Ocelot** | 23.0+ | API Gateway | Request routing, rate limiting, QoS |
| **Hangfire** | 1.8+ | Background Jobs | Recurring jobs, job scheduling, dashboard |
| **FluentValidation** | 11.0+ | Validation | Request validation, custom rules |

### Frontend Technologies

| Technology | Version | Usage |
|------------|---------|-------|
| **Bootstrap** | 5.3 | UI framework, responsive design |
| **jQuery** | 3.7 | DOM manipulation, AJAX calls |
| **SweetAlert2** | 11.0 | Modal dialogs, notifications |
| **DataTables** | 1.13 | Table sorting, pagination, search |

### Infrastructure

| Technology | Version | Usage |
|------------|---------|-------|
| **Docker** | 24.0+ | Containerization |
| **Docker Compose** | 2.20+ | Multi-container orchestration |
| **SQL Server** | 2022 | Relational database |
| **Redis** | 7.0 | In-memory cache, job storage |

### Payment & Integration

| Service | Usage |
|---------|-------|
| **Razorpay** | Payment gateway integration |

---

## 💡 Development Philosophy

### How This Project Was Built

This project was architected with a **design-first approach**, focusing on:

1. **Domain-Driven Design (DDD)**
   - Clear separation of concerns between services
   - Each microservice owns its domain logic
   - Shared kernel for common models (JWT, tenant context)

2. **Clean Architecture Principles**
   - Dependency inversion (interfaces over implementations)
   - Repository pattern for data access
   - Service layer for business logic
   - Controller layer for HTTP handling

3. **SOLID Principles**
   - **S**ingle Responsibility: Each service has one reason to change
   - **O**pen/Closed: Extensible without modifying existing code
   - **L**iskov Substitution: Interfaces are properly abstracted
   - **I**nterface Segregation: Small, focused interfaces
   - **D**ependency Inversion: Depend on abstractions, not concretions

4. **GitHub Copilot Integration**
   - Used for boilerplate code generation (DTOs, models, controllers)
   - Accelerated stored procedure creation with consistent patterns
   - Reduced repetitive code across microservices
   - Maintained code consistency through AI-assisted refactoring
   - **Time saved**: ~40% reduction in development time for CRUD operations

### .NET Core Principles Covered

| Principle | Implementation |
|-----------|----------------|
| **Dependency Injection** | Built-in DI container for all services |
| **Middleware Pipeline** | JWT authentication, CORS, exception handling |
| **Configuration Management** | appsettings.json with environment overrides |
| **Logging & Monitoring** | ILogger interface, structured logging |
| **Health Checks** | Endpoint health monitoring for each service |
| **Async/Await** | Asynchronous database operations throughout |
| **Action Filters** | Custom authorization filters for tenant isolation |
| **Model Binding** | Automatic request to model mapping |

---

## 🚀 Quick Start

### Prerequisites
- **Docker Desktop** installed and running
- **8GB RAM** minimum
- **Ports available**: 5000-5005, 1433, 6379
- **OS**: macOS/Linux (Windows users: use WSL2 or `start-windows.bat`)

### One-Command Startup

**macOS/Linux:**
\`\`\`bash
./start-mac.sh
\`\`\`

**Windows:**
\`\`\`cmd
start-windows.bat
\`\`\`

The startup script will:
1. ✅ Start all Docker containers (8 services)
2. ✅ Wait for SQL Server initialization
3. ✅ Automatically create database and schema
4. ✅ Run all stored procedures
5. ✅ Seed test data (users, license types, sample licenses)
6. ✅ Display access URLs and credentials

### Access the Application

🌐 **Frontend**: http://localhost:5005

**Default Credentials:**
- Admin: `admin@test.com` / `Password123!`
- User: `user@test.com` / `Password123!`

---

## 🔐 Security

### JWT Authentication Architecture

```
┌─────────────┐
│   Login     │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────┐
│  LicenseService generates JWT       │
│  - HS256 algorithm                  │
│  - Claims: userId, email, roles     │
│  - tenantId, expiration             │
└──────┬──────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  Token stored in:                   │
│  - Server Session (HttpOnly)        │
│  - localStorage (client-side APIs)  │
└──────┬──────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  Every API request includes:        │
│  Authorization: Bearer <token>      │
│  X-Tenant-ID: <tenantId>           │
│  X-User-Role: <role>               │
└──────┬──────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  Each microservice validates:       │
│  1. Token signature (shared secret) │
│  2. Token expiration                │
│  3. Tenant isolation                │
│  4. Role-based access               │
└─────────────────────────────────────┘
```

### Security Features

| Feature | Implementation | Purpose |
|---------|----------------|---------|
| **JWT Tokens** | HS256 signing with shared secret | Stateless authentication across microservices |
| **Role-Based Access Control** | Claims-based authorization | Admin vs User permissions |
| **Tenant Isolation** | TenantId in every query | Prevent cross-tenant data access |
| **Password Hashing** | BCrypt with salt | Secure password storage |
| **HTTPS Ready** | SSL/TLS configuration | Encrypted communication |
| **SQL Injection Prevention** | Parameterized queries, stored procedures | Database security |
| **CORS Policy** | Whitelist origins | Cross-origin security |

### Hangfire Background Jobs

**Purpose**: Asynchronous task processing without blocking API responses

```csharp
// NotificationService uses Hangfire for:
- Email notifications after license approval
- Payment confirmation emails
- License expiry reminders
- Daily report generation
```

**How It Works:**
1. API enqueues job: `BackgroundJob.Enqueue(() => SendEmail(...))`
2. Hangfire stores job in Redis
3. Worker processes job asynchronously
4. Retry on failure (exponential backoff)
5. Dashboard at `http://localhost:5004/hangfire`

**Benefits:**
- Non-blocking operations
- Automatic retry mechanism
- Job persistence (survives service restarts)
- Real-time monitoring dashboard

---

## ✨ Features

### Multi-Tenant Capabilities

| Feature | Description |
|---------|-------------|
| **Complete Data Isolation** | Each tenant's data is separated at database level with TenantId filtering |
| **Tenant Context** | Automatic tenant resolution from JWT claims |
| **Cross-Tenant Prevention** | Middleware blocks any cross-tenant access attempts |
| **Tenant Administration** | Super admin can manage multiple tenants |

### Role-Based Features

#### 👨‍💼 Admin Role
- ✅ View all licenses for their tenant
- ✅ Create licenses for any user in tenant
- ✅ Auto-activate licenses (skip approval workflow)
- ✅ Delete licenses with cascade cleanup
- ✅ Approve/reject user license applications
- ✅ Access user management dashboard

#### 👤 User Role
- 📝 Create license applications (auto-status: Pending)
- 📊 View only their own licenses
- 📄 Upload supporting documents
- 💳 Make payments via Razorpay
- 🔔 Receive email notifications

### Payment Integration

```
User Creates License → Payment Required → Razorpay Checkout
                                              ↓
                                    Payment Success
                                              ↓
                              Webhook → Verify Signature
                                              ↓
                            Update License Status → Active
                                              ↓
                              Send Notification Email
```

**Razorpay Features:**
- Secure payment gateway integration
- Webhook signature verification
- Automatic license activation on payment success
- Payment history tracking
- Invoice generation

### License Workflow

```
┌──────────────┐
│ User Request │
└──────┬───────┘
       │
       ▼
┌──────────────┐      ┌─────────────┐
│   Pending    │─────▶│ Admin Review│
└──────┬───────┘      └──────┬──────┘
       │                     │
       │              ┌──────▼────────┐
       │              │   Approved    │
       │              └──────┬────────┘
       │                     │
       ▼                     ▼
┌──────────────┐      ┌─────────────┐
│   Payment    │─────▶│   Active    │
└──────────────┘      └─────────────┘
```

---

## � API Documentation

### Authentication Endpoints

\`\`\`http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@test.com",
  "password": "Password123!"
}

Response: { "accessToken": "eyJhbG...", "userId": 1, "tenantId": 1 }
\`\`\`

### License Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/license` | ✅ | Get user's licenses (or all if admin) |
| POST | `/api/license` | ✅ | Create new license |
| GET | `/api/license/{id}` | ✅ | Get license by ID |
| DELETE | `/api/license/{id}` | 🔒 Admin | Delete license |
| GET | `/api/license/types` | - | Get all license types |
| GET | `/api/license/users` | 🔒 Admin | Get all tenant users |

### Payment Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/payment/create` | ✅ | Create Razorpay order |
| POST | `/api/payment/verify` | ✅ | Verify payment signature |
| POST | `/api/payment/webhook` | - | Razorpay webhook handler |

### Request Headers

All authenticated requests must include:

\`\`\`http
Authorization: Bearer <jwt_token>
X-Tenant-ID: <tenant_id>
X-User-Role: <Admin|User>
Content-Type: application/json
\`\`\`

---

## 🧪 Testing

### Quick Smoke Test (5 minutes)

**1. Test Admin Workflow**
\`\`\`bash
# Login as admin
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@test.com","password":"Password123!"}'

# Create license for user
# Use token from login response
curl -X POST http://localhost:5000/api/license \
  -H "Authorization: Bearer <token>" \
  -H "X-Tenant-ID: 1" \
  -d '{"licenseTypeId":1,"userId":2}'
\`\`\`

**2. Test User Workflow**
- Open http://localhost:5005
- Login: `user@test.com` / `Password123!`
- Create License → Status should be "Pending"
- Logout, login as admin
- Approve user's license
- Verify status changed to "Approved"

**3. Test Payment Integration**
- Create license requiring payment
- Click "Make Payment"
- Use Razorpay test card: `4111 1111 1111 1111`
- Verify license activates after payment

### Unit Testing

Each microservice includes unit tests:

\`\`\`bash
# Run tests for LicenseService
cd LicenseService/tests
dotnet test

# Run tests for PaymentService
cd PaymentService/tests
dotnet test
\`\`\`

---

## �️ Development

### Running Individual Services

\`\`\`bash
# Run License Service locally
cd LicenseService/src
dotnet run

# Run with watch mode
dotnet watch run
\`\`\`

### Docker Commands

\`\`\`bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f licenseservice

# Restart single service
docker-compose restart licenseservice

# Stop all
docker-compose down

# Rebuild after code changes
docker-compose up -d --build
\`\`\`

### Database Management

\`\`\`bash
# Connect to SQL Server
docker exec -it gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd123" -C

# Backup database
docker exec gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd123" -C \
  -Q "BACKUP DATABASE Gov2Biz TO DISK='/var/opt/mssql/backup/gov2biz.bak'"

# View Hangfire dashboard
open http://localhost:5004/hangfire
\`\`\`

---

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| **Containers won't start** | `docker-compose down && docker-compose up -d` |
| **Port conflicts** | `lsof -ti:5001 \| xargs kill -9` |
| **Database not initialized** | Startup script auto-runs SQL, or manually: `docker exec -i gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd123" -C -i /Scripts/Gov2Biz_Full_Database_Setup.sql` |
| **JWT validation fails** | Check `Jwt:Key` in `docker-compose.yml` matches across all services |
| **Payment webhook fails** | Verify Razorpay webhook secret in `PaymentService` config |
| **Logs not showing** | `docker-compose logs -f <service-name>` |

---

## 📂 Project Structure

\`\`\`
Gov2Biz/
├── README.md                                    # Main documentation
├── QUICK_START_GUIDE.md                         # Quick reference guide
├── DEPLOYMENT_GUIDE.md                          # Production deployment
├── docker-compose.yml                           # Service orchestration
├── start-mac.sh                                 # macOS/Linux startup
├── start-windows.bat                            # Windows startup
│
├── Scripts/
│   └── Gov2Biz_Full_Database_Setup.sql         # Complete DB setup
│
├── ApiGateway/                                  # Ocelot API Gateway
│   ├── ocelot.json                             # Routing configuration
│   ├── Program.cs                              # Gateway startup
│   └── Dockerfile
│
├── LicenseService/                              # License Management
│   ├── src/
│   │   ├── Controllers/
│   │   │   └── LicenseController.cs            # License CRUD endpoints
│   │   ├── Models/                             # Domain models
│   │   ├── Services/                           # Business logic
│   │   ├── Repositories/                       # Data access (Dapper)
│   │   └── Program.cs                          # Service configuration
│   ├── tests/                                  # Unit tests
│   └── Dockerfile
│
├── PaymentService/                              # Payment Processing
│   ├── src/
│   │   ├── Controllers/
│   │   │   └── PaymentController.cs            # Razorpay integration
│   │   ├── Services/
│   │   │   └── RazorpayService.cs             # Payment logic
│   │   └── Program.cs
│   ├── tests/
│   └── Dockerfile
│
├── DocumentService/                             # Document Management
│   ├── src/
│   │   ├── Controllers/
│   │   │   └── DocumentController.cs           # File upload/download
│   │   └── Program.cs
│   └── Dockerfile
│
├── NotificationService/                         # Background Jobs
│   ├── src/
│   │   ├── Controllers/
│   │   │   └── NotificationController.cs       # Email/SMS endpoints
│   │   ├── Jobs/
│   │   │   └── EmailJob.cs                    # Hangfire jobs
│   │   └── Program.cs                          # Hangfire setup
│   └── Dockerfile
│
└── MVCFrontend/                                # Web Application
    ├── src/
    │   ├── Controllers/
    │   │   ├── LicensesController.cs           # License views
    │   │   ├── PaymentController.cs            # Payment views
    │   │   └── AccountController.cs            # Authentication
    │   ├── Views/
    │   │   ├── Licenses/                       # License UI
    │   │   ├── Payment/                        # Payment UI
    │   │   └── Shared/
    │   │       └── _Layout.cshtml             # Master layout
    │   ├── wwwroot/
    │   │   ├── js/
    │   │   │   └── auth.js                    # JWT token management
    │   │   └── css/                           # Custom styles
    │   └── Program.cs
    └── Dockerfile
\`\`\`

---

## 🎯 What Makes This Special?

### Technical Excellence
- ✅ **Microservices Done Right**: Each service is independently deployable
- ✅ **True Multi-Tenancy**: Database-level isolation with row-level security
- ✅ **Clean Architecture**: SOLID principles, DDD patterns
- ✅ **API Gateway Pattern**: Centralized routing with Ocelot
- ✅ **JWT Authentication**: Stateless, scalable auth across services
- ✅ **Background Jobs**: Hangfire for async processing
- ✅ **Payment Integration**: Production-ready Razorpay implementation
- ✅ **Docker-First**: Full containerization with health checks

### Developer Experience
- 🚀 **One-Command Startup**: `./start-mac.sh` and you're running
- 🔄 **Auto-Initialization**: Database automatically created and seeded
- 📝 **Comprehensive Docs**: Architecture, API, deployment guides
- 🧪 **Unit Tests**: Test coverage for critical services
- 🐳 **Docker Compose**: All services orchestrated seamlessly

---

## � What's Included Out of the Box

### Sample Data
- **2 Tenants**: TechCorp Solutions, Global Enterprises
- **5 Users**: Admins and regular users with different roles
- **10 License Types**: Business, Professional, Construction, Healthcare, etc.
- **Sample Licenses**: Pre-created licenses with various statuses
- **Test Payments**: Payment records for testing

### Default Credentials

| Tenant | Email | Password | Role |
|--------|-------|----------|------|
| TechCorp | admin@test.com | Password123! | Admin |
| TechCorp | user@test.com | Password123! | User |
| TechCorp | audit@test.com | Password123! | Admin, Auditor |
| Global | admin2@test.com | Password123! | Admin |
| Global | user2@test.com | Password123! | User |

---

## � Service Health Checks

Each service exposes health endpoints:

\`\`\`bash
# Check all services
curl http://localhost:5001/health  # License Service
curl http://localhost:5002/health  # Payment Service
curl http://localhost:5003/health  # Document Service
curl http://localhost:5004/health  # Notification Service
\`\`\`

---

## 📚 Additional Documentation

- **[QUICK_START_GUIDE.md](QUICK_START_GUIDE.md)** - Fast setup and common commands
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Production deployment guide
- **[deployments-aws/](deployments-aws/)** - ☁️ **AWS EKS Kubernetes deployment files** with auto-scaling, load balancers, and production-ready configurations
- **Postman Collection**: `Postman/Gov2Biz.postman_collection.json`

---

## ☁️ Cloud Deployment Ready

This project includes **production-ready Kubernetes manifests** for AWS EKS deployment:

### What's Included in `deployments-aws/`

- ✅ **Complete Kubernetes Deployments** - All 8 services containerized and orchestrated
- ✅ **AWS Application Load Balancer** - Automatic HTTPS, SSL termination, path-based routing
- ✅ **Horizontal Pod Autoscaling** - Auto-scale based on CPU/Memory (70-80% threshold)
- ✅ **Persistent Storage** - EBS for SQL Server, EFS for document storage
- ✅ **Network Policies** - Database and Redis security isolation
- ✅ **Health Checks** - Liveness and readiness probes for all services
- ✅ **Resource Limits** - Proper CPU/Memory allocation for cost optimization
- ✅ **ConfigMaps & Secrets** - Environment-based configuration management
- ✅ **Multi-AZ Deployment** - High availability across availability zones
- ✅ **Step-by-Step Guide** - Complete deployment documentation

### Deployment Architecture

```
Internet → ALB (SSL/HTTPS) → Kubernetes Ingress
    ↓
Frontend (3 replicas, auto-scales to 20)
    ↓
API Gateway (2 replicas) → Microservices (2 replicas each)
    ↓
SQL Server + Redis (persistent storage)
```

**Ready to deploy?** Check out `deployments-aws/README.md` for complete instructions!

---

## 🤝 Contributing

This project follows industry-standard practices:

1. **Code Style**: Microsoft C# coding conventions
2. **Branching**: GitFlow (main, develop, feature/*)
3. **Commits**: Conventional commits (feat:, fix:, docs:)
4. **Testing**: Unit tests required for new features
5. **Documentation**: Update README for architectural changes

---

## 📜 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

- **GitHub Copilot**: Accelerated development by ~40%
- **.NET Team**: For excellent documentation and tooling
- **Razorpay**: Seamless payment integration
- **Hangfire**: Reliable background job processing

---

## 📊 Project Stats

| Metric | Value |
|--------|-------|
| **Total Services** | 8 (6 APIs + Gateway + Frontend) |
| **Lines of Code** | ~15,000+ |
| **Database Tables** | 5 core tables |
| **Stored Procedures** | 15+ procedures |
| **API Endpoints** | 30+ endpoints |
| **Docker Images** | 8 images |
| **Technologies Used** | 15+ technologies |
| **Development Time** | ~2 months (with Copilot) |
| **Cloud Deployment** | AWS EKS ready with full K8s manifests |

---

## 👨‍💻 About the Developer

**Created by:** Soumyaranjan Sia  
**Email:** [rahulsia2000@gmail.com](mailto:rahulsia2000@gmail.com)  
**GitHub:** [@soumyaranjansia](https://github.com/soumyaranjansia)

### The Story Behind Gov2Biz ☕

This project was developed as a **take-home assessment** for Gov2Biz - a testament to what can be built in a dedicated timeframe with passion, focus, and the right tools. Built during countless coffee sessions with my trusty companion - **GitHub Copilot** - this represents my journey of implementing enterprise-grade architecture patterns while continuously learning and improving.

**Special Thanks:** I'm incredibly grateful to **Naresh** and **Chris** from Gov2Biz for giving me this opportunity to showcase my skills through this assessment. This challenge allowed me to demonstrate my capabilities in building complex, scalable systems.

### A Note from the Developer

> *"I'm always eager to learn new things and push the boundaries of what I can build. This assessment project showcases my **technical skills, architectural thinking, and problem-solving abilities**. I've poured my knowledge of microservices, multi-tenancy, authentication, payment integration, and cloud deployment into this system."*
>
> *"I know that as humans, we make mistakes, and there's always room for improvement. I've given my best effort to implement industry-standard practices, clean code principles, and production-ready patterns. I hope you find this project demonstrates my understanding of how complex systems can be architected and built."*
>
> *"Thank you, Naresh and Chris, for this opportunity. I've truly enjoyed every moment of building Gov2Biz, and I'm excited about the possibility of contributing to your team!"*
>
> — **Soumyaranjan Sia** ☕💻

### What This Project Demonstrates

✅ Microservices architecture with API Gateway pattern  
✅ Multi-tenant SaaS application design  
✅ JWT-based authentication across distributed services  
✅ Payment gateway integration (Razorpay)  
✅ Background job processing (Hangfire)  
✅ Docker containerization and orchestration  
✅ Kubernetes deployment with auto-scaling  
✅ Clean code and SOLID principles  
✅ RESTful API design  
✅ Database design with stored procedures  
✅ Frontend development with MVC and Bootstrap  

**Built with dedication, coffee, and a lot of learning!** ☕🚀

---

## 📜 License

This project is licensed under the MIT License - feel free to use, modify, and learn from it!

---

## 🤝 Contributing

While this is primarily a portfolio/showcase project, I'm always open to:

- 🐛 Bug reports and fixes
- 💡 Feature suggestions
- 📝 Documentation improvements
- 🎨 UI/UX enhancements
- 🔧 Code optimizations

**Want to contribute?** Feel free to fork, make improvements, and submit a pull request!

---

## 🙏 Acknowledgments

- **Naresh & Chris from Gov2Biz** 🏛️ - For providing this incredible opportunity to showcase my skills through this assessment
- **GitHub Copilot** 🤖 - My AI pair programmer that accelerated development by ~40% and helped me learn best practices
- **.NET Team** - For excellent documentation and tooling
- **Microsoft Azure/AWS** - For cloud infrastructure knowledge
- **Razorpay** - Seamless payment integration
- **Hangfire** - Reliable background job processing
- **Docker & Kubernetes** - For making deployment seamless
- **Open Source Community** - For all the amazing libraries and tools

---

<div align="center">

### Built with ❤️ using .NET 8, Docker, SQL Server, Redis & Bootstrap 5

**Version 1.0** | **Portfolio Showcase** | **November 2025**

### 📧 Contact Me

**Soumyaranjan Sia** | [rahulsia2000@gmail.com](mailto:rahulsia2000@gmail.com)

*"Code is like humor. When you have to explain it, it's bad." – Cory House*

[⬆ Back to Top](#-gov2biz---multi-tenant-license-management-system)

</div>

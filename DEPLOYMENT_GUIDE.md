# 🚀 Gov2Biz Deployment Guide

## Complete Production Deployment Instructions

---

## 📋 Table of Contents
1. [Prerequisites](#prerequisites)
2. [Database Setup](#database-setup)
3. [Local Development](#local-development)
4. [Docker Deployment](#docker-deployment)
5. [Production Deployment](#production-deployment)
6. [Verification](#verification)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software
```bash
# .NET 8 SDK
dotnet --version  # Should be 8.0 or higher

# Docker Desktop
docker --version
docker-compose --version

# SQL Server (one of):
- Docker container (recommended for dev)
- Local SQL Server instance
- Azure SQL Database
```

### Optional Tools
- Postman (for API testing)
- Azure Data Studio or SSMS (for database management)
- Visual Studio 2022 or VS Code

---

## Database Setup

### Step 1: Start SQL Server
```bash
# Using Docker (recommended)
docker run -e 'ACCEPT_EULA=Y' \
  -e 'SA_PASSWORD=YourStrong@Passw0rd' \
  -p 1433:1433 \
  --name gov2biz-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest

# Verify it's running
docker ps | grep gov2biz-sql
```

### Step 2: Execute Database Scripts

**Using Azure Data Studio or SSMS:**
```
Server: localhost,1433
Login: sa
Password: YourStrong@Passw0rd
```

**Execute in this exact order:**
```sql
-- 1. Create tables and schema
Scripts/CreateTables.sql

-- 2. Create stored procedures for LicenseService
Scripts/sp_CreateLicense.sql
Scripts/sp_GetLicenseById.sql
Scripts/sp_RenewLicense.sql
Scripts/sp_GetLicensesByTenant.sql

-- 3. Create stored procedures for PaymentService
Scripts/sp_CreatePayment.sql
Scripts/sp_GetPaymentById.sql
Scripts/sp_UpdatePaymentStatus.sql

-- 4. Create Documents table
Scripts/CreateDocumentsTable.sql

-- 5. Optional: Load test data
Scripts/SeedData.sql
```

**Using sqlcmd (command line):**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz/Scripts

sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i CreateTables.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_CreateLicense.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_GetLicenseById.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_RenewLicense.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_GetLicensesByTenant.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_CreatePayment.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_GetPaymentById.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i sp_UpdatePaymentStatus.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i CreateDocumentsTable.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i SeedData.sql
```

### Step 3: Verify Database Setup
```sql
USE Gov2BizDB;

-- Check tables
SELECT name FROM sys.tables;
-- Should show: Tenants, Users, Licenses, LicenseHistory, Payments, Notifications, Documents

-- Check stored procedures
SELECT name FROM sys.procedures WHERE name LIKE 'sp_%';

-- Check test data (if SeedData.sql was run)
SELECT COUNT(*) FROM Users;     -- Should be 4
SELECT COUNT(*) FROM Licenses;  -- Should be 4
```

---

## Local Development

### Option 1: Run Individually (Recommended for Development)

**Terminal 1 - Redis:**
```bash
docker run -d -p 6379:6379 --name gov2biz-redis redis:alpine
```

**Terminal 2 - LicenseService:**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
dotnet run --project LicenseService/src/Gov2Biz.LicenseService.csproj
```

**Terminal 3 - PaymentService:**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
dotnet run --project PaymentService/src/Gov2Biz.PaymentService.csproj
```

**Terminal 4 - DocumentService:**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
dotnet run --project DocumentService/src/Gov2Biz.DocumentService.csproj
```

**Terminal 5 - NotificationService:**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
dotnet run --project NotificationService/src/Gov2Biz.NotificationService.csproj
```

**Terminal 6 - API Gateway:**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
dotnet run --project ApiGateway/Gov2Biz.ApiGateway.csproj
```

### Option 2: Use setup.sh Helper
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz
chmod +x setup.sh
./setup.sh
# Follow the instructions
```

---

## Docker Deployment

### Full Stack Deployment (Recommended for Testing)

**Step 1: Build and Start All Services**
```bash
cd /Users/soumyaranjansia/Desktop/Gov2Biz

# Build and start all services
docker-compose up --build

# Or run in detached mode
docker-compose up --build -d
```

**Step 2: Verify Services**
```bash
# Check all containers are running
docker-compose ps

# Should show 7 services:
# - sqlserver
# - redis
# - apigateway
# - licenseservice
# - paymentservice
# - documentservice
# - notificationservice
```

**Step 3: View Logs**
```bash
# View all logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f licenseservice
docker-compose logs -f paymentservice
```

**Step 4: Execute Database Scripts**
```bash
# Wait 30 seconds for SQL Server to initialize, then:
docker exec -it gov2biz-sqlserver-1 /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' \
  -Q "CREATE DATABASE Gov2BizDB"

# Then execute scripts as shown in Database Setup section
```

### Individual Service Deployment
```bash
# Build specific service
docker build -f LicenseService/Dockerfile -t gov2biz-licenseservice .

# Run specific service
docker run -p 5001:80 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=Gov2BizDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  gov2biz-licenseservice
```

---

## Production Deployment

### Azure Deployment

**Prerequisites:**
- Azure subscription
- Azure CLI installed

**Step 1: Create Azure Resources**
```bash
# Login to Azure
az login

# Create resource group
az group create --name Gov2BizRG --location eastus

# Create Azure SQL Database
az sql server create --name gov2biz-sql --resource-group Gov2BizRG \
  --location eastus --admin-user sqladmin --admin-password 'YourStrong@Passw0rd123!'

az sql db create --resource-group Gov2BizRG \
  --server gov2biz-sql --name Gov2BizDB \
  --service-objective S0

# Create Azure Cache for Redis
az redis create --name gov2biz-redis --resource-group Gov2BizRG \
  --location eastus --sku Basic --vm-size c0

# Create Azure Container Registry
az acr create --resource-group Gov2BizRG \
  --name gov2bizacr --sku Basic

# Create Azure Container Apps Environment
az containerapp env create --name gov2biz-env \
  --resource-group Gov2BizRG --location eastus
```

**Step 2: Build and Push Docker Images**
```bash
# Login to ACR
az acr login --name gov2bizacr

# Build and push each service
docker build -f LicenseService/Dockerfile -t gov2bizacr.azurecr.io/licenseservice:latest .
docker push gov2bizacr.azurecr.io/licenseservice:latest

docker build -f PaymentService/Dockerfile -t gov2bizacr.azurecr.io/paymentservice:latest .
docker push gov2bizacr.azurecr.io/paymentservice:latest

docker build -f DocumentService/Dockerfile -t gov2bizacr.azurecr.io/documentservice:latest .
docker push gov2bizacr.azurecr.io/documentservice:latest

docker build -f NotificationService/Dockerfile -t gov2bizacr.azurecr.io/notificationservice:latest .
docker push gov2bizacr.azurecr.io/notificationservice:latest

docker build -f ApiGateway/Dockerfile -t gov2bizacr.azurecr.io/apigateway:latest .
docker push gov2bizacr.azurecr.io/apigateway:latest
```

**Step 3: Deploy Container Apps**
```bash
# Deploy LicenseService
az containerapp create --name licenseservice \
  --resource-group Gov2BizRG --environment gov2biz-env \
  --image gov2bizacr.azurecr.io/licenseservice:latest \
  --target-port 80 --ingress external \
  --registry-server gov2bizacr.azurecr.io \
  --env-vars \
    ConnectionStrings__DefaultConnection="Server=gov2biz-sql.database.windows.net;Database=Gov2BizDB;User Id=sqladmin;Password=YourStrong@Passw0rd123!" \
    ConnectionStrings__Redis="gov2biz-redis.redis.cache.windows.net:6380,password=REDIS_KEY,ssl=True"

# Repeat for other services...
```

### Kubernetes Deployment

**Create Kubernetes Manifests** (example for LicenseService):
```yaml
# licenseservice-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: licenseservice
spec:
  replicas: 3
  selector:
    matchLabels:
      app: licenseservice
  template:
    metadata:
      labels:
        app: licenseservice
    spec:
      containers:
      - name: licenseservice
        image: gov2bizacr.azurecr.io/licenseservice:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: gov2biz-secrets
              key: sql-connection
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: gov2biz-secrets
              key: redis-connection
---
apiVersion: v1
kind: Service
metadata:
  name: licenseservice
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 80
  selector:
    app: licenseservice
```

**Deploy to Kubernetes:**
```bash
# Create secrets
kubectl create secret generic gov2biz-secrets \
  --from-literal=sql-connection="Server=..." \
  --from-literal=redis-connection="..."

# Deploy services
kubectl apply -f licenseservice-deployment.yaml
kubectl apply -f paymentservice-deployment.yaml
kubectl apply -f documentservice-deployment.yaml
kubectl apply -f notificationservice-deployment.yaml
kubectl apply -f apigateway-deployment.yaml
```

---

## Verification

### Health Checks
```bash
# Direct service health checks
curl http://localhost:5001/healthz  # LicenseService
curl http://localhost:5002/healthz  # PaymentService
curl http://localhost:5003/healthz  # DocumentService
curl http://localhost:5004/healthz  # NotificationService

# Via API Gateway
curl http://localhost:5000/license-service/healthz
curl http://localhost:5000/payment-service/healthz
```

### Swagger UI
```
LicenseService:     http://localhost:5001/swagger
PaymentService:     http://localhost:5002/swagger
DocumentService:    http://localhost:5003/swagger
NotificationService: http://localhost:5004/swagger
```

### Hangfire Dashboard
```
http://localhost:5001/hangfire
```

### API Testing with Postman

**Step 1: Import Collection**
- Open Postman
- Import `Postman/Gov2Biz.postman_collection.json`

**Step 2: Run Tests in Order**
1. **Authentication** → Login
   - Saves JWT token automatically
2. **Licenses** → Create License
3. **Licenses** → Get License
4. **Payments** → Create Payment
5. **Documents** → Upload Document
6. **Notifications** → Send Notification

**Step 3: Test Error Scenarios**
- Missing Tenant Header
- Invalid Tenant Format
- Unauthorized Access

---

## Troubleshooting

### SQL Server Connection Issues
```bash
# Check if SQL Server is running
docker ps | grep sql

# Check SQL Server logs
docker logs gov2biz-sqlserver-1

# Test connection
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -Q "SELECT @@VERSION"
```

### Redis Connection Issues
```bash
# Check if Redis is running
docker ps | grep redis

# Test Redis connection
docker exec -it gov2biz-redis-1 redis-cli ping
# Should return: PONG
```

### Service Not Starting
```bash
# Check service logs
docker-compose logs licenseservice

# Common issues:
# 1. Database not ready - wait 30 seconds and restart
# 2. Port already in use - change ports in docker-compose.yml
# 3. Connection string incorrect - check appsettings.json
```

### Build Errors
```bash
# Clean build
dotnet clean
dotnet restore
dotnet build --configuration Release

# Clear NuGet cache if needed
dotnet nuget locals all --clear
```

### Database Script Errors
```bash
# Drop all tables and start fresh
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i Scripts/DropTables.sql
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -i Scripts/CreateTables.sql
# ... run other scripts
```

### JWT Token Issues
```bash
# Ensure JWT:Secret is at least 32 characters
# Check appsettings.json in each service

# Test token generation
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"john.doe@tenant1.com","password":"Password123!"}'
```

### Hangfire Not Running
```bash
# Check Redis connection
# Check Hangfire dashboard: http://localhost:5001/hangfire

# Manually trigger job
docker exec -it gov2biz-redis-1 redis-cli
> KEYS *
> FLUSHALL  # Clear all keys (development only!)
```

---

## Performance Tuning

### Database Optimization
```sql
-- Add indexes for frequently queried columns
CREATE INDEX IX_Licenses_Status_ExpiryDate ON Licenses(Status, ExpiryDate);
CREATE INDEX IX_Payments_Status ON Payments(Status);

-- Update statistics
UPDATE STATISTICS Licenses;
UPDATE STATISTICS Payments;
```

### Redis Configuration
```bash
# Increase max memory (if needed)
docker run -d -p 6379:6379 \
  --name gov2biz-redis \
  redis:alpine \
  redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
```

### Connection Pooling
```json
// In appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Min Pool Size=10;Max Pool Size=50;"
  }
}
```

---

## Monitoring

### Application Insights (Azure)
```bash
# Add to each service's csproj
<PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.21.0" />

# In Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### Prometheus Metrics
```bash
# Add to each service
<PackageReference Include="prometheus-net.AspNetCore" Version="8.2.1" />

# In Program.cs
app.UseMetricServer();
app.UseHttpMetrics();
```

---

## Security Checklist

- [ ] Change default JWT secret
- [ ] Change default SQL Server password
- [ ] Enable HTTPS with valid certificates
- [ ] Implement rate limiting
- [ ] Add API key authentication for webhooks
- [ ] Enable CORS only for trusted origins
- [ ] Rotate secrets regularly
- [ ] Enable firewall rules
- [ ] Use Azure Key Vault for secrets
- [ ] Enable audit logging
- [ ] Implement IP whitelisting

---

## Backup and Recovery

### Database Backup
```sql
-- Manual backup
BACKUP DATABASE Gov2BizDB 
TO DISK = '/var/opt/mssql/backup/Gov2BizDB.bak'
WITH FORMAT;

-- Automated backup (Azure SQL)
az sql db ltr-policy set --resource-group Gov2BizRG \
  --server gov2biz-sql --database Gov2BizDB \
  --weekly-retention P4W --monthly-retention P12M
```

### Document Storage Backup
```bash
# Backup documents directory
tar -czf documents-backup-$(date +%Y%m%d).tar.gz Documents/

# Restore
tar -xzf documents-backup-YYYYMMDD.tar.gz
```

---

## Support

For issues or questions:
1. Check logs: `docker-compose logs -f [service]`
2. Review IMPLEMENTATION_STATUS.md
3. Check QUICK_REFERENCE.md for common commands
4. Review README.md for architecture details

---

**Deployment Status**: ✅ Ready for Production
**Last Updated**: 2024
**Version**: 1.0.0

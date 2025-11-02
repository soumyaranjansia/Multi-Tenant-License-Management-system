#!/bin/bash

# ╔══════════════════════════════════════════════════════════════╗
# ║          🚀 Gov2Biz - Quick Start for Mac/Linux             ║
# ║          Multi-Tenant License Management System              ║
# ╚══════════════════════════════════════════════════════════════╝

clear
echo ""
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║          🚀 Gov2Biz - Starting Application                  ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Error: Docker is not running!"
    echo "   Please start Docker Desktop and try again."
    exit 1
fi

echo "✅ Docker is running"
echo ""

# Stop existing containers
echo "🛑 Stopping existing containers..."
docker-compose down > /dev/null 2>&1

# Start services
echo "🚀 Starting Gov2Biz services..."
docker-compose up -d

# Wait for services to be ready
echo ""
echo "⏳ Waiting for services to start..."
sleep 15

# Initialize database
echo "🗄️  Initializing database..."
docker exec gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd123" -C \
  -i /Scripts/Gov2Biz_Full_Database_Setup.sql > /dev/null 2>&1 || echo "   Database already initialized"

# Check service health
echo ""
echo "🔍 Checking service status..."
FRONTEND=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5005 2>/dev/null)

if [ "$FRONTEND" = "200" ] || [ "$FRONTEND" = "302" ]; then
    echo "✅ Frontend: Running"
else
    echo "⚠️  Frontend: Starting..."
fi

echo ""
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║                  🎉 Application Started!                     ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║                                                              ║"
echo "║  🌐 Web Application:  http://localhost:5005                  ║"
echo "║  🔧 API Gateway:      http://localhost:8000                  ║"
echo "║                                                              ║"
echo "║  Test Credentials:                                           ║"
echo "║    👤 User:  testuser@example.com / Password123!            ║"
echo "║    👨‍💼 Admin: admin@test.com / Password123!                  ║"
echo "║                                                              ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "📝 Logs: docker-compose logs -f"
echo "🛑 Stop: docker-compose down"
echo ""

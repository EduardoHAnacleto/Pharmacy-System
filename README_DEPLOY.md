# 🚀 Production Deployment Guide

Pharmacy System -- Full Stack (Vue + .NET + MySQL + Docker)

------------------------------------------------------------------------

## 📌 Overview

This document describes how to deploy the Pharmacy System in a real
production environment using Docker and Docker Compose.

The system consists of:

-   Frontend (Vue 3 + Nginx)
-   Backend (.NET Web API)
-   MySQL 8 Database

All services are containerized and orchestrated using Docker Compose.

------------------------------------------------------------------------

# 🏗 Production Architecture

Client → Frontend (Nginx) → Backend (.NET API) → MySQL Database

All services run inside isolated Docker containers connected through an
internal Docker network.

⚠ In production, the database should NOT be publicly exposed.

------------------------------------------------------------------------

# 🖥 Server Requirements

Minimum recommended:

-   2 CPU cores
-   4GB RAM
-   20GB storage
-   Docker Engine installed
-   Docker Compose plugin installed

Supported environments:

-   Linux Server (Recommended)
-   Windows Server with Docker Desktop
-   VPS (AWS, Azure, DigitalOcean, etc.)

------------------------------------------------------------------------

# 📦 Deployment Steps

## 1️⃣ Install Docker (Linux Example)

``` bash
sudo apt update
sudo apt install docker.io -y
sudo systemctl enable docker
sudo systemctl start docker
```

Install Docker Compose plugin:

``` bash
sudo apt install docker-compose-plugin -y
```

Verify installation:

``` bash
docker --version
docker compose version
```

------------------------------------------------------------------------

## 2️⃣ Upload Project Files

Upload the following to the server:

-   docker-compose.yml
-   .env
-   frontend/
-   backend/

You may upload via:

-   Git clone
-   SCP
-   FTP
-   CI/CD pipeline

------------------------------------------------------------------------

## 3️⃣ Configure Environment Variables

Create a `.env` file in the root directory:

Example:

    MYSQL_ROOT_PASSWORD=StrongRootPassword
    MYSQL_DATABASE=pharmacy_db
    MYSQL_USER=pharmacy_user
    MYSQL_PASSWORD=StrongUserPassword

⚠ Never commit real credentials to version control.

------------------------------------------------------------------------

## 4️⃣ Start the System

From the project root:

``` bash
docker compose up -d --build
```

The `-d` flag runs containers in detached mode.

------------------------------------------------------------------------

## 5️⃣ Verify Running Containers

``` bash
docker ps
```

You should see:

-   pharmacy_frontend
-   pharmacy_api
-   pharmacy_db

------------------------------------------------------------------------

# 🌐 Accessing the Application

Frontend: http://SERVER_IP

Backend API (if exposed): http://SERVER_IP:5000/swagger

------------------------------------------------------------------------

# 🔄 Automatic Restart

The system uses:

restart: unless-stopped

This ensures:

-   Containers restart automatically after server reboot
-   No .bat or startup scripts are required
-   No manual intervention is needed

------------------------------------------------------------------------

# 🔐 Production Security Recommendations

✔ Do NOT expose MySQL port externally\
✔ Use strong passwords\
✔ Configure firewall rules\
✔ Restrict server SSH access\
✔ Use HTTPS (reverse proxy recommended)\
✔ Backup MySQL volume regularly

------------------------------------------------------------------------

# 🔒 Enabling HTTPS (Recommended)

For public deployments, use:

-   Nginx Reverse Proxy
-   Traefik
-   Certbot (Let's Encrypt)

This allows:

-   SSL certificate automation
-   Secure HTTPS communication
-   Domain-based routing

------------------------------------------------------------------------

# 💾 Database Backup Strategy

To backup MySQL:

``` bash
docker exec pharmacy_db mysqldump -u root -p pharmacy_db > backup.sql
```

To restore:

``` bash
docker exec -i pharmacy_db mysql -u root -p pharmacy_db < backup.sql
```

Automate backups via cron job in production.

------------------------------------------------------------------------

# 🔄 Updating the Application

If using local build:

``` bash
docker compose down
docker compose up -d --build
```

If using Docker Hub images:

``` bash
docker compose pull
docker compose up -d
```

------------------------------------------------------------------------

# 📈 Monitoring

Basic monitoring:

``` bash
docker stats
```

Advanced monitoring (optional):

-   Prometheus
-   Grafana
-   Portainer

------------------------------------------------------------------------

# 🧠 Production Best Practices

✔ Keep environment variables externalized\
✔ Use named Docker volumes\
✔ Avoid hardcoded secrets\
✔ Keep containers stateless (except DB volume)\
✔ Maintain versioned Docker images\
✔ Document deployment steps

------------------------------------------------------------------------

# 🏁 Final Notes

This deployment approach ensures:

-   Portability
-   Scalability
-   Production readiness
-   Maintainability
-   Infrastructure consistency

The system can run:

-   On-premise server
-   Cloud VPS
-   Corporate internal network
-   Development environment

------------------------------------------------------------------------

# 👨‍💻 Maintainer

Eduardo Hipolito Anacleto\
Full Stack Developer -- .NET \| Vue \| Docker \| MySQL

------------------------------------------------------------------------

End of Deployment Guide

# FarmaVilmo - Full Stack E-Commerce Pharmacy Platform

## Overview

This project is a full-stack pharmacy e-commerce platform designed to
demonstrate production-ready architecture, clean code practices,
containerization, and scalable backend/frontend integration.

This project was built as a portfolio-ready application to showcase:

-   Frontend architecture with Vue 3 + TypeScript
-   RESTful API development with .NET
-   MySQL relational database design
-   Docker-based containerized deployment
-   Full environment orchestration with Docker Compose

------------------------------------------------------------------------

## Architecture

The system is composed of three main services:

1.  Frontend (Vue 3 + Vite)
2.  Backend API (.NET)
3.  MySQL 8 Database

All services are containerized and orchestrated via Docker Compose.

### High-Level Flow

Client → Vue Frontend → .NET REST API → MySQL Database

------------------------------------------------------------------------

## Tech Stack

### Frontend

-   Vue 3 (Composition API)
-   TypeScript
-   Pinia (State Management)
-   Vite
-   Bootstrap 5

### Backend

-   .NET 9 (ASP.NET Core Web API)
-   Entity Framework Core
-   RESTful architecture

### Database

-   MySQL 8.0

### DevOps & Infrastructure

-   Docker
-   Docker Compose
-   Multi-container orchestration
-   Environment-based configuration

------------------------------------------------------------------------

## Repository Structure

    Pharmacy-System/
    │
    ├── frontend/              # Vue 3 + TypeScript application
    │   ├── src/
    │   ├── public/
    │   ├── Dockerfile
    │   └── vite.config.ts
    │
    ├── backend/                   # .NET Web API
    │   ├── Controllers/
    │   ├── Models/
    │   ├── Data/
    │   ├── Dockerfile
    │   └── Program.cs
    │
    |
    ├── database/             # MySQL Database schema
    │   ├── schema.sql
    │   ├── seed.sql
    |
    |
    ├── docker-compose.yml     # Full stack orchestration
    ├── .env.development
    |
    └── README.md

------------------------------------------------------------------------

## Features

-   Product listing (HomeView)
-   Shopping cart (CartView)
-   Delivery & pickup logic
-   Customer information validation (Name + CPF (ID) with validation)
-   Responsive design
-   Dockerized production build

------------------------------------------------------------------------

## Running the Project (Development Mode)

### 1. Clone the Repository

``` bash
git clone https://github.com/eduardohanacleto/pharmacy.git
cd farmavilmo
```

### 2. Build and Run Containers

``` bash
docker compose up --build
```

### 3. Access the Application

Frontend: http://localhost

API: http://localhost:5000/swagger

MySQL: localhost:3306

------------------------------------------------------------------------

## Running with Prebuilt Images (Production Mode)

This project provides prebuilt Docker images published to GitHub Container Registry (GHCR).

No local build is required.

### Requirements

- Docker
- Docker Compose

### Run the Application

```bash
docker compose up

------------------------------------------------------------------------

## Docker Deployment Strategy

-   Frontend runs in a Node-based container
-   API runs in a .NET runtime container
-   MySQL runs as an official MySQL 8 container
-   All services communicate via Docker internal network

------------------------------------------------------------------------

## Environment Variables

Example configuration:

API: - ASPNETCORE_ENVIRONMENT=Production -
ConnectionStrings\_\_DefaultConnection=Server=db;Port=3306;Database=pharmacy_db;User=pharmacy_user;Password=StrongUserPassword;

Database: - MYSQL_ROOT_PASSWORD=StrongUserPassword - MYSQL_DATABASE=pharmacy_db

------------------------------------------------------------------------

## Screenshots

![Main Layout](MainLayout-1.png)
![Shopping Cart](Cart-1.png)
![Checkout Page](Checkout-1.png)
![Contact Page](Contact-1.png)
------------------------------------------------------------------------

## Production Readiness Highlights

-   Separation of concerns (Frontend / API / Database)
-   Containerized services
-   Stateless API design
-   Environment-based configuration
-   Scalable architecture foundation

------------------------------------------------------------------------

## Future Improvements

-   Authentication (JWT-based)
-   Admin dashboard
-   Order persistence
-   Payment gateway integration
-   CI/CD pipeline
-   Cloud deployment (AWS, Azure, or GCP)

------------------------------------------------------------------------

## Author

Eduardo Hipolito Anacleto

Full Stack Developer \| 
.NET \| Python  \|
Vue \| Vue.Js \| Javascript \|
Docker \| MySQL \| PostgreSQL \| SQL Server 

------------------------------------------------------------------------

## License

This project is for portfolio and demonstration purposes.

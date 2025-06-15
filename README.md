# Ecommerce Platform

A **modular**, microservices-based e-commerce platform built with .NET 8, featuring authentication, order management, payment processing, and user management.  
**This is a learning project**—exploring modern microservices architecture, authentication, and payment integration (including bKash and Stripe).

---

## Table of Contents

- [About](#about)
- [Architecture](#architecture)
- [Services](#services)
- [Getting Started](#getting-started)
- [Running Locally](#running-locally)
- [CI/CD](#cicd)

---

## About

This project is designed for learning and experimentation with:

- Microservices using .NET 8
- Authentication with Keycloak
- Payment integration (Stripe and bKash)
- Containerization with Docker
- CI/CD using GitHub Actions

---

## Architecture

- **Microservices**: Each core domain (User, Order, Payment) is a separate .NET service.
- **Authentication**: Centralized with Keycloak (OpenID Connect).
- **Data Storage**: SQL Server for persistent storage, Redis for caching.
- **Communication**: REST APIs (Swagger enabled).
- **Containerization**: Only infrastructure dependencies (Keycloak, SQL Server, Redis) are orchestrated with Docker Compose..

---

## Services

| Service         | Description                               | Path              | Port (default) |
|----------------|-------------------------------------------|-------------------|----------------|
| UserService     | User registration, login, profile         | `/UserService`    | 7268           |
| OrderService    | Order creation, management                | `/OrderService`   | 7038           |
| PaymentService  | Payment processing (Stripe, bKash)        | `/PaymentService` | 7266           |
| Keycloak        | Identity and access management            | Docker container  | 8080           |
| SQL Server      | Database                                  | Docker container  | 1433           |
| Redis           | Caching                                   | Docker container  | 6379           |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Docker](https://www.docker.com/)
- [Git](https://git-scm.com/)

### Clone the Repository

```bash
git clone https://github.com/yourusername/EcommercePlatform.git
cd EcommercePlatform
```
## Running Locally

### 1. Start Infrastructure

```bash
docker-compose up -d
```
- Keycloak: http://localhost:8080
- SQL Server: localhost:1433
- Redis: localhost:6379

### 2. Build and Run Services

You can run each service individually:

```bash
dotnet build EcommercePlatform.sln
dotnet run --project UserService/UserService.csproj
dotnet run --project OrderService/OrderService.csproj
dotnet run --project PaymentService/PaymentService.csproj
```

### 3. API Documentation
Each service exposes Swagger UI at /swagger

## CI/CD

- GitHub Actions: Automated build on every push/PR to the Production branch.
- Workflow: Restores dependencies and builds the solution.
- Branch Protection: PRs and passing builds required for merging to Production.

### Note:
Update service ports and URLs as per your launch settings if they differ.

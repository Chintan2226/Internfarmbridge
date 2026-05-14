# FarmBridge - B2B Agri-Procurement Pltform

A comprehensive platform connecting farmers with vendors and facilitating agricultural procurement, payments, and inventory management. Built with C# .NET ecosystem and modern web technologies.

## 📋 Project Overview

**FarmBridge** is an internship project that bridges the gap between farmers and vendors through a robust digital platform. It enables farmers to manage their fields, track procurement requests, process payments, and optimize their inventory while vendors can manage their products and fulfill farmer requirements.

## 🏗️ Architecture

The project follows a **multi-tier architecture** with two main applications:

### 1. **API** (RESTful Backend)
- Built with **ASP.NET Core 8.0**
- Provides REST endpoints for mobile and web clients
- Implements JWT-based authentication
- Structured with **BAL** (Business Logic Layer), **Controllers**, **Models**, and **Services**

### 2. **MVC** (Web Application)
- ASP.NET Core MVC web application
- User-friendly interface for farmers and vendors
- Cookie-based authentication
- **Multi-language support** (English, Hindi, Gujarati)
- Responsive design with static files management

### 3. **Python** (Data Processing)
- Data import and loading utilities
- Database operations and ETL processes

## 🛠️ Technology Stack

### Backend & Core
- **Language**: C# (.NET 8.0)
- **Runtime**: ASP.NET Core
- **Authentication**: JWT Bearer Tokens + Cookie-based Auth
- **Database**: PostgreSQL (via Npgsql)
- **Caching**: Redis (StackExchange.Redis)

### External Services & Libraries
- **Cloud Storage**: Cloudinary (for image management)
- **Email**: MailKit (SMTP integration)
- **Message Queue**: RabbitMQ (for asynchronous notifications)
- **Search Engine**: Elasticsearch
- **Payment Gateway**: Razorpay
- **PDF Generation**: QuestPDF
- **Authentication**: JWT Bearer, OAuth (Google Auth)
- **API Documentation**: Swagger/OpenAPI

### Frontend
- **Views**: Razor Views (ASP.NET MVC)
- **Localization**: .NET Standard Resource Files
- **Image Service**: Unsplash Integration

## 📁 Project Structure

```
Internfarmbridge/
├── API/
│   ├── Program.cs              # API Configuration & DI Setup
│   ├── API.csproj              # API Project File
│   ├── API.http                # HTTP Testing File
│   ├── BAL/                    # Business Logic Layer
│   ├── Controllers/            # API Endpoints
│   ├── Models/                 # Data Models & Settings
│   ├── Services/               # External Service Integration
│   │   ├── RedisService        # Caching
│   │   ├── EmailService        # Email Notifications
│   │   ├── CloudinaryService   # Image Upload
│   │   ├── ElasticService      # Search
│   │   ├── RabbitMqService     # Message Queue
│   │   ├── AiInventoryService  # AI-based Inventory
│   │   └── JwtService          # Token Generation
│   ├── Templates/              # Email & Document Templates
│   └── Properties/             # App Metadata
├── MVC/
│   ├── Program.cs              # MVC Configuration
│   ├── MVC.csproj              # MVC Project File
│   ├── Controllers/            # Page Handlers
│   ├── Models/                 # View Models
│   ├── Services/               # Business Services
│   │   ├── AuthApiService      # API Authentication
│   │   └── UnsplashService     # Image Search
│   ├── Views/                  # Razor View Templates
│   ├── Resources/              # Localization Files
│   ├── Filters/                # Authentication Filters
│   ├── wwwroot/                # Static Files (CSS, JS, Images)
│   └── scratch/                # Development/Test Files
├── Python/
│   ├── Import Data/            # Data Import Scripts
│   ├── Load Data/              # Data Loading Scripts
│   └── test_db.py              # Database Testing
├── Internfarmbridge.sln        # Solution File
└── t_payments_farmer.json      # Sample Payment Data

```

## 🔐 Key Features

### Authentication & Authorization
- **JWT Authentication** for API endpoints
- **Cookie-based Authentication** for MVC web application
- **Google OAuth Integration** for social login
- Role-based access control (Admin, Farmer, Field Officer, Vendor, Staff)

### Core Functionality
- **Farmer Management**: Field tracking, crop details
- **Vendor Management**: Product catalog, availability
- **Procurement**: Request creation and fulfillment
- **Payments**: Razorpay integration for secure transactions
- **Notifications**: Real-time alerts via RabbitMQ & email
- **Search & Discovery**: Elasticsearch-powered product search
- **Inventory Management**: AI-powered inventory optimization

### Data Features
- **Multi-language Support**: English, Hindi, Gujarati
- **Real-time Caching**: Redis for performance optimization
- **Document Generation**: QuestPDF for reports
- **Cloud Storage**: Cloudinary for image management

## 🚀 Getting Started

### Prerequisites
- **.NET 8.0 SDK** or later
- **PostgreSQL 12+**
- **Redis Server**
- **RabbitMQ Server**
- **.NET CLI** or **Visual Studio 2022**

### Environment Configuration

Create `appsettings.json` in both API and MVC with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;Database=farmbridge;User Id=postgres;Password=yourpassword;"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Jwt": {
    "Key": "your-secret-key-here",
    "Issuer": "farmbridge-api",
    "Audience": "farmbridge-client"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-app-password"
  },
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone https://github.com/Chintan2226/Internfarmbridge.git
   cd Internfarmbridge
   ```

2. **Open the solution**
   ```bash
   # Using Visual Studio
   start Internfarmbridge.sln
   
   # Or using .NET CLI
   dotnet sln Internfarmbridge.sln
   ```

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Run migrations** (if applicable)
   ```bash
   dotnet ef database update
   ```

5. **Start the API**
   ```bash
   cd API
   dotnet run
   ```
   API will be available at: `https://localhost:5000`
   Swagger Documentation: `https://localhost:5000/swagger`

6. **Start the MVC** (in another terminal)
   ```bash
   cd MVC
   dotnet run
   ```
   MVC will be available at: `https://localhost:5001`

## 📡 API Endpoints

The API provides comprehensive REST endpoints. Key areas:

- **Authentication**: `/api/auth/login`, `/api/auth/register`
- **Farmers**: `/api/farmers`, `/api/farmers/{id}/fields`
- **Vendors**: `/api/vendors`, `/api/vendors/products`
- **Procurement**: `/api/procurement/requests`
- **Payments**: `/api/payments`
- **Inventory**: `/api/inventory`

**Full API Documentation**: Available at `/swagger` endpoint after running the API

## 🗄️ Database

**Primary Database**: PostgreSQL
- **Connection**: Npgsql driver
- Contains tables for farmers, vendors, fields, procurement, payments, etc.

**Sample Data**: `t_payments_farmer.json` contains example payment records

## 📨 External Integrations

| Service | Purpose | Status |
|---------|---------|--------|
| Razorpay | Payment Processing | ✅ Integrated |
| Cloudinary | Image Storage | ✅ Integrated |
| MailKit | Email Sending | ✅ Integrated |
| RabbitMQ | Message Queue | ✅ Integrated |
| Elasticsearch | Full-text Search | ✅ Integrated |
| Redis | Caching | ✅ Integrated |
| Google OAuth | Social Authentication | ✅ Integrated |

## 🔄 CI/CD & Deployment

Currently configured for:
- Development on the `dev` branch
- Multiple merge strategies (commit, squash, rebase)
- Issue and pull request tracking enabled

## 📊 Project Metrics

- **Language**: Primarily C#
- **Repository Size**: ~19.5 MB
- **Default Branch**: `dev`
- **Created**: 23 days ago
- **Last Updated**: April 30, 2026

## 🤝 Team & Contribution

This is a **team internship project**. Contributions are made by multiple team members through:
- Pull requests on the `dev` branch
- Issue tracking and project management
- Code reviews and collaborative development

## 🐛 Known Issues & TODOs

- Python data import/loading scripts need completion
- Full test coverage needed
- API rate limiting to be implemented
- Complete API endpoint documentation in Swagger

## 🎯 Future Enhancements

- Mobile app development (iOS/Android)
- Advanced AI features for crop prediction
- IoT sensor integration for field monitoring
- Blockchain for supply chain transparency
- Advanced analytics dashboard
- Multi-currency support

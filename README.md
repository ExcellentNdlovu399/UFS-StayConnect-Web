# UFS StayConnect Web

## Integrated University Accommodation and Financial Management System

UFS StayConnect is a web-based accommodation and financial management platform developed to support accommodation administration and student/staff services at the University of the Free State (UFS), QwaQwa Campus.

The system provides a centralised platform where administrators can manage accommodation facilities, rooms, applications, services and customer charges, while students and staff can access their accommodation information, view charges and manage payments.

The platform is developed using **ASP.NET Core 9, C#, Entity Framework Core and Microsoft SQL Server**, with a separate Android mobile application designed to communicate with the backend through REST APIs.

## Key Features

### 1. User and Role Management
- Secure user registration and authentication.
- Role-based access control.
- Three user roles: Administrator, Financial Officer and Customer.
- Role-specific dashboards and functionality.
- User profile management.

### 2. Accommodation Management
- Create and manage accommodation facilities.
- Manage rooms and accommodation availability.
- View accommodation information.
- Process and manage accommodation applications.
- Approve or reject accommodation applications.
- Allocate approved customers to accommodation facilities and rooms.

### 3. Financial and Billing Management
- Create and assign charges to customers.
- Manage accommodation rental charges.
- Manage water and electricity charges.
- Manage parking charges based on registered vehicles.
- Display outstanding balances and payment information.
- Allow customers to view charges assigned to them.

### 4. Services Management
- Manage accommodation-related services.
- Configure service charges.
- Track customer service usage.
- Display relevant service information to customers.

### 5. Payment Management
- View payment information and outstanding charges.
- Support payment-related records and transaction management.
- Provide customers with payment information based on available payment methods.

### 6. Notifications
- Display notifications to users.
- Provide updates relating to applications, charges and other system activities.

### 7. REST API Integration
- ASP.NET Core Web API endpoints.
- JWT-based API authentication.
- Backend integration with the Android mobile application.
- Shared backend data for the web and mobile platforms.

## User Roles

| Role | Responsibilities |
|---|---|
| Administrator | Manages users, accommodation facilities, rooms, services and system administration. |
| Financial Officer | Handles financial operations, charges and payment-related administration. |
| Customer | Applies for accommodation, views allocations, accesses services and manages charges and payments. |

## Technologies Used

| Technology | Purpose |
|---|---|
| C# | Backend programming |
| ASP.NET Core 9 MVC | Web application |
| ASP.NET Core Web API | Mobile application integration |
| Entity Framework Core | Database access and ORM |
| Microsoft SQL Server | Database management |
| HTML5, CSS3 and JavaScript | Frontend development |
| Bootstrap 5 | Responsive user interface |
| Font Awesome | Icons |
| JWT | API authentication |

## Project Architecture

The solution consists of a web application and a REST API backend.

- **Web Application:** Provides browser-based access for administrators, Financial Officers and customers.
- **REST API:** Exposes backend functionality to external applications.
- **Database:** Stores user accounts, accommodation records, applications, services, charges and payment information.
- **Android Application:** A separate native Android application that communicates with the backend API.

## Installation and Setup

### Prerequisites
- .NET 9 SDK
- Microsoft SQL Server
- Visual Studio 2022 or later
- SQL Server Management Studio (optional)

### Setup Instructions

1. Clone the repository:

   bash
   git clone https://github.com/ExcellentNdlovu399/UFS-StayConnect-Web.git
  

2. Open the solution in Visual Studio.

3. Configure your database connection string using local configuration or environment variables. Do not commit credentials.

4. Restore NuGet packages:

   bash
   dotnet restore
   

5. Apply Entity Framework Core migrations:

   bash
   dotnet ef database update
 

6. Run the application:

   bash
   dotnet run
  

7. Open the local URL displayed in the terminal.

## Demo Login Accounts

The following section is reserved for dedicated demonstration accounts.

**Administrator**
- Email: Admin@ufs.ac.za
- Password: Admin@123

Do not use production credentials or personal accounts for demonstrations. Change temporary passwords before use and keep credentials out of public source code.

## Project Information

**Project:** UFS StayConnect  
**Institution:** University of the Free State  
**Campus:** QwaQwa Campus  
**Application:** Web Application and REST API  
**Framework:** ASP.NET Core 9

## Developer

**Minenhle Excellent Ndlovu**

GitHub: https://github.com/ExcellentNdlovu399

---

*UFS StayConnect is an academic software development project and is not an official University of the Free State product.*


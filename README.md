# MeetingRoomWeb

[English](#english) | [Tiếng Việt](#tiếng-việt) | [中文](#中文)

---

## English

### Meeting Room Booking Management System

A full-featured web application for managing meeting room bookings, built with **ASP.NET Core 10.0** and **PostgreSQL**. Includes a native cross-platform launcher (C++ / Dear ImGui) for one-click service management.

### Features

- **Authentication** — Register, Login, Forgot/Reset Password (BCrypt, auto-migrate SHA-256)
- **Booking Management** — Create, Edit, Delete bookings with conflict detection
- **Admin Workflow** — Approve/Reject bookings with rejection reasons
- **Room Management** — CRUD operations for meeting rooms (capacity, equipment, status)
- **User Management** — Admin can change roles (Admin/Employee) and delete accounts
- **Document Upload** — Attach files (PDF, DOCX, XLSX, PNG, JPG) to bookings, stored securely outside wwwroot
- **Dashboard** — Upcoming bookings, room usage timeline with day/week/month filters
- **Dark/Light Theme** — Toggle with localStorage persistence
- **Security** — Rate limiting (10 req/min on auth), CSP headers, CSRF protection, audit logging
- **Tailscale Funnel** — Remote access via HTTPS without port forwarding
- **Native Launcher** — C++ ImGui app to start/stop Docker, PostgreSQL, Tailscale, and the web app

### Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 10.0 (MVC) |
| Database | PostgreSQL 15 + Entity Framework Core 10 |
| Frontend | Razor Views + Tailwind CSS 3 |
| Auth | Cookie Authentication + BCrypt |
| Launcher | C++17, Dear ImGui 1.91, SDL2, OpenGL |
| Deployment | Docker (PostgreSQL), Tailscale Funnel |

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker](https://www.docker.com/) (for PostgreSQL)
- [Tailscale](https://tailscale.com/) (optional, for remote access)

### Quick Start

```bash
# 1. Start PostgreSQL
docker run -d --name meeting-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 postgres:15

# 2. Run the app
cd MeetingRoomWeb
dotnet run

# 3. Open http://localhost:5038
```

### Default Credentials

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@test.com | 123456 |
| Employee | test@test.com | 123456 |

### Project Structure

```
MeetingRoomWeb/
├── Controllers/          # MVC Controllers (Home, Bookings, MeetingRooms, Users)
├── Data/                 # Entity Framework DbContext
├── Models/               # Domain models (User, Booking, MeetingRoom, Document)
├── Views/                # Razor views with Tailwind CSS
├── Migrations/           # EF Core database migrations
├── MeetingRoomLauncher/  # Native C++ launcher (ImGui + SDL2)
├── wwwroot/              # Static assets (CSS, JS, favicon)
├── Program.cs            # App entry point & middleware config
├── start.sh              # macOS startup script
└── appsettings.json      # Configuration
```

### Screenshots

The app features a split-panel layout:
- **Left panel** — Auth (login/register/forgot password) and navigation
- **Right panel** — Dashboard with upcoming bookings and room usage timeline
- **Modals** — Booking CRUD, room management, user management (all AJAX)

### License

MIT

---

## Tiếng Việt

### Hệ thống quản lý lịch họp

Ứng dụng web đầy đủ tính năng để quản lý đặt lịch họp, xây dựng bằng **ASP.NET Core 10.0** và **PostgreSQL**. Bao gồm ứng dụng launcher đa nền tảng (C++ / Dear ImGui) để quản lý dịch vụ bằng một cú nhấp chuột.

### Tính năng

- **Xác thực** — Đăng ký, Đăng nhập, Quên/Đặt lại mật khẩu (BCrypt, tự động migrate từ SHA-256)
- **Quản lý lịch họp** — Tạo, Sửa, Xoá lịch với phát hiện trùng lịch
- **Phê duyệt** — Admin duyệt/Từ chối lịch với lý do từ chối
- **Quản lý phòng** — CRUD phòng họp (sức chứa, thiết bị, trạng thái)
- **Quản lý tài khoản** — Admin phân quyền (Admin/Nhân viên) và xoá tài khoản
- **Đính kèm tài liệu** — Upload file (PDF, DOCX, XLSX, PNG, JPG), lưu trữ an toàn ngoài wwwroot
- **Dashboard** — Lịch sắp tới, timeline sử dụng phòng với bộ lọc ngày/tuần/tháng
- **Giao diện tối/sáng** — Chuyển đổi với localStorage
- **Bảo mật** — Rate limiting (10 req/phút), CSP headers, CSRF protection, audit logging
- **Tailscale Funnel** — Truy cập từ xa qua HTTPS không cần port forwarding
- **Launcher** — Ứng dụng C++ ImGui để start/stop Docker, PostgreSQL, Tailscale và web app

### Công nghệ

| tầng | công nghệ |
|------|----------|
| Backend | ASP.NET Core 10.0 (MVC) |
| Cơ sở dữ liệu | PostgreSQL 15 + Entity Framework Core 10 |
| Frontend | Razor Views + Tailwind CSS 3 |
| Xác thực | Cookie Authentication + BCrypt |
| Launcher | C++17, Dear ImGui 1.91, SDL2, OpenGL |
| Triển khai | Docker (PostgreSQL), Tailscale Funnel |

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker](https://www.docker.com/) (cho PostgreSQL)
- [Tailscale](https://tailscale.com/) (tuỳ chọn, cho truy cập từ xa)

### Khởi động nhanh

```bash
# 1. Khởi động PostgreSQL
docker run -d --name meeting-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 postgres:15

# 2. Chạy ứng dụng
cd MeetingRoomWeb
dotnet run

# 3. Mở http://localhost:5038
```

### Tài khoản mặc định

| Vai trò | Email | Mật khẩu |
|---------|-------|----------|
| Admin | admin@test.com | 123456 |
| Nhân viên | test@test.com | 123456 |

### Cấu trúc dự án

```
MeetingRoomWeb/
├── Controllers/          # MVC Controllers (Home, Bookings, MeetingRooms, Users)
├── Data/                 # Entity Framework DbContext
├── Models/               # Models (User, Booking, MeetingRoom, Document)
├── Views/                # Razor views với Tailwind CSS
├── Migrations/           # EF Core database migrations
├── MeetingRoomLauncher/  # Launcher C++ (ImGui + SDL2)
├── wwwroot/              # Static assets (CSS, JS, favicon)
├── Program.cs            # Entry point & cấu hình middleware
├── start.sh              # Script khởi động macOS
└── appsettings.json      # Cấu hình
```

### Giấy phép

MIT

---

## 中文

### 会议室预订管理系统

一个功能齐全的会议室预订管理 Web 应用，使用 **ASP.NET Core 10.0** 和 **PostgreSQL** 构建。包含一个跨平台原生启动器（C++ / Dear ImGui），可一键管理所有服务。

### 功能特性

- **身份验证** — 注册、登录、忘记/重置密码（BCrypt，自动从 SHA-256 迁移）
- **预订管理** — 创建、编辑、删除预订，支持冲突检测
- **审批流程** — 管理员可批准/拒绝预订并填写拒绝原因
- **会议室管理** — 会议室的增删改查（容量、设备、状态）
- **用户管理** — 管理员可更改角色（管理员/员工）和删除账户
- **文档上传** — 为预订附加文件（PDF、DOCX、XLSX、PNG、 JPG），安全存储在 wwwroot 之外
- **仪表板** — 即将到来的预订、按日/周/月筛选的会议室使用时间线
- **深色/浅色主题** — 支持切换，使用 localStorage 持久化
- **安全性** — 速率限制（认证接口每分钟 10 次）、CSP 头、CSRF 防护、审计日志
- **Tailscale Funnel** — 通过 HTTPS 远程访问，无需端口转发
- **原生启动器** — C++ ImGui 应用，可启动/停止 Docker、PostgreSQL、Tailscale 和 Web 应用

### 技术栈

| 层级 | 技术 |
|------|------|
| 后端 | ASP.NET Core 10.0 (MVC) |
| 数据库 | PostgreSQL 15 + Entity Framework Core 10 |
| 前端 | Razor Views + Tailwind CSS 3 |
| 身份验证 | Cookie Authentication + BCrypt |
| 启动器 | C++17, Dear ImGui 1.91, SDL2, OpenGL |
| 部署 | Docker (PostgreSQL), Tailscale Funnel |

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker](https://www.docker.com/)（用于 PostgreSQL）
- [Tailscale](https://tailscale.com/)（可选，用于远程访问）

### 快速开始

```bash
# 1. 启动 PostgreSQL
docker run -d --name meeting-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 postgres:15

# 2. 运行应用
cd MeetingRoomWeb
dotnet run

# 3. 打开 http://localhost:5038
```

### 默认账户

| 角色 | 邮箱 | 密码 |
|------|------|------|
| 管理员 | admin@test.com | 123456 |
| 员工 | test@test.com | 123456 |

### 项目结构

```
MeetingRoomWeb/
├── Controllers/          # MVC 控制器 (Home, Bookings, MeetingRooms, Users)
├── Data/                 # Entity Framework DbContext
├── Models/               # 领域模型 (User, Booking, MeetingRoom, Document)
├── Views/                # Razor 视图 + Tailwind CSS
├── Migrations/           # EF Core 数据库迁移
├── MeetingRoomLauncher/  # 原生 C++ 启动器 (ImGui + SDL2)
├── wwwroot/              # 静态资源 (CSS, JS, favicon)
├── Program.cs            # 应用入口 & 中间件配置
├── start.sh              # macOS 启动脚本
└── appsettings.json      # 配置文件
```

### 许可证

MIT

# Nestify — Bachelor Life, Unified

## Team Members

1. **Tahmid Mubashira Obonti**
   - Email: tahmidobonti9@gmail.com
   - ID: 20230104048

2. **Tasmia Tabassum Shreoshi**
   - Email: tasmiashreoshi@gmail.com
   - ID: 20230104026

3. **Tasnima Faruk Prapty**
   - Email:  tasnimafarukprapty@gmail.com
   - ID: 20230104038

4. **Kazi Ishmamul Haque**
   - Email: kaziishmamulhaque@gmail.com
   - ID: 20230104040

## Project Overview

### Objective
Nestify is an integrated web platform for Bangladeshi university students and bachelors living away from home. It unifies four fragmented, informal parts of bachelor life — accommodation & roommate search, domestic help hiring, shared expense tracking, and a student marketplace — under a single verified account, closing gaps around trust, verification, and local-context relevance that existing tools (Facebook groups, word of mouth, notebooks, generic classifieds) don't address.

### Target Audience
- University students living in mess/shared accommodation
- Bachelors relocating for work or study
- Domestic help service providers (maids/cooks)
- Platform administrators

## Tech Stack
- **Backend:** ASP.NET Core Web API (Clean Architecture)
- **Database:** PostgreSQL (via Entity Framework Core)
- **Frontend:** React.js
- **Styling:** Bootstrap
- **Maps/Location:** Leaflet.js + OpenStreetMap
- **Auth:** ASP.NET Identity + JWT (role-based access control)
- **Deployment:** Dockerized — Vercel / Render / Neon
- **Rendering Method:** Client-Side Rendering (CSR)

## Project Features

### Core Features
- **Accommodation & Roommate Finder** — map-based House/Vacancy listings, search & filter by location/budget/distance, Manager & Co-Manager controls, tenant management
- **Domestic Help Management** — verified Maid profiles, AI-based proximity/charge/availability filtering, hiring workflow, ratings & reviews
- **Shared Expense & Utility Tracker** — Fixed Cost + Meal-based expense splitting, automatic per-meal cost calculation, monthly settlement statements
- **Student/Bachelor Marketplace** — post/search/filter second-hand items by category, price & area, buyer-seller contact, listing management
- **User Registration, Authentication & Role Management** — role-based access (Student/User, Maid, Admin, dynamic House Manager/Co-Manager), login/logout/password reset, profile management

### AI-Assisted Features
- **Smart Maid Recommendation** — ranks domestic help by proximity, charge, and availability
- **Roommate Compatibility Matching** — matches users by budget, location, lifestyle habits, and study schedule

### Admin Features
- Report & content moderation (listings, marketplace posts, Maid profiles, reviews)
- User account management & policy enforcement
- Platform statistics dashboard
- Logged, auditable moderation actions

## CRUD Operations
- Users
- Houses / Listings
- Maids
- Expenses / Settlements
- Marketplace Items
- Reports
- Admin

## Non-Functional Highlights
- Search/filter results in 2–3 seconds
- Password hashing, RBAC, SQL Injection/XSS/CSRF protection
- Responsive, mobile-friendly UI
- Docker-based deployment for consistent dev/prod environments
- Immutable financial records post-settlement, with logged corrections

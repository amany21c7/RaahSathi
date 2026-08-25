# 🚗 RaahSathi - Complete Platform Manual & Testing Guide

Welcome to the **RaahSathi Roadside Assistance Network** comprehensive testing manual and system documentation. This document covers every page, button, tool, and feature across all user roles (Customer, Mechanic, and Admin) with step-by-step testing scenarios.

---

## 📌 Executive Summary & Architecture

**RaahSathi** is an end-to-end connected roadside breakdown assistance platform built on ASP.NET Core MVC (NET 9.0), Entity Framework Core (SQL Server), Leaflet JS Maps, and custom Haversine Dispatch & Dynamic Pricing Engines.

### Core Portals & User Roles:
1. **Public Portal**: Unauthenticated visitors (Services, How It Works, Emergency SOS, Multi-Language, AI Chatbot).
2. **Customer Portal**: Vehicle owners requesting emergency breakdown dispatch, tracking mechanics live, and rating services.
3. **Mechanic Portal**: Verified patrol mechanics accepting jobs, updating duty status (Online/Offline), submitting custom parts estimates, and completing repairs.
4. **Admin Console**: Operations desk monitoring live GPS dispatch radar, verifying KYC documents, configuring pricing rules, and resolving issues.

---

## 🏠 1. Public Portal & Global Navigation

### 1.1 Header Navbar
- **Brand Logo & Name**: Clicking the **RaahSathi** logo redirects to the Home page.
- **Portal Pill Navigation**:
  - `Home`: Overview hero, quick service cards, live network status.
  - `Services`: Detailed listing of services (Towing, Battery Jumpstart, Flat Tire, Fuel Delivery, Key Unlock).
  - `How It Works`: 3-step breakdown assistance guide (Request -> Auto Dispatch -> Repair).
  - `About Us`: Mission, Vision, KYC Safety & Network statistics.
  - `Contact Us`: Support helpline, office location, and contact form.
  - `Dynamic Role Button`: Switches between **Customer Dashboard**, **Mechanic Dashboard**, and **Admin Console** depending on active login session.
- **Multi-Language Engine**:
  - Supports 10 Indian Languages (*English, Hindi, Marathi, Gujarati, Punjabi, Bengali, Tamil, Telugu, Kannada, Malayalam*).
  - **Zero-Flash Translation Guard**: Instant language switching via custom dropdown without full page reloading.
- **User Profile Pill**: Displays initials / avatar with Quick Logout option.

### 1.2 Footer Section
- **Brand Overview**: 24×7 Highway emergency description.
- **Emergency Support Highlight**: Prominent hotline banner (`1800-102-7224`) and red **SOS** trigger button.
- **Quick Links**: Navigation & Legal links (Privacy Policy, FAQs & Knowledgebase, Escrow Protection Rules, KYC Policy).
- **Network Status Card**: Live Dispatch System Status (`ONLINE`), Average Arrival ETA (`Under 14 Mins`), and direct WhatsApp button.
- **Copyright & Tricolor Badge**: `© 2026 RaahSathi Roadside Assistance Network. All rights reserved.` paired with **Make In India** (Kesariya `#FF9933`, Safed `#FFFFFF`, Hara `#00E676`).

---

## 🚨 2. Emergency SOS & AI Chatbot Options

### 2.1 Emergency SOS Trigger (`#emergencySupportModal`)
- **Location**: Top Navbar SOS Button, Footer SOS Button, and Dashboard Emergency Cards.
- **How It Works**:
  1. Opens an instant high-priority glassmorphism modal.
  2. Displays Toll-Free Helpline: `1800-102-7224`.
  3. Action 1: **Call Helpline Now** (`tel:18001027224`).
  4. Action 2: **Request Instant Dispatch** (opens 30-Second Quick Request Form).
  5. Action 3: **WhatsApp Emergency Desk** (`https://wa.me/919876543210`).

### 2.2 Floating AI Chatbot (`#aiFloatingWidgetContainer`)
- **Location**: Bottom-Right corner of all pages.
- **How It Works**:
  1. Click **Chat** floating button to toggle the compact AI Assistant window.
  2. Offers Quick Suggestion Buttons: *Book Mechanic*, *Visiting Charges*, *Emergency Helpline*.
  3. Enter custom query in text field -> sends request to `/Home/AskAiChat` -> displays response with loading spinner.

---

## 👤 3. Customer Operations Portal

### 3.1 Customer Dashboard (`/Customer/Dashboard`)
- **Registered Vehicles Bar**: Shows customer's saved vehicles with a 1-click **Quick Request** button.
- **Add New Vehicle**: Form to register Car, Bike, Auto, or Commercial Truck with Model & License Plate.
- **Active Breakdown Dispatch Cards**: Shows live status of current ongoing breakdown jobs with link to **Live Tracker**.
- **Service History Table**: List of past completed/cancelled jobs with ratings and receipts.

### 3.2 Instant Breakdown Request (`/Customer/Book` & `QuickRequest`)
- **Geolocation API**: Captures user's exact GPS Latitude and Longitude automatically.
- **30-Second Express Guest Booking**: Unauthenticated users enter Name, 10-digit Phone, and instant verification code (`1234`) to auto-create account and launch dispatch.
- **Problem Category & Upload**: Select problem type (*Battery Jumpstart, Towing, Flat Tire, Engine Stall, Fuel Delivery*) and optional photo upload.
- **Dynamic Pricing Preview**: Displays calculated Visiting Charge and estimated Service Charge range.

### 3.3 Live Dispatch Tracker (`/Customer/Tracker?jobId=...`)
- **Status Timeline**: `Requested` ➡️ `Assigned / Accepted` ➡️ `In Progress` ➡️ `Completed`.
- **Mechanic Profile Info**: Assigned mechanic name, phone, photo, KYC verification badge, and vehicle model.
- **Live ETA & Map Radar**: Visual Leaflet JS map displaying customer location and mechanic marker.
- **Job Cancellation**: Allows customer to cancel job before mechanic starts work.
- **Job Rating & Feedback**: After work completion, customer can rate (1 to 5 stars), select positive tags (*Punctual, Professional, Fair Price*), and write a review.

---

## 🔧 4. Mechanic Field Operations Portal

### 4.1 Mechanic Dashboard (`/Mechanic/Dashboard`)
- **Duty Toggle Switch**: Toggle **Online / Offline** status instantly. Only Online mechanics receive job broadcasts.
- **KYC Status Badge**: Shows document status (*Approved, Pending, Rejected*).
- **Incoming Broadcast Alerts**: Real-time pop-up notification when a new customer breakdown occurs nearby with Distance (km), Problem Type, Customer Address, and Visiting Fee.
- **Accept / Decline Job**: Button to claim job instantly or pass to next patrol mechanic.

### 4.2 Active Job Management (`/Mechanic/JobDetails?id=...`)
- **Job Stage Progression**:
  - `Accept Job`: Sets status to `Accepted`.
  - `Start Journey`: Sets status to `In Progress` (Customer notified mechanic is en route).
  - `Submit Custom Estimate`: If extra parts or labour are needed, mechanic inputs Part Name & Additional Charge for customer approval.
  - `Complete Work`: Marks job as `Completed`, triggers visiting charge + service charge settlement.

---

## 🛡️ 5. Admin Command & Operations Console

### 5.1 Admin Dashboard (`/Admin/Dashboard`)
- **System Metrics Overview**: Total Revenue (₹), Total Jobs, Active Patrol Mechanics, KYC Verification Queue.
- **Global Dispatch Radar**: Interactive Leaflet JS map plotting all active breakdown locations, customer coordinates, and mechanic positions in real time.
- **Job Management Matrix**: View, filter, flag, or reassign any job in the network.

### 5.2 Mechanic KYC Verification (`/Admin/KycApproval`)
- **Document Audit**: Review mechanic Aadhaar Card, Driving License, Workshop Address, and Skill Certification photos.
- **Actions**: One-click **Approve KYC** or **Reject KYC** with reason input.

### 5.3 Dynamic Pricing Rules (`/Admin/PricingRules`)
- **Base Fee Configuration**: Configure base visiting fees and per-kilometer rates for Cars, 2-Wheelers, Heavy Vehicles, and Commercial Trucks.

---

## 🧪 Step-by-Step Testing Guide

### Scenario 1: Emergency SOS & Quick Request Testing
1. Open Home Page (`http://localhost:5000` or running app URL).
2. Click the red **SOS** button in top header or bottom footer.
3. Verify that **24×7 Emergency Highway Support** modal opens.
4. Click **Request Instant Mechanic Dispatch**.
5. Select vehicle type (e.g., *Car*), problem (e.g., *Flat Tire*), enter name & phone number.
6. Use test verification code `1234` and click **Submit Request**.
7. Verify auto-redirect to **Live Tracker** page.

### Scenario 2: Mechanic Duty & Job Acceptance Testing
1. Open a new Incognito / Browser window and go to `/Auth/Login?role=Mechanic`.
2. Login as Mechanic or register a new mechanic.
3. On Mechanic Dashboard, toggle duty switch to **Online**.
4. Verify incoming job alert appears for the request created in Scenario 1.
5. Click **Accept Job**.
6. Click **Start Journey** -> Status updates to `In Progress`.
7. Click **Complete Work** -> Job completed.

### Scenario 3: Customer Rating & Review Testing
1. Return to Customer Live Tracker window.
2. Verify status updates to **Completed**.
3. Fill rating form (5 Stars, select tags: *Fast Arrival, Polite*), add comment, and submit.
4. Verify rating reflects on Mechanic profile and Admin Dashboard.

### Scenario 4: Admin KYC & Pricing Rules Testing
1. Navigate to `/Admin/Dashboard` (or login as Admin).
2. Check **Global Dispatch Radar** map.
3. Navigate to KYC Approvals section to verify pending mechanics.
4. Edit a Pricing Rule and verify updated visiting charges on customer booking screen.

---
*Generated by RaahSathi System Agent • July 2026*

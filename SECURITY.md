# Security Audit Report — MeetingRoomWeb

**Date:** 2026-05-18
**Scope:** Full source code review
**Total findings:** 17 (4 Critical, 6 High, 5 Medium, 2 Low)

---

## CRITICAL (4)

### CVE-MR-001: Database credentials exposed in source code
**File:** `appsettings.json:9`
```json
"DefaultConnection": "Host=localhost;Port=5432;Database=MeetingDB;Username=postgres;Password=postgres"
```
**Risk:** Database credentials committed to public GitHub repo. Anyone can see them.
**Fix:** Use environment variables or .NET User Secrets. Remove from repo, rotate password immediately.

### CVE-MR-002: Password hashes exposed in backup.sql
**File:** `backup.sql:150-154`
**Risk:** SHA-256 hashes of "123456" can be cracked in <1 second via rainbow table. File is in public repo.
**Fix:** Remove backup.sql from repo. Change all passwords. Force BCrypt migration for all users.

### CVE-MR-003: Default credentials are trivially weak
**Details:** Admin account `admin@test.com` / `123456`, all test users also use `123456`.
**Risk:** Attacker can log in as admin immediately.
**Fix:** Force password change on first login. Do not seed default credentials in production.

### CVE-MR-004: XSS via JavaScript inline injection
**File:** `Views/Home/Index.cshtml:190-191`
```html
onclick="openApproveModal('@b.Id','@b.Title.Replace("'","\\'")')"
```
**Risk:** If booking title contains `'); alert(document.cookie);//`, JS executes. `Replace("'", "\\'")` does not encode `"`, `<`, `>`, `\n`, Unicode escapes.
**Fix:** Use `@Json.Serialize(b.Title)` or `HtmlEncoder.Default.Encode()`. Or use `data-*` attributes + event listener (already done for edit button, apply to all).

---

## HIGH (6)

### CVE-MR-005: CSP allows `unsafe-inline` scripts
**File:** `Program.cs:80`
```
script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com;
```
**Risk:** `unsafe-inline` completely disables CSP XSS protection. Combined with any XSS vulnerability = full exploit.
**Fix:** Use nonce-based or hash-based CSP. Move inline scripts to separate JS files.

### CVE-MR-006: Cookie missing `Secure` flag
**File:** `Program.cs:27`
```csharp
options.Cookie.SecurePolicy = CookieSecurePolicy.None;
```
**Risk:** Cookie sent over HTTP if attacker downgrades connection.
**Fix:** Use `CookieSecurePolicy.Always` or at minimum `SameAsRequest`.

### CVE-MR-007: No HSTS header in production
**File:** `Program.cs:64-69`
**Risk:** `UseHsts()` only works when `!IsDevelopment`. Missing `UseHttpsRedirection` means HTTP requests are not redirected = MITM possible.
**Fix:** Always enable HSTS + HTTPS redirect in production.

### CVE-MR-008: File upload does not validate content-type
**File:** `Controllers/HomeController.cs:267-272`
**Risk:** Only checks file extension, not MIME type or file content. Attacker can upload `shell.aspx.jpg` or file with mismatched magic bytes. No malware scanning.
**Fix:** Validate extension + MIME type (`document.ContentType`) + magic bytes.

### CVE-MR-009: Rate limiting too permissive for auth
**File:** `Program.cs:41-48`
```csharp
o.PermitLimit = 10;  // 10 requests per minute
```
**Risk:** 10 attempts/min = 600 attempts/hour. Brute-force still feasible. Only applies to auth endpoints; other POST endpoints have no rate limit.
**Fix:** Reduce to 5 per 5 minutes for auth. Add rate limiting to all POST endpoints.

### CVE-MR-010: Anti-forgery token fallback sends empty token
**File:** `Views/Home/Index.cshtml:924`
```javascript
const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
```
**Risk:** If token doesn't exist (DOM changed, session expired), empty token is sent. Server rejects but error handling is unclear.
**Fix:** Validate token before sending. Redirect to login if token is missing.

---

## MEDIUM (5)

### CVE-MR-011: Tailwind CDN has no SRI integrity
**File:** `Views/Shared/_Layout.cshtml:10`
```html
<script src="https://cdn.tailwindcss.com"></script>
```
**Risk:** If CDN is compromised, attacker injects malicious JS into every page.
**Fix:** Add `integrity="sha384-..."` attribute or self-host Tailwind.

### CVE-MR-012: `ForgotPassword` has no rate limit
**File:** `Controllers/HomeController.cs:179`
**Risk:** Missing `[EnableRateLimiting("auth")]` unlike Login/Register. Attacker can brute-force identity verification.
**Fix:** Add `[EnableRateLimiting("auth")]` to ForgotPassword and ResetPassword.

### CVE-MR-013: Audit log lacks context
**File:** `Controllers/HomeController.cs:110`
**Risk:** Logs login failure but not user-agent, no consecutive failure count, no alert mechanism.
**Fix:** Add user-agent, timestamp, failed attempt counter. Alert on >5 failures from same IP.

### CVE-MR-014: `ChangeRole` logging incomplete
**File:** `Controllers/HomeController.cs:440`
**Risk:** Logs role change but not previous role. Cannot audit "who was demoted from Admin to Employee".
**Fix:** Log both `oldRole` and `newRole`.

### CVE-MR-015: Hardcoded Tailscale domain in AllowedHosts
**File:** `appsettings.json:8`
```json
"AllowedHosts": "2026.tailba3e29.ts.net;2026-1.tailba3e29.ts.net;localhost;127.0.0.1"
```
**Risk:** Exposes internal Tailscale hostname in public repo.
**Fix:** Use environment variable or move to `appsettings.Production.json` (gitignored).

---

## LOW (2)

### CVE-MR-016: `start.command` has hardcoded paths
**File:** `start.command:7`
```bash
PROJECT_DIR="/Volumes/PortableSSD/Congviec/MeetingRoomWeb"
```
**Risk:** Exposes local machine directory structure. Information disclosure.
**Fix:** Use relative path or `$(dirname "$0")` like `start.sh`.

### CVE-MR-017: Outdated jQuery in wwwroot/lib
**File:** `wwwroot/lib/jquery/dist/jquery.min.js`
**Risk:** Old jQuery versions have known XSS vulnerabilities (CVE-2020-11022, CVE-2020-11023).
**Fix:** Update jQuery to latest or remove entirely (code already uses fetch API).

---

## Compliance Summary

| Criteria | Status | Notes |
|----------|--------|-------|
| Authentication | PASS | BCrypt, auto-migrate SHA-256, cookie auth |
| Authorization | PASS | Role-based (Admin/Employee), `[Authorize]` correct |
| Input Validation | WARN | Extension check but missing content-type |
| XSS Protection | FAIL | unsafe-inline CSP, manual string replace in Razor |
| CSRF Protection | PASS | `[ValidateAntiForgeryToken]` on all POST |
| Rate Limiting | WARN | Auth only, too permissive |
| Secrets Management | FAIL | DB creds + password hashes in repo |
| HTTPS/TLS | WARN | Tailscale handles but missing defense-in-depth |
| Logging/Audit | PASS | Audit logs for auth, role change, booking actions |
| File Upload | WARN | Secure storage but missing content validation |

---

## Remediation Priority

| Priority | Items | Timeline |
|----------|-------|----------|
| P0 — Immediate | #001, #002, #003 | Rotate creds, remove backup.sql, change defaults |
| P1 — Within 24h | #004, #005, #006 | Fix XSS, harden CSP, enable Secure cookie |
| P2 — Within 1 week | #008, #009, #011, #012 | Content-type validation, rate limiting, SRI |
| P3 — Within 1 month | #007, #010, #011, #013, #014, #015, #016, #017 | HSTS, self-host Tailwind, audit improvements |

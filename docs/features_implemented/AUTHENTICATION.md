# Authentication Feature

## Overview
MobileAICLI now includes password-based authentication to protect access to the application.

## Setup

### 1. Generate a Password Hash

Generate a password hash using C# code or a utility script.

### 2. Set Environment Variable

```bash
export MOBILEAICLI_PASSWORD_HASH='pbkdf2$100000$...'
```

### 3. Configure Settings

Edit `appsettings.json`:
- `EnableAuthentication`: true/false
- `SessionTimeoutMinutes`: 30
- `MaxFailedLoginAttempts`: 5
- `FailedLoginDelaySeconds`: 1

## Security Features
- PBKDF2 password hashing with SHA-256
- Rate limiting on failed login attempts
- Secure session cookies (HttpOnly, Secure, SameSite)
- IP address masking in logs
- Automatic session expiration

## Usage
1. Navigate to the application
2. Log in with your password
3. Use the application normally
4. Log out when done

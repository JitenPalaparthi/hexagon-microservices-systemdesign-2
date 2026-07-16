# Zero Trust Architecture (ZTA)

> **Never Trust, Always Verify**

Zero Trust is a security model where **every request is authenticated,
authorized, and continuously validated**, regardless of whether it
originates from inside or outside the network.

## Core Principles

1.  Never Trust
2.  Always Verify
3.  Least Privilege Access
4.  Assume Breach
5.  Continuous Verification
6.  Micro-Segmentation
7.  Strong Identity
8.  Encrypt Everything

## NIST Pillars

-   Identity
-   Device
-   Network
-   Application
-   Workload
-   Data
-   Visibility & Analytics
-   Automation & Orchestration

## Authentication & Authorization Technologies

### OAuth 2.0

-   Authorization framework
-   Issues Access Tokens and Refresh Tokens
-   Delegates permissions

### OpenID Connect (OIDC)

-   Built on OAuth 2.0
-   Adds authentication
-   Provides an ID Token

### JWT (JSON Web Token)

Structure: - Header - Payload - Signature

Used for carrying identity and authorization claims.

### MFA and TOTP

-   Password + OTP
-   Time-based One-Time Passwords (30-second rotation)
-   Google Authenticator, Microsoft Authenticator, Authy

## Credential Rotation

### Password Rotation

Regularly rotate database, application, and service passwords using a
secrets manager.

### Secret Rotation

Use: - HashiCorp Vault - Azure Key Vault - AWS Secrets Manager

### Certificate Rotation

Automatically renew and replace TLS/mTLS certificates before expiration.

### JWT Signing Key Rotation

Publish old and new signing keys through JWKS until old tokens expire.

### Refresh Token Rotation

Issue a new refresh token whenever one is redeemed.

## mTLS

Service-to-service authentication using certificates.

    Orders Service
          |
        mTLS
          |
    Payments Service
          |
    Verify Client Certificate
          |
    Allow

## Zero Trust Request Flow

    User
     |
    MFA
     |
    OIDC Login
     |
    JWT
     |
    API Gateway
     |
    JWT Validation
     |
    Orders Service
     |
    mTLS
     |
    Payments Service
     |
    mTLS
     |
    Inventory
     |
    Database

## Common Use Cases

-   Banking
-   Healthcare
-   Government
-   Enterprise SSO
-   Kubernetes Service Mesh
-   B2B APIs
-   IoT
-   Cloud-native Microservices

## Common Technologies

Identity: - Microsoft Entra ID - Keycloak - Okta - Auth0

API Gateways: - NGINX - Kong - Apache APISIX - Azure API Management

Service Mesh: - Istio - Linkerd - Consul

Observability: - Prometheus - Grafana - OpenTelemetry - Jaeger

## Zero Trust Checklist

-   Strong Identity
-   OAuth 2.0
-   OpenID Connect
-   JWT
-   MFA
-   TOTP
-   mTLS
-   Least Privilege
-   RBAC / ABAC
-   Micro-segmentation
-   Continuous Verification
-   Secret Rotation
-   Password Rotation
-   Certificate Rotation
-   Encryption Everywhere
-   Audit Logging
-   Assume Breach

## Key Takeaways

-   Never trust any request by default.
-   Continuously verify users, devices, workloads, and services.
-   Use OAuth + OIDC + JWT for user identity.
-   Use mTLS for service identity.
-   Rotate passwords, secrets, certificates, and signing keys.
-   Monitor, log, and automate security responses continuously.

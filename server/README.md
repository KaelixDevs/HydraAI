# Optional account service architecture

No cloud endpoint is configured and the desktop collects no credentials. Local mode is fully independent.

Before implementing `IAccountService`, provide TLS, normalized/unique usernames and emails, salted password hashes, per-IP/account rate limits, breached-password checks, opaque random sessions stored by hash, rotating refresh tokens, device revocation, CSRF protection where applicable, single-use expiring reset tokens, non-enumerating reset responses, email verification and optional WebAuthn/TOTP MFA. Deliver email via a user-configured provider. Never ship shared credentials.

Sync requires separate opt-in and client-held encryption keys; server-side encryption alone must not be called end-to-end encryption. The local password utility is tested infrastructure, not a claim that an authentication service is deployed.

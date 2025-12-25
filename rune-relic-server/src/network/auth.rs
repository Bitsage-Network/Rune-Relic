//! JWT Authentication
//!
//! Validates JWTs from external auth providers (Firebase, Auth0, Supabase, etc.).
//! The server does NOT issue tokens - only validates them.

use jsonwebtoken::{decode, DecodingKey, Validation, Algorithm, TokenData};
use serde::{Deserialize, Serialize};
use sha2::{Sha256, Digest};
use std::time::{SystemTime, UNIX_EPOCH};
use thiserror::Error;

use crate::game::state::PlayerId;

/// Authentication configuration.
#[derive(Clone, Debug)]
pub struct AuthConfig {
    /// Expected issuer claim ("iss"). If None, any issuer accepted.
    pub issuer: Option<String>,
    /// Expected audience claim ("aud"). If None, any audience accepted.
    pub audience: Option<String>,
    /// RS256 public key in PEM format (preferred for external providers).
    pub public_key_pem: Option<String>,
    /// HS256 secret (fallback for simple setups).
    pub secret: Option<String>,
    /// Whether to skip expiry validation (for testing only).
    pub skip_expiry: bool,
}

impl Default for AuthConfig {
    fn default() -> Self {
        Self {
            issuer: None,
            audience: None,
            public_key_pem: None,
            secret: None,
            skip_expiry: false,
        }
    }
}

impl AuthConfig {
    /// Create config from environment variables.
    pub fn from_env() -> Self {
        Self {
            issuer: std::env::var("AUTH_ISSUER").ok(),
            audience: std::env::var("AUTH_AUDIENCE").ok(),
            public_key_pem: std::env::var("AUTH_PUBLIC_KEY_PEM").ok(),
            secret: std::env::var("AUTH_SECRET").ok(),
            skip_expiry: std::env::var("AUTH_SKIP_EXPIRY")
                .map(|v| v == "true" || v == "1")
                .unwrap_or(false),
        }
    }

    /// Check if authentication is configured.
    pub fn is_configured(&self) -> bool {
        self.public_key_pem.is_some() || self.secret.is_some()
    }
}

/// Standard JWT claims we expect from auth providers.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TokenClaims {
    /// Subject - usually the user ID from the auth provider.
    pub sub: String,
    /// Expiry timestamp (Unix seconds).
    #[serde(default)]
    pub exp: u64,
    /// Issued at timestamp.
    #[serde(default)]
    pub iat: u64,
    /// Issuer (auth provider).
    #[serde(default)]
    pub iss: Option<String>,
    /// Audience.
    #[serde(default)]
    pub aud: Option<serde_json::Value>,
}

impl TokenClaims {
    /// Derive a deterministic PlayerId from the subject claim.
    /// Uses SHA256 to create a 16-byte ID from the subject string.
    pub fn player_id(&self) -> PlayerId {
        let mut hasher = Sha256::new();
        hasher.update(b"rune-relic-player:");
        hasher.update(self.sub.as_bytes());
        let hash = hasher.finalize();

        let mut id = [0u8; 16];
        id.copy_from_slice(&hash[..16]);
        PlayerId::new(id)
    }
}

/// Authentication errors.
#[derive(Debug, Error)]
pub enum AuthError {
    /// No authentication configured on server.
    #[error("authentication not configured")]
    NotConfigured,
    /// Token format is invalid.
    #[error("invalid token format")]
    InvalidFormat,
    /// Token signature verification failed.
    #[error("invalid signature")]
    InvalidSignature,
    /// Token has expired.
    #[error("token expired")]
    Expired,
    /// Issuer claim doesn't match expected value.
    #[error("invalid issuer")]
    InvalidIssuer,
    /// Audience claim doesn't match expected value.
    #[error("invalid audience")]
    InvalidAudience,
    /// Required claim is missing.
    #[error("missing required claim: {0}")]
    MissingClaim(String),
    /// JWT decoding error.
    #[error("decode error: {0}")]
    DecodeError(String),
}

/// Validate a JWT token and extract claims.
pub fn validate_token(token: &str, config: &AuthConfig) -> Result<TokenClaims, AuthError> {
    if !config.is_configured() {
        return Err(AuthError::NotConfigured);
    }

    // Determine algorithm based on config
    let algorithm = if config.public_key_pem.is_some() {
        Algorithm::RS256
    } else {
        Algorithm::HS256
    };

    // Build validation rules
    let mut validation = Validation::new(algorithm);

    // Disable required claims validation by default
    validation.required_spec_claims = std::collections::HashSet::new();

    // Set expected issuer (if not set, any issuer is accepted)
    if let Some(ref issuer) = config.issuer {
        validation.set_issuer(&[issuer]);
    }

    // Set expected audience (if not set, skip audience validation)
    if let Some(ref audience) = config.audience {
        validation.set_audience(&[audience]);
    } else {
        validation.validate_aud = false;
    }

    // Handle expiry validation
    if config.skip_expiry {
        validation.validate_exp = false;
    }

    // Decode and validate
    let token_data: TokenData<TokenClaims> = if let Some(ref pem) = config.public_key_pem {
        let key = DecodingKey::from_rsa_pem(pem.as_bytes())
            .map_err(|e| AuthError::DecodeError(format!("invalid public key: {}", e)))?;
        decode(token, &key, &validation)
            .map_err(|e| map_jwt_error(e))?
    } else if let Some(ref secret) = config.secret {
        let key = DecodingKey::from_secret(secret.as_bytes());
        decode(token, &key, &validation)
            .map_err(|e| map_jwt_error(e))?
    } else {
        return Err(AuthError::NotConfigured);
    };

    let claims = token_data.claims;

    // Validate subject exists
    if claims.sub.is_empty() {
        return Err(AuthError::MissingClaim("sub".into()));
    }

    // Manual expiry check (in case validation was skipped)
    if !config.skip_expiry && claims.exp > 0 {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs();
        if now > claims.exp {
            return Err(AuthError::Expired);
        }
    }

    Ok(claims)
}

/// Map JWT library errors to our error type.
fn map_jwt_error(err: jsonwebtoken::errors::Error) -> AuthError {
    use jsonwebtoken::errors::ErrorKind;
    match err.kind() {
        ErrorKind::ExpiredSignature => AuthError::Expired,
        ErrorKind::InvalidSignature => AuthError::InvalidSignature,
        ErrorKind::InvalidIssuer => AuthError::InvalidIssuer,
        ErrorKind::InvalidAudience => AuthError::InvalidAudience,
        ErrorKind::InvalidToken | ErrorKind::Base64(_) => AuthError::InvalidFormat,
        _ => AuthError::DecodeError(err.to_string()),
    }
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use jsonwebtoken::{encode, EncodingKey, Header};

    fn create_test_token(claims: &TokenClaims, secret: &str) -> String {
        let header = Header::new(Algorithm::HS256);
        let key = EncodingKey::from_secret(secret.as_bytes());
        encode(&header, claims, &key).unwrap()
    }

    fn test_claims() -> TokenClaims {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs();
        TokenClaims {
            sub: "user123".into(),
            exp: now + 3600, // 1 hour from now
            iat: now,
            iss: Some("test-issuer".into()),
            aud: Some(serde_json::json!("test-audience")),
        }
    }

    #[test]
    fn test_valid_token_validation() {
        let secret = "test-secret-key-256-bits-long!!";
        let claims = test_claims();
        let token = create_test_token(&claims, secret);

        let config = AuthConfig {
            secret: Some(secret.into()),
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(result.is_ok());
        assert_eq!(result.unwrap().sub, "user123");
    }

    #[test]
    fn test_expired_token_rejected() {
        let secret = "test-secret-key-256-bits-long!!";
        let mut claims = test_claims();
        claims.exp = 1; // Expired in 1970

        let token = create_test_token(&claims, secret);

        let config = AuthConfig {
            secret: Some(secret.into()),
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(matches!(result, Err(AuthError::Expired)));
    }

    #[test]
    fn test_invalid_signature_rejected() {
        let claims = test_claims();
        let token = create_test_token(&claims, "correct-secret-key-here!!!!!");

        let config = AuthConfig {
            secret: Some("wrong-secret-key-here!!!!!!".into()),
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(matches!(result, Err(AuthError::InvalidSignature)));
    }

    #[test]
    fn test_missing_sub_rejected() {
        let secret = "test-secret-key-256-bits-long!!";
        let mut claims = test_claims();
        claims.sub = String::new();

        let token = create_test_token(&claims, secret);

        let config = AuthConfig {
            secret: Some(secret.into()),
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(matches!(result, Err(AuthError::MissingClaim(_))));
    }

    #[test]
    fn test_issuer_validation() {
        let secret = "test-secret-key-256-bits-long!!";
        let claims = test_claims();
        let token = create_test_token(&claims, secret);

        let config = AuthConfig {
            secret: Some(secret.into()),
            issuer: Some("wrong-issuer".into()),
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(matches!(result, Err(AuthError::InvalidIssuer)));
    }

    #[test]
    fn test_player_id_derivation() {
        let claims = TokenClaims {
            sub: "user123".into(),
            exp: 0,
            iat: 0,
            iss: None,
            aud: None,
        };

        let id1 = claims.player_id();
        let id2 = claims.player_id();

        // Same sub should give same ID
        assert_eq!(id1, id2);

        // Different sub should give different ID
        let other_claims = TokenClaims {
            sub: "user456".into(),
            ..claims
        };
        let id3 = other_claims.player_id();
        assert_ne!(id1, id3);
    }

    #[test]
    fn test_not_configured_error() {
        let config = AuthConfig::default();
        let result = validate_token("some.jwt.token", &config);
        assert!(matches!(result, Err(AuthError::NotConfigured)));
    }

    #[test]
    fn test_skip_expiry_for_testing() {
        let secret = "test-secret-key-256-bits-long!!";
        let mut claims = test_claims();
        claims.exp = 1; // Expired in 1970

        let token = create_test_token(&claims, secret);

        let config = AuthConfig {
            secret: Some(secret.into()),
            skip_expiry: true,
            ..Default::default()
        };

        let result = validate_token(&token, &config);
        assert!(result.is_ok());
    }
}

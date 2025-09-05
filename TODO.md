# TODO: JWT & Refresh Token Endpoint

## Users Controller
- [ ] Create an endpoint for issuing JWT and refresh tokens
- [ ] Accept login credentials (e.g., email + password)
- [ ] Validate user credentials against the database
- [ ] Generate JWT with claims (include `UserId`)
- [ ] Generate refresh token

## Controller Filter
- [ ] Implement controller filter to set `UserId` from JWT claims
- [ ] Add logging/check to confirm `UserId` is correctly set
- [ ] Verify filter is triggered for protected endpoints

## Redis Integration
- [ ] Store refresh tokens in Redis (keyed by `UserId`)
- [ ] Check Redis to confirm token is being saved
- [ ] Ensure old refresh tokens are invalidated/overwritten
- [ ] Add Redis read check when validating refresh token

## Testing
- [ ] Test login request → receive JWT + refresh token
- [ ] Decode JWT → confirm `UserId` is present
- [ ] Call protected endpoint → confirm `UserId` is set by filter
- [ ] Inspect Redis → confirm refresh token is stored
- [ ] Test refresh token endpoint → issue new JWT
- [ ] Test invalid/expired refresh token handling

## Extras (Optional)
- [ ] Add token expiry config (short for JWT, longer for refresh)
- [ ] Add logout endpoint → remove refresh token from Redis
- [ ] Add middleware to reject blacklisted/expired tokens

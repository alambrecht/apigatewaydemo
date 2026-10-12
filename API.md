# API reference

All requests go through the gateway at **`http://localhost:8000`**. The auth service and the demo API have no published ports, so they can't be called directly from outside Docker.

| Method | Path | Auth | Required role | Purpose |
|---|---|---|---|---|
| `POST` | [`/auth/login`](#post-authlogin) | none | — | Exchange a username and password for an access token |
| `GET` | [`/api/public`](#get-apipublic) | none | — | Public endpoint |
| `GET` | [`/api/me`](#get-apime) | Bearer token | any | Show the identity inside your token |
| `GET` | [`/api/items`](#get-apiitems) | Bearer token | `reader` | List items |
| `GET` | [`/api/admin/stats`](#get-apiadminstats) | Bearer token | `admin` | Admin statistics |

Test users (defined in `auth/UserStore.cs`):

| Username | Password | Roles |
|---|---|---|
| `alice` | `alice` | `reader` |
| `bob` | `bob` | `reader`, `admin` |

---

## Authentication

1. Call [`POST /auth/login`](#post-authlogin) to get an `access_token`.
2. Send it on every protected request:

   ```
   Authorization: Bearer <access_token>
   ```

Tokens last **15 minutes** (`expires_in: 900`). There is no refresh endpoint, so log in again when a token expires.

Every protected request is checked twice: once by the gateway and again by the demo API. Both check the token's signature, issuer, audience, expiry and the route's required role.

### Token format

The token is a JWT signed with **RS256**. Decoded, it looks like this:

**Header**

```json
{ "alg": "RS256", "typ": "JWT", "kid": "93mCDKHfCzJLqUTr" }
```

**Payload**

```json
{
  "iss": "http://auth:8080",
  "aud": "demo-api",
  "sub": "6f1c2a9e-0000-4000-8000-000000000001",
  "preferred_username": "alice",
  "roles": ["reader"],
  "iat": 1791837650,
  "nbf": 1791837650,
  "exp": 1791838550,
  "jti": "76e5414d-cf48-4d3e-a47b-6c978d67d015"
}
```

| Claim | Meaning |
|---|---|
| `iss` | Issuer: the auth service. Must equal `http://auth:8080`. |
| `aud` | Audience. Must equal `demo-api`. |
| `sub` | Stable user ID |
| `preferred_username` | Username |
| `roles` | Always a JSON array, even with one role |
| `iat` / `nbf` / `exp` | Issued at / not valid before / expires (Unix seconds) |
| `jti` | Unique token ID |

`kid` depends on the signing key, which is generated on the auth service's first start. Yours will differ.

---

## Endpoints

### `POST /auth/login`

Exchange credentials for an access token.

**Rate limit:** about **2 requests per second per client IP**, after a short initial burst, and 20 per second across all clients. Requests over the limit get `429 Too Many Requests`.

**Request**

```
Content-Type: application/json
```

```json
{ "username": "alice", "password": "alice" }
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `username` | string | yes | Case-insensitive |
| `password` | string | yes | Case-sensitive |

**Response `200 OK`**

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 900
}
```

**Errors**

| Status | When |
|---|---|
| `400 Bad Request` | The body isn't valid JSON |
| `401 Unauthorized` | Unknown user, wrong password, or missing fields. Every case gets the same answer and takes about the same time, so callers can't tell which usernames exist. |
| `415 Unsupported Media Type` | `Content-Type: application/json` header missing |
| `429 Too Many Requests` | Rate limit exceeded |

Error responses have an empty body.

**Example**

```bash
curl -X POST http://localhost:8000/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"alice"}'
```

---

### `GET /api/public`

No authentication.

**Response `200 OK`**

```json
{ "message": "Anyone can see this." }
```

**Example**

```bash
curl http://localhost:8000/api/public
```

---

### `GET /api/me`

Returns the identity in your token. Any valid token works; no role is required.

**Response `200 OK`**

```json
{
  "id": "6f1c2a9e-0000-4000-8000-000000000001",
  "username": "alice",
  "roles": ["reader"]
}
```

| Field | Source claim |
|---|---|
| `id` | `sub` |
| `username` | `preferred_username` |
| `roles` | `roles` |

**Errors:** `401` when the token is missing or invalid.

**Example**

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/me
```

---

### `GET /api/items`

Lists items. Requires the **`reader`** role.

**Response `200 OK`**

```json
[
  { "id": 1, "name": "Oil change",    "price": 49.99 },
  { "id": 2, "name": "Brake pads",    "price": 189.00 },
  { "id": 3, "name": "Tire rotation", "price": 29.99 }
]
```

| Field | Type |
|---|---|
| `id` | integer |
| `name` | string |
| `price` | decimal number |

The data is hard-coded.

**Errors:** `401` when the token is missing or invalid; `403` when it lacks the `reader` role.

**Example**

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/items
```

---

### `GET /api/admin/stats`

Returns admin statistics. Requires the **`admin`** role.

**Response `200 OK`**

```json
{
  "requestedBy": "bob",
  "totalItems": 3,
  "serverTime": "2026-10-12T20:17:30.3990155Z"
}
```

| Field | Type | Notes |
|---|---|---|
| `requestedBy` | string | Username from the token |
| `totalItems` | integer | |
| `serverTime` | string | UTC timestamp in ISO 8601 format |

**Errors:** `401` when the token is missing or invalid; `403` when it lacks the `admin` role (for example, `alice`).

**Example**

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/admin/stats
```

---

## Status codes

| Status | Meaning | Body |
|---|---|---|
| `200` | Success | JSON |
| `400` | Malformed JSON body | empty |
| `401` | Missing, malformed, expired or wrongly signed token, or failed login | empty |
| `403` | Valid token, but missing the required role | empty |
| `404` | No such route on the gateway | `404 page not found` (plain text) |
| `405` | Route exists but not for this method (`Allow` header lists the valid one) | `405 method not allowed` (plain text) |
| `415` | Login called without `Content-Type: application/json` | empty |
| `429` | Login rate limit exceeded | empty |

Successful gateway responses also carry an `X-Krakend: Version 3.0.0` header.

---

## Internal endpoints

The auth service also has endpoints that are only reachable inside the Docker network. The gateway and the demo API use them to fetch the public key that verifies token signatures. Through the gateway they return `404`.

| Path (on `http://auth:8080`) | Returns |
|---|---|
| `GET /.well-known/openid-configuration` | `{"issuer":"http://auth:8080","jwks_uri":"http://auth:8080/.well-known/jwks.json","id_token_signing_alg_values_supported":["RS256"]}` |
| `GET /.well-known/jwks.json` | `{"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"…","n":"…","e":"AQAB"}]}` |

To look at them, ask the auth container itself:

```bash
docker compose exec auth wget -qO- http://localhost:8080/.well-known/jwks.json
```

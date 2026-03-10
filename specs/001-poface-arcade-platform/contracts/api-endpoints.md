# API Endpoint Contracts — PoFace Backend

**Branch**: `001-poface-arcade-platform`  
**Date**: 2026-03-09  
**Base URL**: `/api` (relative to App Service host)  
**Auth Scheme**: Bearer JWT (Microsoft Entra ID) unless noted  
**Content-Type**: `application/json` (request and response) unless noted  
**Error shape**: RFC 7807 `ProblemDetails` — all 4xx/5xx include `type`, `title`, `status`, `detail`, `correlationId`

---

## Auth / Identity

### POST /dev-login

**Auth**: None (available in `Development` / `Testing` environments only; absent in `Production`)  
**Purpose**: Sets test-auth cookies for local browser-based development. Not called by production code.

**Request**:
```json
{
  "userId": "test-user-001",
  "displayName": "Test User"
}
```

**Response `200 OK`**:
```json
{
  "userId": "test-user-001",
  "displayName": "Test User"
}
```
Sets `X-Test-User-Id` and `X-Test-Display-Name` cookies on the response.

**Errors**:
- `403 Forbidden` — if environment is `Production`

---

## Game Session

### POST /api/sessions

**Auth**: Required  
**Purpose**: Start a new game session. Returns the session ID and the ordered sequence of target emotions for the 5 rounds.

**Request**: *(no body)*

**Response `201 Created`**:
```json
{
  "sessionId": "4f7b2c9e1a3d4e5f6b7c8d9e0a1b2c3d",
  "rounds": [
    { "roundNumber": 1, "targetEmotion": "Happiness" },
    { "roundNumber": 2, "targetEmotion": "Surprise" },
    { "roundNumber": 3, "targetEmotion": "Anger" },
    { "roundNumber": 4, "targetEmotion": "Sadness" },
    { "roundNumber": 5, "targetEmotion": "Fear" }
  ]
}
```
`rounds` array is in a **fixed order** per FR-009: Happiness → Surprise → Anger → Sadness → Fear. The order MUST NOT be shuffled — it is the same for every session and every player.

**Errors**:
- `401 Unauthorized` — missing or invalid token

---

### DELETE /api/sessions/{sessionId}

**Auth**: Required (must be the session owner)  
**Purpose**: Discard an in-progress session (user navigates away mid-game). No storage writes occur.

**Response `204 No Content`**

**Errors**:
- `401 Unauthorized`
- `403 Forbidden` — authenticated user is not the session owner
- `404 Not Found` — session not found or already completed

---

## Scoring

### POST /api/sessions/{sessionId}/rounds/{roundNumber}/score

**Auth**: Required (must be the session owner)  
**Content-Type**: `multipart/form-data`  
**Purpose**: Upload the captured JPEG frame for a single round. The backend stores the image to Blob Storage, calls Azure Face API, computes the score with head-pose validation, persists the RoundCapture to Table Storage, and returns the result.

**Request** (multipart/form-data):
- `image` — binary JPEG file, max 500 KB, `Content-Type: image/jpeg`

**Response `200 OK`**:
```json
{
  "roundNumber": 3,
  "targetEmotion": "Anger",
  "score": 7,
  "rawConfidence": 0.74,
  "headPoseValid": true,
  "headPoseYaw": 8.3,
  "headPosePitch": -4.1,
  "faceDetected": true,
  "imageUrl": "https://poface.blob.core.windows.net/poface-captures/user-001/4f7b2c9e.../round-3.jpg"
}
```

If `headPoseValid = false`, `score` is always `0` regardless of `rawConfidence`.  
If `faceDetected = false`, `score = 0`, `rawConfidence = 0.0`, head-pose fields are `null`.

**Errors**:
- `401 Unauthorized`
- `403 Forbidden` — not the session owner
- `404 Not Found` — session or round not found
- `409 Conflict` — round already scored
- `413 Content Too Large` — image exceeds 500 KB
- `415 Unsupported Media Type` — Content-Type is not `image/jpeg`
- `422 Unprocessable Entity` — round number out of range (1–5) or invalid body structure
- `502 Bad Gateway` — Azure Face API call failed (retried once before returning error)

---

### POST /api/sessions/{sessionId}/complete

**Auth**: Required (must be the session owner)  
**Purpose**: Finalize the session after all 5 rounds are scored. Computes total score, determines if this is a new personal best, upserts the LeaderboardEntry if so, persists the GameSession, and marks non-best blobs for lifecycle management.

**Request**: *(no body)*

**Response `200 OK`**:
```json
{
  "sessionId": "4f7b2c9e1a3d4e5f6b7c8d9e0a1b2c3d",
  "totalScore": 38,
  "isPersonalBest": true,
  "recapUrl": "/recap/4f7b2c9e1a3d4e5f6b7c8d9e0a1b2c3d",
  "previousBestScore": 29
}
```

`previousBestScore` is `null` if this is the user's first ever completed session.

**Errors**:
- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found` — session not found
- `409 Conflict` — fewer than 5 rounds scored; cannot complete
- `422 Unprocessable Entity` — session already completed

---

## Leaderboard

### GET /api/leaderboard

**Auth**: None (public)  
**Purpose**: Return the current year's global leaderboard, ordered by `TotalScore` descending.

**Query params**:
- `top` — integer, default `100`, max `500` — number of entries to return

**Response `200 OK`**:
```json
{
  "year": "2026",
  "entries": [
    {
      "rank": 1,
      "userId": "user-abc",
      "displayName": "PoFace Champion",
      "totalScore": 48,
      "deviceType": "Desktop",
      "recapUrl": "/recap/sessionId-here",
      "achievedAt": "2026-03-09T14:32:11Z"
    }
  ],
  "count": 1
}
```

**Errors**: None expected (returns empty `entries` array if no data)

---

## Recap

### GET /api/recap/{sessionId}

**Auth**: None (public — recap pages are shareable)  
**Purpose**: Return all 5 round results and image URLs for a completed session. Used by the `RecapPage` Blazor component.

**Response `200 OK`**:
```json
{
  "sessionId": "4f7b2c9e1a3d4e5f6b7c8d9e0a1b2c3d",
  "userId": "user-abc",
  "displayName": "PoFace Champion",
  "totalScore": 38,
  "isPersonalBest": true,
  "completedAt": "2026-03-09T14:32:11Z",
  "rounds": [
    {
      "roundNumber": 1,
      "targetEmotion": "Happiness",
      "score": 9,
      "headPoseValid": true,
      "imageUrl": "https://poface.blob.core.windows.net/poface-captures/.../round-1.jpg"
    }
  ]
}
```

**Errors**:
- `404 Not Found` — session does not exist
- `410 Gone` — session existed but has expired (non-best, 24-hour TTL elapsed)

---

## Diagnostics

### GET /api/diag

**Auth**: Required  
**Purpose**: Return current health status of all backing services with masked config values.

**Response `200 OK`**:
```json
{
  "version": "1.0.0.0",
  "region": "eastus",
  "timestamp": "2026-03-09T14:45:00Z",
  "services": {
    "faceApi": { "status": "OK" },
    "blobStorage": { "status": "OK" },
    "tableStorage": { "status": "OK" }
  },
  "config": {
    "faceApiKeyMasked": "ABCD****WXYZ",
    "blobAccountName": "poface",
    "appInsightsConnectionMasked": "Instrumentation****"
  }
}
```

If a service check fails:
```json
{
  "services": {
    "faceApi": { "status": "ERROR", "message": "HTTP 503 — upstream unavailable" }
  }
}
```

HTTP status is always `200` (diagnostics themselves do not fail with 5xx so dashboards can always retrieve status).

**Errors**:
- `401 Unauthorized`

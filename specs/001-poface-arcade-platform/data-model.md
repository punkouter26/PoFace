# Data Model: PoFace — Arcade Emotion-Matching Platform

**Branch**: `001-poface-arcade-platform`  
**Date**: 2026-03-09  
**Storage**: Azure Table Storage (leaderboard) + Azure Blob Storage (images)

---

## Entities

### 1. Player

**Represents**: An authenticated Microsoft Account user who has completed at least one session.  
**Storage**: Azure Table Storage — table `Players`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `PartitionKey` | `string` | Required | Fixed value `"Player"` — all players in one partition for simplicity |
| `RowKey` | `string` | Required, unique | `UserId` — the `sub` claim from the MSAL/Entra ID token |
| `DisplayName` | `string` | Required, max 100 chars | Display name from the `name` claim; stored for leaderboard display |
| `CreatedAt` | `DateTimeOffset` | Required | Timestamp of the player's first ever session |
| `LastSeenAt` | `DateTimeOffset` | Required | Updated on every game start; used for activity tracking |

**Relationships**: One Player → zero or many GameSessions; one Player → zero or one LeaderboardEntry.

---

### 2. GameSession

**Represents**: One complete 5-round play attempt by a Player. May or may not be the player's personal best.  
**Storage**: Azure Table Storage — table `GameSessions`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `PartitionKey` | `string` | Required | `UserId` — all sessions for a user in one partition |
| `RowKey` | `string` | Required, unique | `SessionId` — `Guid.NewGuid().ToString("N")` |
| `UserId` | `string` | Required | Denormalized for query convenience |
| `TotalScore` | `int` | Required, 0–50 | Sum of all 5 round scores |
| `IsPersonalBest` | `bool` | Required | `true` if this session replaced a previous leaderboard entry |
| `DeviceType` | `string` | Required, enum: `"Mobile"` \| `"Desktop"` | Detected from `User-Agent` at session start |
| `RecapUrl` | `string` | Required | Fully qualified public URL: `/recap/{SessionId}` |
| `CompletedAt` | `DateTimeOffset` | Required | UTC timestamp when round 5 result was stored |
| `ExpiresAt` | `DateTimeOffset?` | Nullable | Non-null only when `IsPersonalBest = false`; set to `CompletedAt + 24h` |

**State transitions**:
```
[Start] → Active → Completed (IsPersonalBest=true) → [persisted indefinitely]
                  → Completed (IsPersonalBest=false) → [ExpiresAt set; TTL cleanup]
         → Abandoned (navigated away mid-game) → [never written to storage]
```

**Relationships**: One GameSession → exactly 5 RoundCaptures (FK: `SessionId`); one GameSession → zero or one LeaderboardEntry.

---

### 3. RoundCapture

**Represents**: The result of a single round within a GameSession — the captured image, target emotion, score, and pose validity.  
**Storage**: Azure Table Storage — table `RoundCaptures`

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `PartitionKey` | `string` | Required | `SessionId` — all rounds for a session in one partition |
| `RowKey` | `string` | Required, unique | `{SessionId}_{RoundNumber}` e.g., `"abc123_3"` |
| `SessionId` | `string` | Required | Denormalized FK to GameSession |
| `RoundNumber` | `int` | Required, 1–5 | The round sequence number |
| `TargetEmotion` | `string` | Required, enum | One of: `"Happiness"`, `"Surprise"`, `"Anger"`, `"Sadness"`, `"Fear"` |
| `Score` | `int` | Required, 0–10 | Final score after head-pose validation applied |
| `RawConfidence` | `double` | Required, 0.0–1.0 | Raw emotion confidence from Face API before rounding/penalty |
| `HeadPoseYaw` | `double` | Required | Yaw angle in degrees at capture time |
| `HeadPosePitch` | `double` | Required | Pitch angle in degrees at capture time |
| `HeadPoseValid` | `bool` | Required | `false` if `Abs(Yaw) > 20 || Abs(Pitch) > 20` → score forced to 0 |
| `ImageBlobUrl` | `string` | Required | Public Azure Blob Storage URL for the captured image |
| `CapturedAt` | `DateTimeOffset` | Required | UTC timestamp at the shutter moment |

**Relationships**: Many RoundCaptures → one GameSession (via PartitionKey = SessionId).

---

### 4. LeaderboardEntry

**Represents**: A single global leaderboard row — always the highest-scoring GameSession for a given Player. One entry per Player.  
**Storage**: Azure Table Storage — table `Leaderboard`  
**Upsert strategy**: `PartitionKey = Year` (e.g., `"2026"` — supports yearly leaderboard resets if desired), `RowKey = UserId` — guarantees one entry per user per year.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `PartitionKey` | `string` | Required | Year string: `"2026"` |
| `RowKey` | `string` | Required, unique per partition | `UserId` |
| `DisplayName` | `string` | Required, max 100 chars | Copied from Player at upsert time |
| `TotalScore` | `int` | Required, 0–50 | Best-match total score |
| `SessionId` | `string` | Required | FK to the winning GameSession |
| `RecapUrl` | `string` | Required | Public recap URL for the best session |
| `DeviceType` | `string` | Required | `"Mobile"` or `"Desktop"` from the winning session |
| `AchievedAt` | `DateTimeOffset` | Required | Timestamp of the best-match session completion |

**Upsert logic** (Strategy pattern — `BestMatchUpsertStrategy`):
```
1. Read existing entry for (Year, UserId)
2. If none exists → Insert new entry
3. If exists AND newScore > existing.TotalScore → Replace entry with new session data
4. If exists AND newScore <= existing.TotalScore → No-op (do not write)
```

**Relationships**: One LeaderboardEntry → one GameSession (via `SessionId`); one LeaderboardEntry → one Player (via `RowKey = UserId`).

---

### 5. AnalysisResult *(transient — never persisted)*

**Represents**: The response payload from the Azure Face API for a single round capture. Used within the `Scoring` feature slice only; not stored to Table Storage.

| Field | Type | Notes |
|---|---|---|
| `EmotionLabel` | `string` | Mapped from the highest-confidence emotion in `FaceEmotionProperties` |
| `TargetEmotionConfidence` | `double` | Confidence (0.0–1.0) for the target emotion specifically |
| `HeadPoseYaw` | `double` | Degrees |
| `HeadPosePitch` | `double` | Degrees |
| `HeadPoseValid` | `bool` | Computed: `Abs(Yaw) ≤ 20 && Abs(Pitch) ≤ 20` |
| `Score` | `int` | `HeadPoseValid ? Round(TargetEmotionConfidence * 10) : 0` |
| `FaceDetected` | `bool` | `false` if Face API returned zero face detections |

---

### 6. DiagnosticsReport *(transient — never persisted)*

**Represents**: A point-in-time health snapshot returned by the `/api/diag` endpoint. Built on each request by probing the three backing services.

| Field | Type | Notes |
|---|---|---|
| `FaceApiStatus` | `string` | `"OK"` or `"ERROR"` — result of a lightweight ping to Face API |
| `BlobStorageStatus` | `string` | `"OK"` or `"ERROR"` — result of `BlobServiceClient.GetPropertiesAsync()` |
| `TableStorageStatus` | `string` | `"OK"` or `"ERROR"` — result of `TableServiceClient.GetPropertiesAsync()` |
| `FaceApiKeyMasked` | `string` | e.g., `"ABCD****WXYZ"` — first 4 + last 4 chars of the key |
| `BlobConnectionMasked` | `string` | Account name only — connection string never exposed |
| `Version` | `string` | Assembly `FileVersion` of `PoFace.Api` |
| `Region` | `string` | From `WEBSITE_REGION` Azure App Service environment variable |
| `Timestamp` | `DateTimeOffset` | UTC time the report was generated |

---

## Blob Storage Layout

**Container**: `poface-captures` (public blob access — images are directly URL-accessible)

```text
poface-captures/
└── {UserId}/
    └── {SessionId}/
        ├── round-1.jpg   (640×480, JPEG 0.85 or 0.60)
        ├── round-2.jpg
        ├── round-3.jpg
        ├── round-4.jpg
        └── round-5.jpg
```

**Lifecycle policy**:
- Best-match session blobs: retained indefinitely (no policy applied to `IsPersonalBest` folders)
- Non-best-match session blobs: lifecycle rule transitions to `Archive` tier after 1 day; deleted after 2 days (aligns with 24-hour recap TTL)
- All blobs: transition to `Cool` tier after 180 days inactivity; `Archive` tier after 365 days inactivity (cost management for older best-match images)

**Blob name convention**: `{UserId}/{SessionId}/round-{N}.jpg` — no sub-folder ambiguity; blob prefix filtering on `{SessionId}/` retrieves all 5 images for a recap.

---

## Validation Rules

| Entity | Field | Rule | Error |
|---|---|---|---|
| `RoundCapture` | `Score` | 0–10 inclusive | Reject with 422 |
| `RoundCapture` | `RoundNumber` | 1–5 inclusive | Reject with 422 |
| `RoundCapture` | `TargetEmotion` | Must be one of the 5 enum values | Reject with 422 |
| `GameSession` | `TotalScore` | Must equal sum of 5 `RoundCapture.Score` values | Log discrepancy; use computed sum |
| `Player` | `DisplayName` | Non-empty, stripped, max 100 chars | Truncate to 100, do not reject |
| Image upload | Byte length | Max 500 KB (640×480 JPEG at 0.85 never exceeds ~180 KB in practice) | Reject with 413 |
| Image upload | Content-Type | Must be `image/jpeg` | Reject with 415 |

# ProductSpec — PoFace Arcade

## Why

Most emotion-recognition demos are passive — you see a label, not a score. PoFace turns it into a competitive game with a leaderboard and a shareable recap, making the AI feedback loop viscerally human and repeatable. The goal is to be the most expressive human on the leaderboard.

---

## Product Vision

> "Use your face. Beat your score. Prove you feel things."

PoFace is a browser-based arcade game where players race through 5 target emotions using their webcam. Each frame is analyzed by Google Cloud Vision. The score is ruthlessly objective — confidence percentage mapped to 0–10 points per round.

---

## Core User Journeys

| Journey | Entry Point | Auth Required |
|---|---|---|
| Play anonymously | `/` → "Start Game" | No |
| Save score to leaderboard | Play → Complete | Yes (Entra ID) |
| Share game recap | `/recap/{sessionId}` | No (link is public) |
| Check standings | `/leaderboard` | No |
| View service health | `/diagnostics` | No (data needs auth) |

---

## Feature Definitions

### FR-001 — Anonymous Play
Players may complete a full 5-round game without authentication. Score not persisted to leaderboard. Session stored for recap only (1-day expiry unless personal best).

### FR-002 — Authenticated Play & Leaderboard
Authenticated players (Microsoft Entra ID) can have personal-best scores upserted to the year-scoped leaderboard. Only the best score per user per calendar year is retained.

### FR-003 — Round Scoring (0–10)
Each round: upload JPEG → Google Cloud Vision detects face → target emotion likelihood is mapped to a 0–10 score. Head pose validity gate: |yaw| ≤ 20° AND |pitch| ≤ 20°. Invalid head pose → score penalty.

### FR-004 — Fixed Emotion Sequence
Rounds always proceed in canonical order: **Happiness → Surprise → Anger → Sadness → Fear**. Order must not be randomized (leaderboard integrity).

### FR-005 — Game State Machine
Client-side `GameOrchestrator` drives the UI through: `Idle → GetReady (2s) → Countdown (3→2→1) → Capturing → Analyzing → ScoreReveal (2s) → [next round | GameOver]`

### FR-006 — Recap Page
A shareable URL `/recap/{sessionId}` showing round images, per-round targets, scores, and total. Expires after 1 day for non-personal-best sessions (API returns 410 Gone).

### FR-007 — Leaderboard
Top-N (configurable, max 500) entries for the current calendar year, sorted by total score descending. Shows display name, score, device type, date achieved, and recap link.

### FR-008 — Image Constraints
JPEG only. Maximum 500 KB. Rejected with 400 if wrong content-type or too large. Stored in `poface-captures` blob container with lifecycle expiry policy.

### FR-009 — Diagnostics Endpoint
`GET /api/diag` (auth required) returns live health of BlobStorage, TableStorage, and FaceApi (Google Vision) connectivity plus masked config dump. Client polls every 5 s.

---

## Scoring Model

```
Score = Round( RawConfidence × 10 )   ; clamped to [0, 10]
TotalScore = Σ Score[1..5]             ; max 50
```

`RawConfidence` is the normalized likelihood float from Google Vision's `FaceAnnotation` for the target emotion (e.g., `JoyLikelihood`).

---

## Success Metrics

| Metric | Target | Measurement |
|---|---|---|
| Round completion rate | ≥ 80% of started sessions | `IsCompleted / sessions started` in Table Storage |
| Leaderboard submission rate | ≥ 40% of authenticated completions | `LeaderboardEntity` upserts per auth session |
| P95 scoring latency | < 3 s per round | OTel trace on `ScoreRound` span in App Insights |
| Vision API error rate | < 1% | `FaceApiStatus` health check + App Insights failures |
| App availability | 99.9% | App Service health check + App Insights availability |
| Personal-best detection accuracy | 100% | Unit tests on `CompleteSessionCommand` |

---

## Non-Goals

- Multi-player real-time play (no WebSocket/SignalR)
- Mobile app (web only, responsive)
- Custom emotion models (Google Cloud Vision is the sole analyzer)
- Social features (no following, commenting, or friend lists)
- Paid tiers or monetization

---

## Key Constraints

| Constraint | Detail |
|---|---|
| No relational DB | All persistence via Azure Table Storage (schemaless) |
| No Redis cache | Leaderboard is read directly from Table Storage with `top` paging |
| Optional auth | All game endpoints are `AllowAnonymous`; auth gate is only at leaderboard write |
| Single region | App Service + Storage co-located per `azd` environment |
| Image size | 500 KB JPEG hard limit enforced server-side |

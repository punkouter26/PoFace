# Feature Specification: PoFace — Arcade Emotion-Matching Platform

**Feature Branch**: `001-poface-arcade-platform`  
**Created**: 2026-03-09  
**Status**: Draft  
**Input**: Comprehensive architectural and functional blueprint for PoFace

## Clarifications

### Session 2026-03-09

- Q: Who is permitted to access the PoVerify diagnostics page? → A: Any authenticated user (no special admin role required; anonymous access is denied).
- Q: What does the UI show on the frozen-frame overlay while awaiting the analysis engine score (up to 3 s)? → A: Display `ANALYZING…` text/spinner immediately after the shutter; trigger the 0-to-final count-up animation once the score arrives.
- Q: What is the storage policy for images from unauthenticated sessions, and who is allowed to play? → A: Only authenticated users may play; the Arcade Arena MUST require a Microsoft Account login before a session can start. No anonymous sessions exist, so the anonymous image storage question is moot.
- Q: How long is the "GET READY" emotion prompt displayed before the 3-2-1 countdown begins? → A: 2 seconds, fixed and non-skippable.
- Q: Does the leaderboard row show individual round scores or total score only? → A: Total score only on the leaderboard row; individual round scores are exclusively shown in the Recap Gallery.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Play a Complete 5-Round Game Session (Priority: P1)

An authenticated user (logged in via Microsoft Account) opens the app, grants camera access, and plays through all five emotion rounds (Happiness, Surprise, Anger, Sadness, Fear). They see a live camera feed inside the face guide, watch the 3-2-1 countdown per round, experience the shutter flash at capture, view their per-round score on the frozen-frame overlay, and see a final total score at the end.

**Why this priority**: This is the entire value proposition of PoFace. Without a playable game loop, nothing else matters. All other stories are built on top of this confirmed core gameplay.

**Independent Test**: Grant camera access, navigate to the Arcade Arena, and complete all 5 rounds manually. The session ends with a final score screen showing round-by-round scores and a total out of 50. No authentication, leaderboard, or persistence is required for this test to prove the story works.

**Acceptance Scenarios**:

1. **Given** I am on the Arcade Arena page and have granted camera access, **When** I click "Start Game," **Then** the first round's emotion label (">>> HAPPINESS <<<") is displayed for exactly 2 seconds (non-skippable), after which the 3-2-1 countdown begins automatically.
2. **Given** the countdown is running, **When** it reaches 0, **Then** a 150ms full-viewport white flash occurs, the live feed is replaced by the frozen frame of my expression, and an `ANALYZING…` indicator is displayed on the overlay. Once the score is returned by the analysis engine, the indicator is replaced by a count-up animation from 0 to the final score value over 500ms.
3. **Given** a round result is displayed, **When** the 2-second review period elapses, **Then** the screen performs a horizontal slide transition and the next round's emotion prompt appears.
4. **Given** all 5 rounds have been scored, **When** the final round's review period ends, **Then** the game ends, the total score (out of 50) is displayed, and I am offered options to save the session (if authenticated) or view my recap.
5. **Given** my head is turned more than 20 degrees in any direction during the countdown, **When** the shutter fires, **Then** my score for that round is 0 and the UI informs me the capture was penalized due to head angle.

---

### User Story 2 — Authentication and Best Match Leaderboard (Priority: P2)

An authenticated user completes a game session. The system calculates the total score and compares it to any previously stored score for that user. If the new score is higher, the leaderboard record is updated; if not, the old record is preserved. The leaderboard page displays one entry per user (their personal best), ranked globally.

**Why this priority**: Authentication and persistent scoring are what give the game long-term replay value and competitive legitimacy. Without this, each session is ephemeral.

**Independent Test**: Log in via the authentication portal, complete a game session, and verify on the Leaderboard page that your entry appears exactly once showing the highest-ever total. Play a second session with a lower score and verify your leaderboard entry does not change.

**Acceptance Scenarios**:

1. **Given** I am not authenticated, **When** I try to navigate to the Arcade Arena, **Then** I am redirected to the Microsoft Account login page; I CANNOT play without completing authentication. I CAN view the Leaderboard and Recap pages without logging in.
2. **Given** I am authenticated and have no prior score, **When** I complete a game session, **Then** my score is saved to the leaderboard and appears with my display name.
3. **Given** I am authenticated with an existing leaderboard score of 32/50, **When** I complete a session scoring 39/50, **Then** my leaderboard entry updates to 39/50 with the new session's images.
4. **Given** I am authenticated with an existing leaderboard score of 39/50, **When** I complete a session scoring 28/50, **Then** my leaderboard entry remains at 39/50, the new session is discarded from the leaderboard, but a temporary recap link is generated for the lower-scoring session.
5. **Given** I am on the Leaderboard page, **Then** each user appears exactly once; the list is sorted by total score descending; each row shows rank, display name, **total score only** (e.g., "39/50"), device type (📱/💻), and a "View Gallery" link. Individual round scores are NOT shown on this page — they are exclusively available via the Recap Gallery.

---

### User Story 3 — Match Recap Gallery (Priority: P3)

After a game ends, the system generates a publicly accessible recap page with five sections — one per round. Each section shows the actual static snapshot captured during that round, together with the target emotion, the score (0–10), and a timestamp.

**Why this priority**: Shareability of funny or expressive captures drives organic word-of-mouth. Leaderboard entries link directly to this page, so it must be in place for the leaderboard to be fully useful.

**Independent Test**: Complete a session and navigate to the unique recap URL. Verify all five round snapshots render, each with correct emotion label and score. Share the URL with a second browser session (no login) and verify it loads without authentication.

**Acceptance Scenarios**:

1. **Given** a game session has ended, **When** I navigate to the recap URL, **Then** five round panels are displayed in order, each showing the captured image, target emotion name, score (e.g., "7/10"), and capture timestamp.
2. **Given** a recap URL, **When** an unauthenticated user opens it, **Then** all five round images and their metadata are visible without requiring a login.
3. **Given** a leaderboard entry, **When** I click "View Gallery," **Then** I am taken to that user's best-match recap page.
4. **Given** a session that did not beat the user's personal best, **When** completed, **Then** a temporary recap link is still generated and accessible for at least 24 hours.

---

### User Story 4 — Terminal UI & Aesthetic Fidelity (Priority: P4)

Every page of the app renders with the defined "Matrix" terminal aesthetic: Terminal Black (#0a0a0a) background, Neon Green (#00ff00) text and borders, monospaced typography, a scrolling CRT scanline overlay, text flicker micro-animations, and neon glow box-shadows on interactive elements. The face guide within the Arcade Arena pulses red when head alignment is out of range.

**Why this priority**: The aesthetic IS the brand. A technically complete game with a generic UI would fail the stated vision. It is later in priority than gameplay and persistence because it does not block those functionalities from being testable.

**Independent Test**: Load all five pages (Home, Arcade Arena, Leaderboard, Recap, Diagnostics) in a desktop and a mobile browser. Visually verify the color palette, monospaced fonts, CRT scanline effect, and glow pulse on focusable elements. No gameplay required.

**Acceptance Scenarios**:

1. **Given** any page loads, **Then** the background is #0a0a0a, all primary text is #00ff00, and all fonts render as monospaced.
2. **Given** the page is visible, **Then** a low-opacity vertical scanline animation rolls continuously across the screen.
3. **Given** any interactive element (button, link, input) receives focus or hover, **Then** it displays a perceptible neon green glow box-shadow.
4. **Given** the Arcade Arena is active and the analysis engine detects head yaw or pitch exceeding 20 degrees, **When** the guide ellipse is rendered, **Then** it changes color from neon green to red and pulses visually until alignment is corrected.
5. **Given** any page is viewed on a mobile device, **Then** the layout is fully usable with no horizontal scroll, and interactive elements meet minimum touch-target sizes.

---

### User Story 5 — Programmatic Audio and Haptic Feedback (Priority: P5)

All in-game sound effects are generated programmatically via the browser's audio context — no audio files are loaded. Countdown ticks produce short high-frequency square wave blips; a score of 8+ triggers a harmonic success chime; the shutter fires a rapid frequency-sweep burst. On mobile, short vibration feedback accompanies each countdown second and a longer vibration accompanies the capture.

**Why this priority**: Audio enriches the arcade feel significantly but is not required for the game, persistence, or visual identity to function. It is additive.

**Independent Test**: Play a game session with the browser's audio enabled and verify all three sound types fire at the correct moments without loading any external audio files (confirm via browser DevTools network tab). On a mobile device, verify haptic feedback at countdown ticks and capture.

**Acceptance Scenarios**:

1. **Given** a countdown tick occurs (3, 2, or 1), **Then** a short 8-bit blip sound plays and, on mobile, a short vibration fires.
2. **Given** the shutter fires at 0, **Then** a rapid frequency-sweep white noise burst plays; on mobile, a longer vibration fires.
3. **Given** a round score of 8 or higher is revealed, **Then** a harmonic chord chime plays.
4. **Given** a score of 7 or lower is revealed, **Then** no chime plays (no audio for lower scores, only the standard tick/shutter sounds).
5. **Given** the network tab is inspected during a full session, **Then** no audio file requests (.mp3, .wav, .ogg, etc.) are visible.

---

### User Story 6 — System Diagnostics Page (Priority: P6)

A hidden or footer-linked diagnostics page ("PoVerify") displays real-time health indicators for the analysis engine, image storage vault, and database. Sensitive configuration keys are shown with middle characters masked. Environment info (version number, region) is displayed as raw JSON.

**Why this priority**: An operational necessity for administrators, but invisible to end users. Lowest priority since it has no impact on the player experience.

**Independent Test**: Navigate to the diagnostics URL (directly or via footer link). Verify all three service indicators show green/red status, that any visible configuration keys are masked, and that the environment JSON block is present.

**Acceptance Scenarios**:

1. **Given** I am not authenticated, **When** I navigate to the PoVerify URL, **Then** I am redirected to the login page; the diagnostics content is not shown.
2. **Given** I am authenticated (any valid login, no special role), **When** I visit the diagnostics page, **Then** all three indicators (analysis engine, image vault, database) reflect their current live status.
3. **Given** all backing services are running, **When** I view the diagnostics page, **Then** all three indicators show a green status.
4. **Given** a backing service is unreachable, **When** I view the diagnostics page, **Then** the corresponding indicator shows red.
5. **Given** a configuration key is displayed, **Then** the middle characters are masked (e.g., "ABCD****WXYZ") and the raw value is never exposed in the UI.
6. **Given** the diagnostics page loads, **Then** a JSON block shows at minimum the current version number and server region.

---

### Edge Cases

- What happens when the user denies camera permission? The game MUST display a clear error state explaining camera access is required and provide guidance to re-enable it; the countdown MUST NOT start.
- What happens when the analysis engine is unavailable during a round? The round MUST fail gracefully with a score of 0 and a visible error message; the game MUST continue to the next round rather than crash.
- What happens when the user navigates away mid-game? The session is discarded and the home page is displayed; no partial score is persisted.
- What happens when two users have the same total score? They are displayed in descending order by score; ties are broken by recency (most recent submission ranked higher).
- What happens when captured image upload fails? The round score is still returned and displayed; the session proceeds but the recap gallery for that round shows a placeholder image.
- What happens when a mobile device does not support the Vibration API? The haptic sequences are silently skipped; no error is thrown.
- What happens when an unauthenticated user attempts to access the Arcade Arena? They are immediately redirected to the Microsoft Account login page. The game MUST NOT be reachable without a valid session.
- What happens when the user's connection is slow? The system checks upload speed before round 1 and dynamically reduces JPEG capture quality to ensure the frozen frame loads without visible lag.

---

## Glossary

- **Analysis engine**: Throughout this spec, "analysis engine" refers to the Azure AI Vision Face API v1.0.0-beta.1, consumed via `FaceAnalysisService.cs` in the Scoring vertical slice.
- **Image storage vault**: Azure Blob Storage container `poface-captures`.
- **Database**: Azure Table Storage (tables: `Leaderboard`, `GameSessions`, `RoundCaptures`).

---

## Requirements *(mandatory)*

### Functional Requirements

**Home Page**

- **FR-001**: Users MUST authenticate via **Microsoft Account** (Microsoft Identity Platform / MSAL, OIDC flow) to access any gameplay functionality. No other identity provider is supported.
- **FR-002**: Authenticated users MUST see their display name in the navigation or header area.
- **FR-003**: The Home Page MUST include an ASCII-art logo and a "System Status: Active" indicator.
- **FR-004**: The Home Page MUST provide a "How to Play" section explaining the 5-round loop.
- **FR-005**: Users MUST be able to navigate to the Leaderboard and Recap pages without authentication. Navigating to the Arcade Arena MUST require a valid Microsoft Account session; unauthenticated users attempting to access the Arena MUST be redirected to the Microsoft login page.

**Arcade Arena — Core Game Loop**

- **FR-006**: The Arcade Arena MUST provide a mirrored live camera feed inside a centered viewport.
- **FR-007**: A static glowing ellipse face guide MUST be superimposed over the camera feed.
- **FR-008**: If the analysis engine returns a round score of 0 due to head pose penalty (yaw or pitch > 20°), the face guide border MUST visually change to a red pulse warning state **after the score is returned** — this is a post-capture feedback indicator, not a real-time live tracking signal. No client-side head-pose estimation is required.
- **FR-009**: The game MUST progress through exactly 5 hardcoded rounds in this order: Happiness, Surprise, Anger, Sadness, Fear.
- **FR-010**: Each round MUST begin with a "GET READY" prompt displaying the target emotion name (e.g., `>>> ANGER <<<`) for exactly **2 seconds**. The prompt MUST be non-skippable; the 3-2-1 countdown MUST start automatically after the 2-second display.
- **FR-011**: The countdown MUST display 3, 2, 1 as large monospaced numerals centered over the camera feed; each tick MUST be accompanied by an 8-bit blip sound.
- **FR-012**: At countdown 0, the system MUST trigger a 150ms full-viewport white flash and capture a 640×480 pixel still frame from the camera at the exact moment of the flash.
- **FR-013**: The captured frame MUST be sent to the backend analysis service; the resulting score (0–10, integer) MUST be returned and displayed within the frozen-frame overlay.
- **FR-014**: Head pose validation MUST be applied: if yaw or pitch exceeds 20 degrees at capture time, the score MUST be forced to 0 regardless of expression analysis.
- **FR-015**: Immediately after the shutter flash, the frozen-frame overlay MUST display an `ANALYZING…` indicator while the backend score is being fetched. Once the score is returned, the indicator MUST be replaced by a 0-to-final animated count-up over 500ms. The overlay MUST persist for at least 2 seconds from the moment the score is revealed (not from the moment of capture).
- **FR-016**: After 2 seconds, the screen MUST perform a horizontal slide transition to the next round prompt; the transition MUST be automatic with no user interaction required.
- **FR-017**: After round 5 completes, the system MUST display the total score (sum of all round scores, out of 50).

**Analysis Engine**

- **FR-018**: The backend MUST send a "warmup ping" to the analysis engine during round 1's countdown to prevent first-round latency.
- **FR-019**: The analysis engine MUST evaluate the target emotion against the captured image and return a confidence score rounded to the nearest integer 0–10. The score is computed as `Round(targetEmotionConfidence × 10)`. The Azure AI Vision Face API (`Detection_03` model) supplies its own internal normalization which constitutes the de facto smoothing for micro-expressions; **no additional custom smoothing algorithm is required or expected**.
- **FR-020**: The system MUST check the user's upload speed before round 1 and reduce JPEG capture quality if the connection is slow, to ensure timely transmission.

**Leaderboard**

- **FR-021**: The Global Leaderboard MUST display at most one entry per authenticated user, showing their all-time highest total score.
- **FR-022**: Each leaderboard row MUST display: rank, player display name, **total score only** (e.g., "39/50"), device type indicator (📱 for mobile / 💻 for desktop), and a "View Gallery" link. Individual round scores MUST NOT appear on the leaderboard row; they are exclusively shown in the Recap Gallery.
- **FR-023**: The leaderboard MUST be sorted descending by total score; ties MUST be broken by submission recency (most recent first).
- **FR-024**: The "View Gallery" link MUST navigate to the Match Recap page for that specific session.

**Match Recap Gallery**

- **FR-025**: Every completed game session MUST generate a Match Recap page at a unique, publicly accessible URL, regardless of whether it represents the user's best score.
- **FR-026**: The Recap page MUST display five panels in round order, each containing: the round's captured static image, target emotion name, score (e.g., "7/10"), and capture timestamp.
- **FR-027**: The Recap page MUST be accessible without authentication.
- **FR-028**: The Recap page for a session that did not beat the user's personal best MUST remain accessible for at least 24 hours via its temporary URL.

**Data Persistence & Storage**

- **FR-029**: All captured round images MUST be persisted in a storage vault sized for long-term retention.
- **FR-030**: Images inactive for several months MUST be automatically transitioned to an archival storage tier to manage costs; leaderboard and metadata MUST remain in a high-performance access tier.
- **FR-031**: When a new session total score exceeds the user's existing leaderboard record, the existing record (images + scores) MUST be replaced with the new session's data.
- **FR-032**: When a new session total score does not exceed the user's existing record, the new session's images MUST still be stored for the temporary recap link but MUST NOT update the leaderboard entry.
- **FR-033**: The system MUST NOT store PII beyond the display name provided by the identity provider.

**System Diagnostics (PoVerify)**

- **FR-034**: The PoVerify page MUST be accessible only to authenticated users; unauthenticated requests MUST be redirected to the login page. Once authenticated, the page MUST display real-time connectivity indicators (green/red) for: the analysis engine, the image storage vault, and the database.
- **FR-035**: Any configuration keys shown on the diagnostics page MUST be masked, displaying only the first 4 and last 4 characters with the middle replaced by asterisks (e.g., "ABCD****WXYZ").
- **FR-036**: The PoVerify page MUST display a JSON block containing at minimum the application version number and server/deployment region.

**Audio & Haptics**

- **FR-037**: All audio effects MUST be generated programmatically via the browser's audio API — no audio files may be loaded.
- **FR-038**: The countdown tick audio MUST be a short high-frequency square wave; the shutter audio MUST be a rapid frequency-sweep burst; the success chime (score ≥ 8) MUST be a harmonic chord.
- **FR-039**: On supported mobile devices, a short vibration MUST fire on each countdown tick and a longer vibration MUST fire at the shutter moment.

**Cross-Platform**

- **FR-040**: The 640×480 pixel capture resolution MUST be enforced regardless of the user's camera hardware or screen size, ensuring a consistent baseline across all devices.
- **FR-041**: The UI MUST be responsive and fully usable on both mobile and desktop viewport sizes.

---

### Key Entities

- **Player**: An authenticated user identity; stores display name (from identity provider) and unique ID. No additional PII is stored.
- **GameSession**: One complete 5-round play attempt by a player. Holds references to 5 RoundCaptures, a total score, a device type flag, a timestamp, and the public recap URL. May or may not be the player's personal best.
- **RoundCapture**: A single round's result within a GameSession. Holds the target emotion, the captured image reference, the integer score (0–10), the capture timestamp, and head-pose validity flag.
- **LeaderboardEntry**: One row on the global leaderboard — always the highest-scoring GameSession for a given Player. One-to-one with the player's best GameSession.
- **AnalysisResult**: The transient response from the analysis engine per round. Contains the emotion confidence score, head-pose angles (yaw, pitch), and validity flag.
- **DiagnosticsReport**: A transient snapshot of infrastructure health — service connectivity statuses, masked configuration metadata, version, and region.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Players can complete a full 5-round game session — from landing on the Arcade Arena to seeing their total score — in under 90 seconds of total wait time (excluding time the player spends making expressions).
- **SC-002**: The analysis engine returns a score result within 3 seconds of the shutter capture on a standard connection; this round-trip MUST NOT block the frozen-frame display.
- **SC-003**: The leaderboard correctly displays one entry per user with their highest-ever total score in 100% of tested cases — no duplicate entries, no score regressions.
- **SC-004**: All five round captured images render on the Recap page within 4 seconds of the page load for 95% of sessions.
- **SC-005**: The Recap page is publicly accessible (unauthenticated) via its unique URL — verified across at least 3 different browsers/devices.
- **SC-006**: The CRT terminal aesthetic (black background, neon green text, scanline overlay, monospaced fonts) is consistent across all 5 pages on both mobile and desktop viewports.
- **SC-007**: Zero audio file network requests are observed in browser DevTools during a full game session; all audio is confirmed programmatically generated.
- **SC-008**: On a simulated 3G connection (slow upload), the frozen frame MUST appear within 3 seconds of the shutter — confirming the bitrate adaptation mechanism functions correctly.
- **SC-009**: The PoVerify diagnostics page correctly reflects service health state changes within 5 seconds of an actual service going offline or coming back online.
- **SC-010**: The application loads and is ready to play on a mobile device (portrait orientation) with no horizontal scroll and with all interactive elements meeting minimum touch-target sizing guidelines.

---

## Assumptions

- **Microsoft Account (Microsoft Identity Platform) is the sole identity provider.** Authentication uses the MSAL OIDC flow (Azure AD / Entra ID). No other social or enterprise identity provider will be integrated.
- "Several months" for archival tier transition is assumed to be 6 months of inactivity; this can be tuned at configuration time without a spec change.
- "Slow connection" for bitrate adaptation is defined as measured upload speed below 1 Mbps (proxied via `navigator.connection.downlink`), which will reduce JPEG capture quality from **85%** to 60%. Normal-connection quality is 85% (`toDataURL('image/jpeg', 0.85)`).
- The analysis engine is a third-party cognitive service (e.g., a cloud vision API) consumed via the backend; the spec does not constrain which vendor is used.
- "Temporary recap link" persists for 24 hours for non-best sessions; best-match sessions persist indefinitely.
- The face guide ellipse dimensions are sized to comfortably contain a human face at standard webcam distances (approximately 50-80cm from the camera).
- The ASCII art logo on the Home Page will be defined during the UI implementation phase; exact character content is not part of this spec.

---

## Out of Scope

- Multiplayer or head-to-head competition modes.
- User-configured expression sets or custom rounds (the 5 emotions and their order are hardcoded).
- Native mobile apps (iOS/Android); the platform is a web application only.
- Video recording or animated GIF export of sessions.
- Administrative moderation tools for leaderboard management (e.g., banning, score removal).
- Real-time streaming or WebRTC publishing of gameplay.
- Social features such as comments, likes, or friend leaderboards.

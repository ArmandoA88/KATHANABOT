# Quiz and raffle notice alerts

Version 1.0.223 adds **Quiz / raffle notice alerts** at the top of the Quiz tab.
It is enabled by default, including when loading an older profile. Turning the
toggle off is remembered when you close or restart the bot.

Select a Full game window. While the bot is open, the existing capture and local
Windows OCR scanner checks the game client for whole-word `quiz`, `quizzes`,
`quizes`, `raffle`, or `raffles`, without requiring calibration, an API key, an
enabled solver, or a running combat bot. It continues on other tabs. A minimized
or unavailable game window is skipped until it is readable again. Scans are
limited to roughly one every two seconds, with no overlapping notice scans.

Alerts go to the existing **ntfy Channel (Global)** destination in notification
settings, even if other alerts use Discord. Recognition uses no OpenAI or other
remote recognition service. Only the matched event type and timing are sent to
ntfy; screenshots and full-screen OCR text are not included in the notification.

Examples:

- `Quiz starts in 5 minutes` → **Quiz notice**: “Quiz detected on the game screen.
  Time mentioned: starts in 5 minutes.”
- `Raffle at 20:30` → **Raffle notice** with the mentioned clock time. The bot does
  not invent a time zone or convert it to a countdown.
- `Quiz prize: 500 rupiah` → **Quiz notice** with “No time mentioned.”

Time extraction accepts hours, minutes, seconds, abbreviations, written numbers,
and clock-style countdowns. It can use a following OCR line that begins with a
time phrase. It does not borrow a separate event's countdown or an unrelated
line's timer.

An announcement that stays visible sends one alert rather than one on every
scan. A newly readable time can send one follow-up. Repeated countdown changes
are suppressed. A notice rearms after disappearing for at least 30 seconds and
at least two minutes since its previous alert. Failed delivery retries no more
than once a minute while the notice remains visible. Turning the toggle off
cancels pending scanning and delivery; an alert already delivered cannot be
recalled.

The answer solver retains its separate toggle and existing API behavior. Notice
monitoring never clicks answers or sends game input.

Validation: `dotnet run --project tests/QuizSolver.Tests -c Release` exercises
keyword boundaries, time parsing, saved defaults, duplicate suppression, retry,
cancellation, and a generated image processed by the real local OCR engine.
Notification tests use a fake sender and make no live ntfy or AI requests.

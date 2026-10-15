# Quiz and raffle notice alerts

**Quiz / raffle notice alerts** is at the top of the Quiz tab. It is enabled by
default, including when loading an older profile. Turning it off is remembered.

Select a Full game window. The local Windows OCR scanner checks the game client
roughly every two seconds, without overlapping notice scans, an API key, an enabled
answer solver, or a running combat bot. Scanning continues on other tabs. Minimized
or unreadable game windows wait until capture becomes available.

## Raffle trigger (1.0.264)

Raffle notices require this text on the captured game screen:

- `prizes are revealed`
- `prizes are revealed when the timer ends`

Matching ignores case and repeated whitespace. The phrase can wrap across up to
three adjacent OCR rows. The word `raffle` is not required in addition to the
phrase. Ordinary `raffle` / `raffles` announcements and near matches such as
`prizes will be revealed` do not trigger raffle notices.

Quiz notices continue to match whole-word `quiz`, `quizzes`, or `quizes`.

## Character identity and delivery

The raffle notice title and body include the character name, for example:

> Raffle notice - AccountOne
>
> Character: AccountOne. Raffle detected on the game screen. Prizes are revealed
> when the timer ends. No time mentioned.

The name is read above the HP bar in the same screenshot used for detection,
using the existing HP region. A character-bearing title from that selected game
window is a fallback. An active character from a different Full/Lite window is
never used. Generic game titles are not account names. If a raffle is found but
the name is unreadable, the status says it is waiting for a readable character
name; the notice is not marked sent and is checked again on later scans. Check
the HP region or selected window if this persists.

Alerts use **ntfy Channel (Global)** in notification settings, even when other
alerts use Discord. Recognition is local. Only the event, qualifying phrase,
character name and any matched timing are sent; screenshots and full-screen OCR
text are not included.

## Timing and repeated notices

Time extraction accepts hours, minutes, seconds, abbreviations, written numbers,
and clock-style times from the event text or a following OCR row beginning with
a time phrase. It does not invent a timezone or borrow another event's countdown.
`when the timer ends` qualifies the raffle but does not supply a numeric time.

A visible announcement sends one notice per window and character. A newly readable
time can send one follow-up; repeated countdown changes are suppressed. A notice
rearms after disappearing for at least 30 seconds and at least two minutes since
its previous alert. Failed delivery retries at most once per minute while visible.
Turning the toggle off cancels pending scanning and delivery.

The answer solver retains its separate toggle and API behavior. Notice monitoring
never clicks answers or sends game input.

## Validation

`dotnet run --project tests/QuizSolver.Tests -c Release` checks positive/negative
phrase matches, wrapped text, quiz timing, character labels, name lookup, saved
defaults, per-character duplicate suppression, missing-name deferral, retry and
cancellation. Generated images exercise the real local OCR engine. Notification
tests use fake senders and make no live ntfy or AI requests. Live-game recognition
and delivery remain dependent on the actual window rendering and OCR readability.

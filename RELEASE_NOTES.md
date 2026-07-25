# KathanaBot 1.0.74

## What changed

- Added an on-demand phone status button. Set a private ntfy topic in the new "ntfy Channel (On-Demand Request)" field, publish to it from your phone (e.g. a free iOS Shortcut or Android HTTP-shortcut app), and the bot replies to your existing Stats destination within ~20 seconds with character name, on/off, HP%/MP%, EXP%+rate, Rupiahs+rate, and the mob currently being attacked. Nothing is sent unless you trigger it.
- Fixed the on-demand request being missed on the very first press after setting up the topic - it now compares each message's own timestamp instead of blindly ignoring everything seen on the first check.
- Fixed repeated notification loops when the Stats destination and the On-Demand Request topic are the same (or otherwise echo back to each other): the bot now ignores any message titled "KathanaBot ..." when checking for a button press, since that's always its own reply, never a real request.
- Added backoff for ntfy.sh's free-tier rate limit: on-demand polling slowed to every 20 seconds, and if a 429 (too many requests) is still hit, polling pauses for 5 minutes and resumes automatically instead of retrying immediately and re-triggering the same error.

## Recent change history - last 5

1. **On-demand phone status button:** press a button on your phone, get an instant character/HP/MP/EXP/Rupiahs/target report - no periodic polling, only when you ask.
2. **First-press reliability fix:** the very first message on a freshly configured request topic is no longer silently dropped as "baseline."
3. **Self-reply loop fix:** using the same topic for requests and stats replies can no longer cause the bot to keep re-triggering itself.
4. **Rate-limit friendly:** polling cadence and automatic backoff keep the feature working within ntfy.sh's free-tier limits instead of erroring out.
5. **Fresh standalone build:** rebuilt and versioned for this release.

# Quiz answers and online lookup

The Quiz tab now distinguishes three kinds of question:

| Question | Behavior |
| --- | --- |
| Kathana / Tantra Online game facts | Search online before answering. Require supporting evidence, a URL actually returned by search, and confidence of at least 75%. Skip unresolved or conflicting answers. |
| General knowledge | Answer directly at 85% confidence or higher; use search for uncertain, obscure, or time-sensitive facts. Sourced answers require at least 75% confidence. |
| Personal trivia about GMs/admins | Allow a best-effort choice among visible answers and label it **GM guess** unless supported by web evidence. Game mechanics or event rules mentioning a GM still require a game-fact lookup. |

The prompt prioritizes the [Kathana wiki](https://kathana.gitbook.io/wiki) and official Kathana announcements, then relevant Tantra guides. It explicitly warns against using another private server's custom facts for Kathana or confusing game terminology with religious Tantra.

## Speed and request limits

The default remains `gpt-5.4-mini`; the existing `gpt-5-mini` selection is preserved. Both now use low reasoning rather than the old `none` setting. The response budget is 1,600 tokens, including reasoning and structured answer output; the previous 220-token limit left very little room to assess evidence.

The normal path uses a single Responses API request with both existing images and the hosted `web_search` tool. Search context is `low`, with at most two tool actions. The model can avoid searches for straightforward general knowledge and personal GM guesses. If it returns a game answer without executing search, one additional request makes search mandatory. There is no silent fallback to an unsourced game guess.

All requests in one solve share a 30-second client deadline, including the existing fallback from Priority to standard processing if Priority is unavailable. A timeout, unsupported search, rate limit, incomplete response, or unresolved answer causes no click. Automatic retries wait 15 seconds; **Solve Now** can explicitly retry sooner. The local preview continues refreshing during requests and retry delays.

Screen-refresh ticks do not themselves call OpenAI. Actual web searches have additional tool charges plus model token usage; see [OpenAI web search documentation](https://developers.openai.com/api/docs/guides/tools-web-search) and [API pricing](https://developers.openai.com/api/docs/pricing).

## Evidence and clicking

- The parser checks completed API output and matches the selected source URL against actual search-source metadata or URL citations. Merely putting a URL in model-generated JSON is insufficient.
- The current answer and saved history show the answer method and a clickable source. The answer cell's tooltip includes the short supporting explanation. A matched source URL establishes provenance, not a guarantee that the model interpreted the page correctly.
- Historical records retain their previous guess flag and are labeled **Legacy** when no method was saved. They are not reused as verified answers.
- Random button fallback and approximate grid fallback are removed. Every answer, including GM guesses, must identify a valid detected button.
- Before clicking, the solver checks the selected process and client size and compares the current quiz and answer images against the original. The existing foreground 10-click burst remains. Image hashes are approximate change detection, not proof of identical text.
- Quiz may remain enabled while RESU is running. Both input paths run through the WinForms UI thread, so each synchronous click/key operation completes before another UI input operation begins.
- The API-key dialog explains that calibrated quiz images go to OpenAI and question text can be used for web searches. Keys remain encrypted using the existing Windows-account storage.

## Offline verification

Run:

```powershell
dotnet run --project tests/QuizSolver.Tests/QuizSolver.Tests.vbproj
```

The test transport never reaches the network. Tests cover the evidence policy, GM-only guesses, invalid mappings, unsafe/invented URLs, response parsing, mandatory-search retry, Priority fallback, rate limits, unsupported tools, and cancellation. Live API behavior, actual quiz accuracy, and in-game timing still need an in-game check.

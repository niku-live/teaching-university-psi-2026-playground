# Walkthrough 11: UI/UX in Practice - Seven Small Fixes to One Page

This is the step-by-step walkthrough behind [Lecture 11](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture11). It takes the [UI/UX theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/10-ui-ux.qmd) and applies it to one real page: the StudySpot "Study Sessions" page in React. Every fix starts from a problem the page really had, names the principle from the theory lecture, changes the code, and proves the change with a Jest test that fails before the fix and passes after it. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-10.md](WALKTHROUGH-10.md) (or any earlier one from [WALKTHROUGH-08.md](WALKTHROUGH-08.md) on): a React page that calls your API, and Jest + React Testing Library tests in `CoolApp.Client/tests/`. Nothing on the backend changes. Apply the same ideas to **your own team's project**: your main page will have its own versions of these problems.

**Every step is independent.** Each one starts from the Lecture 10 code, changes only its own lines, and adds its own test file in `CoolApp.Client/tests/ux/`. You can do one, skip one, or do them in any order. The project builds and every test passes after each one. In this repository each step is also its own commit, so its diff can be read on its own.

The steps come in **two stages**, following the theory lecture's action cycle: after an action, a person asks two questions, "did it work?" and "what do I do next?".

1. **Stage 1 - Did it work? (steps 1-4):** the gulf of evaluation. Waiting, failures and errors must be visible, and every error must say what happened.
2. **Stage 2 - What next? (steps 5-7):** the gulf of execution and the end of the task. Show the person where to act, and give every task a clear ending.

| Step | Problem on the page before the fix | Principle (theory lecture) | Test file |
|---|---|---|---|
| 1 | A failed load shows "Loading..." forever | Every request has four states (9.1-9.3) | `tests/ux/01-loading-states.test.js` |
| 2 | Creating a session offline hangs on "Creating..." | Never fail silently (9.2) | `tests/ux/02-create-offline.test.js` |
| 3 | Every failure says "Please try again" | One message per cause (10.1-10.3) | `tests/ux/03-error-messages.test.js` |
| 4 | A failed rating does nothing | Visibility of system status (9.2, 12.1) | `tests/ux/04-rating-failure.test.js` |
| 5 | The error is far from where the user looks | Feedback where the eyes already are (8.1-8.2) | `tests/ux/05-focus-invalid-field.test.js` |
| 6 | A successful create only empties the form | Design the end (11.1-11.2) | `tests/ux/06-create-success.test.js` |
| 7 | An empty filter result is a blank table | No clear next step (5.3) | `tests/ux/07-empty-list.test.js` |

All the changes are in `CoolApp.Client/src/components/StudySessions.js`. Run the tests after each step:

```bash
cd CoolApp.Client
npm test                                          # every test
npm test -- --testPathPattern=01-loading-states   # only one step's test file
```

> 💡 **How to see the problems in the browser.** Run the app (`dotnet run --project CoolApp.Api`) and open the browser's developer tools (F12), **Network** tab. The throttling list there has **Offline** (no connection at all) and **Slow 4G** (slow answers). To make the API fail, stop it with Ctrl+C while the page is open.

# Stage 1 - Did It Work?

## 1. The Session List Has Four Loading States

**The problem.** `populateStudySessions()` never handles a failure. If the API is down, `fetch` throws, nothing catches it, and the page shows "Loading..." forever. If the API answers `500`, the code still calls `response.json()` and the list breaks. A slow answer looks exactly like a broken one.

**See it:** stop the API, then reload the page. Or pick **Slow 4G** in the Network tab: nothing tells you that the page is still working.

**The principle.** Theory slides 9.1-9.3: a request has four states, not two. These are waiting (under 1 s), slow (over 1 s), failed and done. The happy path is only one of them, and the other three are either designed or left to chance.

**The change.** Replace `populateStudySessions()` so it reports a slow answer after 1 second and catches every failure:

```js
  async populateStudySessions(filters = this.state.filters) {
    const query = StudySessions.buildSessionsQuery(filters);
    // UX fix 1: a request has four states - waiting, slow (over 1 s), failed and done.
    // Before, only "waiting" and "done" existed: a failed request left "Loading..." on screen forever.
    const slowTimer = setTimeout(() => this.setState({ slow: true }), 1000);
    try {
      const response = await fetch(`api/studysessions${query}`);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      const data = await response.json();
      this.setState({ sessions: data, loading: false, loadError: null });
    } catch {
      this.setState({ loading: false, loadError: 'Could not load the study sessions. Check your connection and try again.' });
    } finally {
      clearTimeout(slowTimer);
      this.setState({ slow: false });
    }
  }
```

Show the new states in `render()`. The `contents` expression gets two more branches between "loading" and the table:

```js
    let contents = this.state.loading
      // UX fix 1: "Still loading..." after 1 s, and a failure that says so, with a way out.
      ? <p><em>{this.state.slow ? 'Still loading...' : 'Loading...'}</em></p>
      : this.state.loadError
        ? (
          <div className="alert alert-danger" role="alert">
            {this.state.loadError}{' '}
            <button className="btn btn-link p-0 align-baseline" type="button" onClick={() => this.retryLoading()}>Try again</button>
          </div>
        )
      : StudySessions.renderSessionsTable(this.state.sessions, this.rateSession);
```

And add the way out, a method next to the filter handlers (above `handleFilterChange`):

```js
  // UX fix 1: the way out of a failed load.
  async retryLoading() {
    this.setState({ loading: true, loadError: null });
    await this.populateStudySessions();
  }
```

`slow` and `loadError` are not in the constructor's initial state. A missing state field is `undefined`, which counts as "no", so the page starts in the right state anyway. You can add them there too (`slow: false, loadError: null`) if you prefer to see the whole state in one place.

**The test** (`tests/ux/01-loading-states.test.js`) uses Jest's fake timers to jump 1 second ahead without waiting:

```js
  it('says "Still loading..." when the list takes longer than 1 second', () => {
    jest.useFakeTimers();
    global.fetch = jest.fn(() => new Promise(() => {})); // the server never answers

    render(<StudySessions />);
    expect(screen.getByText('Loading...')).toBeTruthy();

    act(() => jest.advanceTimersByTime(1000));

    expect(screen.getByText('Still loading...')).toBeTruthy();
  });
```

The file also checks that a network error and a `500` both show the error, and that "Try again" loads the list.

## 2. Creating a Session Without a Connection

**The problem.** `fetch` does not throw for `400` or `500`: those are answers. It only rejects when **no answer** came back: no connection, the server is down, or DNS failed. `handleSubmit()` never expected that, so the error escapes, `submitting` stays `true`, and the button says "Creating..." forever. The person cannot tell whether the session was created.

**See it:** open the page, switch the Network tab to **Offline**, fill in the form and press "Create session".

**The principle.** Theory slide 9.2, silent waiting and silent failure: never fail silently. And the slide 10.3 table: for "No connection", the message is "Could not reach the server. Your answers are kept."

**The change.** In `handleSubmit()`, turn a rejected `fetch` into `null` and handle it right after the call:

```js
    const response = await fetch('api/studysessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    }).catch(() => null); // UX fix 2: fetch rejects only when no answer came back at all (offline, server down)

    // UX fix 2: before, this case threw, nothing caught it, and the button said "Creating..." forever.
    if (!response) {
      this.setState({ submitting: false, error: 'Could not reach the server. Your answers are kept, so check your connection and try again.' });
      return;
    }
```

The form state is not touched, so everything typed stays in the fields.

**The test** (`tests/ux/02-create-offline.test.js`) makes the POST reject and checks three things. The message is shown, the button can be pressed again, and the typed topic is still there:

```js
    global.fetch.mockImplementationOnce(() => Promise.reject(new TypeError('Failed to fetch')));

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(await screen.findByText(/Could not reach the server/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create session' }).disabled).toBe(false);
    expect(screen.getByLabelText('Topic').value).toBe('Testing');
```

## 3. One Message per Cause of a Failed Create

**The problem.** Apart from `400`, every failure gets the same message: "Could not create the study session. Please try again." Trying again cannot help with every cause. A server bug (`500`) fails again, and an outdated page (`404`, the endpoint moved) needs a reload, not a retry.

**The principle.** Theory slides 10.1-10.3: a useful message names what happened, says what it means for the person, and offers the next step. Slide 10.4, "the API limits the screen": the page can only tell causes apart because the API returns a different status code for each one. Your own exception handler from [WALKTHROUGH-09.md](WALKTHROUGH-09.md) (`409 Conflict`) is one of them.

**The change.** Add a static method next to the other helpers (above `buildSessionsQuery`):

```js
  // UX fix 3: one message per cause. "Please try again" cannot help with every failure,
  // so say what happened, what it means for the user, and what to do next.
  static describeCreateFailure(status) {
    if (status === 409) {
      return 'Someone changed the same data at the same time. Reload the list and try again.';
    }
    if (status === 429 || status === 503) {
      return 'The server is busy right now. Your answers are kept, so try again in a minute.';
    }
    if (status >= 500) {
      return 'Something went wrong on our side, not in your answers. They are kept, so try again later.';
    }
    return `The server could not accept this request (HTTP ${status}). Reload the page and try again.`;
  }
```

Use it in `handleSubmit()`, in place of the fixed message:

```js
      } else {
        this.setState({ submitting: false, error: StudySessions.describeCreateFailure(response.status) });
      }
```

**The test** (`tests/ux/03-error-messages.test.js`) checks the method alone, one row per status code, with `it.each`, and once on the page:

```js
  it.each([
    [500, 'Something went wrong on our side'],
    [503, 'The server is busy right now'],
    [409, 'Someone changed the same data at the same time'],
    [404, 'Reload the page and try again']
  ])('HTTP %i gets its own message', (status, message) => {
    expect(StudySessions.describeCreateFailure(status)).toContain(message);
  });
```

## 4. A Failed Rating Is Not Silent

**The problem.** `rateSession()` only does something when the `PUT` succeeds. If the session was deleted in the meantime (`404`) or the connection is gone, nothing happens. Worse, the select keeps showing the new number, so the person believes the rating was saved.

**See it:** open the page, delete one of the sessions with the `DELETE` request in `CoolApp.http`, then rate it on the page (without reloading). Or rate a session while **Offline**.

**The principle.** Visibility of system status, the first of the ten heuristics (slide 12.1), and slide 9.2 again: never fail silently.

**The change.** In `rateSession()`, handle all three outcomes:

```js
    const response = await fetch(`api/studysessions/${id}/rating`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ value: Number(value) })
    }).catch(() => null); // UX fix 4: no answer at all - offline, or the server is down

    // UX fix 4: before, a failed rating did nothing at all, while the select kept showing the new value.
    if (response && response.ok) {
      this.setState({ ratingMessage: null });
      await this.populateStudySessions();
    } else if (response && response.status === 404) {
      this.setState({ ratingMessage: 'That session no longer exists, so it was not rated. The list has been refreshed.' });
      await this.populateStudySessions();
    } else {
      this.setState({ ratingMessage: 'The rating was not saved. Check your connection and pick it again.' });
    }
```

Show the message above the table, in `render()`:

```js
        {/* UX fix 4: a failed rating is reported where the list is, not silently ignored. */}
        {this.state.ratingMessage && <div className="alert alert-warning" role="alert">{this.state.ratingMessage}</div>}
        <div className="study-sessions-table">
          {contents}
        </div>
```

**The test** (`tests/ux/04-rating-failure.test.js`) answers the `PUT` with `404` and checks the message, and that the deleted session left the list. It also checks the offline case:

```js
    global.fetch
      .mockImplementationOnce(() => json(404, { title: 'Not Found' })) // the PUT
      .mockImplementationOnce(() => json(200, []));                    // the list, without the deleted session

    fireEvent.change(screen.getByLabelText('Rate "Pull request etiquette"'), { target: { value: '5' } });

    expect((await screen.findByRole('alert')).textContent).toContain('That session no longer exists');
```

# Stage 2 - What Next?

## 5. Focus Moves to the First Invalid Field

**The problem.** After a failed submit, the person is looking at the "Create session" button. The red messages appear next to fields higher up, sometimes off screen. The person has to search for them, and then click into the field.

**See it:** fill in only "Course" and press "Create session".

**The principle.** Theory slides 8.1-8.2: attention is narrow, so give feedback where the eyes already are. In the theory's example, every wrong word is marked and the cursor moves to the word to fix.

**The change.** Add a static method below `validate()`:

```js
  // UX fix 5: feedback where the eyes already are. After a failed submit the user looks
  // at the button; the cursor goes to the first field that needs fixing (in form order).
  static focusFirstInvalidField(errors) {
    const firstInvalid = Object.keys(emptyForm).find(name => errors[name]);
    if (firstInvalid) {
      document.getElementById(firstInvalid)?.focus();
    }
  }
```

`Object.keys(emptyForm)` lists the fields in the same order as the form, and each input's `id` is its field name. Call the method after the errors are rendered: `setState` takes a callback that runs after the update. Do it for the browser-side check:

```js
    if (Object.keys(fieldErrors).length > 0) {
      this.setState({ fieldErrors, error: null }, () => StudySessions.focusFirstInvalidField(fieldErrors));
      return;
    }
```

And for the API's `400`:

```js
      if (response.status === 400) {
        const problemDetails = await response.json();
        const serverErrors = StudySessions.mapServerErrors(problemDetails);
        this.setState({ submitting: false, fieldErrors: serverErrors }, () => StudySessions.focusFirstInvalidField(serverErrors));
      }
```

**The test** (`tests/ux/05-focus-invalid-field.test.js`) checks `document.activeElement`, the element that has the focus:

```js
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    // Course is filled in, so Topic is the first field to fix.
    expect(document.activeElement).toBe(screen.getByLabelText('Topic'));
```

## 6. Creating a Session Has a Clear Ending

**The problem.** After a successful create, the form empties and that's all. Was it created? Where is it? The new row appears somewhere in the table, and nothing points to it.

**The principle.** Theory slides 11.1-11.2, the peak and the end: an experience is judged by its most intense moment and by its end. Design the end, and show the next step.

**The change.** The API already returns the created session (`201 Created` with the body). Read it, keep it in the state, and move the cursor back to the first field for the next session. At the end of `handleSubmit()`:

```js
    // UX fix 6: before, a successful create only emptied the form - did it work?
    const created = await response.json();
    this.setState({ form: { ...emptyForm }, submitting: false, fieldErrors: {}, created });
    await this.populateStudySessions();
    // Ready for the next session: the cursor goes back to the first field.
    document.getElementById('course')?.focus();
  }
```

Clear it when a new submit starts:

```js
    this.setState({ submitting: true, error: null, fieldErrors: {}, created: null });
```

Confirm it under the form:

```js
          <button className="btn btn-primary" type="submit" disabled={this.state.submitting}>
            {this.state.submitting ? 'Creating...' : 'Create session'}
          </button>
        </form>
        {/* UX fix 6: the end of the task is designed too - what happened, and where to look. */}
        {this.state.created && (
          <p className="text-success" role="status">
            Session "{this.state.created.topic}" created. It is highlighted in the list above.
          </p>
        )}
```

And highlight the row. `renderSessionsTable` gets the id to highlight as a third argument, and `table-success` is Bootstrap's green row:

```js
  static renderSessionsTable(sessions, onRate, highlightId) {
...
            <tr key={session.id} className={session.id === highlightId ? 'table-success' : undefined}>
...
      : StudySessions.renderSessionsTable(this.state.sessions, this.rateSession, this.state.created?.id);
```

**The test** (`tests/ux/06-create-success.test.js`) answers the `POST` with `201` and checks the message, the row and the focus:

```js
    expect((await screen.findByRole('status')).textContent).toContain('Session "Testing" created');
    expect((await screen.findByText('Testing')).closest('tr').className).toContain('table-success');
    expect(document.activeElement).toBe(screen.getByLabelText('Course'));
```

## 7. An Empty List Says Why It Is Empty

**The problem.** When no session matches the filters, the page shows a table header with no rows. Is the list empty, still loading, or broken? And what now?

**See it:** type `Quantum` in "Course contains" and press "Apply filters".

**The principle.** Theory slide 5.3, no clear next step. A screen should always say what to do next, and an empty state is a screen too.

**The change.** Under the table, in `render()`, say why the list is empty and offer the next step. If filters are active, the next step is to clear them. If there are no sessions at all, it is to host one:

```js
        <div className="study-sessions-table">
          {contents}
          {/* UX fix 7: an empty result says why it is empty, and offers the next step. */}
          {!this.state.loading && !this.state.loadError && this.state.sessions.length === 0 && (
            Object.values(this.state.filters).some(value => value !== '')
              ? (
                <p>
                  No study sessions match these filters.{' '}
                  <button className="btn btn-link p-0 align-baseline" type="button" onClick={this.handleFilterClear}>Clear filters</button>
                </p>
              )
              : <p>No study sessions yet. Host the first one below.</p>
          )}
        </div>
```

`!this.state.loadError` keeps the message away while step 1's error is shown. Without step 1 that field is always `undefined`, so the condition still works.

**The test** (`tests/ux/07-empty-list.test.js`) applies a filter that matches nothing, presses "Clear filters", and checks that the list is fetched again without the filter:

```js
    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }));

    expect(await screen.findByText('No study sessions yet. Host the first one below.')).toBeTruthy();
    expect(global.fetch).toHaveBeenLastCalledWith('api/studysessions');
```

# Optional - Measure It

## 8. Lighthouse and the Core Web Vitals (optional)

Theory slide 14.4: performance is user experience, and the browser can measure it. Open the page in Chrome, then press F12 and choose **Lighthouse**, then **Analyze page load**. Look at the three Core Web Vitals:

| Metric | What the person notices | Good |
|---|---|---|
| LCP, Largest Contentful Paint | When the main content is there | ≤ 2.5 s |
| INP, Interaction to Next Paint | How fast the page reacts to a click or a key | ≤ 200 ms |
| CLS, Cumulative Layout Shift | Whether things jump while being read | ≤ 0.1 |

The React template already measures them: `src/reportWebVitals.js` uses the `web-vitals` package. Pass `console.log` to it in `src/index.js` (`reportWebVitals(console.log)`) to see the values in the console. Measure the production build (`npm run build`), not `npm start`, because the development server is much slower than what users get.

The **Accessibility** part of the report is an automatic check. Theory slide 15.2 warns about it: it finds what is broken, not what is unusable.

## For Your Project

The theory lecture's task for the team project (slide 16.2):

1. Walk the **main task** of your product through the action cycle. Where can a person fail to *do* it? Where can they fail to *see what happened*?
2. Fix **three** problems. For each one, write down the stage, the principle, and a screenshot before and after. The seven steps above are examples of exactly that.
3. Count **one metric** of the product from what users do, not from what they say (for example, the completion rate of your main task).

Ask about each request your page sends: what does the person see while waiting, when it is slow, when it fails, and when it is done?

## Next Steps

Update `ROADMAP.md` and `README.md`'s "Current Examples". Still open: end-to-end tests with Playwright, which run the React app in a real browser against the running API. They would check the same flows as steps 1-7 without mocking `fetch`.

See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.

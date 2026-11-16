import React, { Component } from 'react';
import './StudySessions.css';

const emptyForm = {
  course: '',
  topic: '',
  location: '',
  startsAt: '',
  hostName: '',
  seatsAvailable: 1
};

const emptyFilters = {
  course: '',
  minSeatsAvailable: '',
  asOf: ''
};

export class StudySessions extends Component {
  static displayName = StudySessions.name;

  constructor(props) {
    super(props);
    this.state = { sessions: [], loading: true, form: { ...emptyForm }, filters: { ...emptyFilters }, submitting: false, error: null, fieldErrors: {} };

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
    this.rateSession = this.rateSession.bind(this);
    this.handleFilterChange = this.handleFilterChange.bind(this);
    this.handleFilterSubmit = this.handleFilterSubmit.bind(this);
    this.handleFilterClear = this.handleFilterClear.bind(this);
  }

  componentDidMount() {
    this.populateStudySessions();
  }

  // Client-side validation: catches obvious mistakes instantly, without a round trip to the API.
  // It is a convenience for the user, not a security boundary - the API validates the same data again itself.
  static validate(form) {
    const errors = {};

    if (!form.course.trim()) errors.course = 'Course is required.';
    if (!form.topic.trim()) errors.topic = 'Topic is required.';
    if (!form.location.trim()) errors.location = 'Location is required.';
    if (!form.hostName.trim()) errors.hostName = 'Host name is required.';

    if (!form.startsAt) {
      errors.startsAt = 'Starts at is required.';
    } else if (new Date(form.startsAt) <= new Date()) {
      errors.startsAt = 'Starts at must be in the future.';
    }

    if (form.seatsAvailable === '' || form.seatsAvailable < 0) {
      errors.seatsAvailable = 'Seats available cannot be negative.';
    }

    return errors;
  }

  // UX fix 5: feedback where the eyes already are. After a failed submit the user looks
  // at the button; the cursor goes to the first field that needs fixing (in form order).
  static focusFirstInvalidField(errors) {
    const firstInvalid = Object.keys(emptyForm).find(name => errors[name]);
    if (firstInvalid) {
      document.getElementById(firstInvalid)?.focus();
    }
  }

  // Maps ASP.NET's ValidationProblemDetails ("errors": { "PropertyName": ["message"] })
  // to the same shape as validate() above, so both can drive the same error display.
  static mapServerErrors(problemDetails) {
    const errors = {};
    if (problemDetails && problemDetails.errors) {
      for (const [key, messages] of Object.entries(problemDetails.errors)) {
        const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
        errors[fieldName] = messages[0];
      }
    }
    return errors;
  }

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

  // Mirrors GetAll's own three query parameters exactly - course, minSeatsAvailable,
  // and asOf are all optional, so an empty filter just means "omit it" here too.
  static buildSessionsQuery(filters) {
    const params = new URLSearchParams();

    if (filters.course.trim()) {
      params.set('course', filters.course.trim());
    }

    if (filters.minSeatsAvailable !== '') {
      params.set('minSeatsAvailable', filters.minSeatsAvailable);
    }

    if (filters.asOf) {
      // Same reasoning as the create-session form: a bare datetime-local value has no
      // timezone, so convert it to an explicit UTC instant before it becomes a query
      // param, rather than leaving the server to guess.
      params.set('asOf', new Date(filters.asOf).toISOString());
    }

    const query = params.toString();
    return query ? `?${query}` : '';
  }

  static renderSessionsTable(sessions, onRate, highlightId) {
    return (
      <table className="table table-striped" aria-labelledby="tableLabel">
        <thead>
          <tr>
            <th>Course</th>
            <th>Topic</th>
            <th>Location</th>
            <th>Starts at</th>
            <th>Host</th>
            <th>Seats left</th>
            <th>Status</th>
            <th>Rating</th>
          </tr>
        </thead>
        <tbody>
          {sessions.map(session =>
            <tr key={session.id} className={session.id === highlightId ? 'table-success' : undefined}>
              <td>{session.course}</td>
              <td>{session.topic}</td>
              <td>{session.location}</td>
              <td>{new Date(session.startsAt).toLocaleString()}</td>
              <td>{session.hostName}</td>
              <td>{session.seatsAvailable}</td>
              <td>{session.status}</td>
              <td>
                {session.hostRating ? `${session.hostRating.value}/5` : 'Not rated'}
                {' '}
                <select
                  aria-label={`Rate "${session.topic}"`}
                  defaultValue=""
                  onChange={event => onRate(session.id, event.target.value)}
                >
                  <option value="" disabled>Rate...</option>
                  <option value="1">1</option>
                  <option value="2">2</option>
                  <option value="3">3</option>
                  <option value="4">4</option>
                  <option value="5">5</option>
                </select>
              </td>
            </tr>
          )}
        </tbody>
      </table>
    );
  }

  renderField(name, label, { type = 'text', extraProps = {} } = {}) {
    const error = this.state.fieldErrors[name];
    return (
      <div className="study-session-form__field mb-3">
        <label className="form-label" htmlFor={name}>{label}</label>
        <input
          className={`form-control${error ? ' is-invalid' : ''}`}
          type={type}
          id={name}
          name={name}
          value={this.state.form[name]}
          onChange={this.handleChange}
          {...extraProps}
        />
        {error && <div className="invalid-feedback">{error}</div>}
      </div>
    );
  }

  render() {
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
      : StudySessions.renderSessionsTable(this.state.sessions, this.rateSession, this.state.created?.id);

    return (
      <div>
        <h1 id="tableLabel">Study Sessions</h1>
        <p>Find a study session hosted by another student, or open a pull request to add your own.</p>

        <form className="study-session-filters mb-3" onSubmit={this.handleFilterSubmit}>
          <div className="row g-2 align-items-end">
            <div className="col-auto">
              <label className="form-label" htmlFor="filter-course">Course contains</label>
              <input
                className="form-control"
                id="filter-course"
                name="course"
                value={this.state.filters.course}
                onChange={this.handleFilterChange}
              />
            </div>
            <div className="col-auto">
              <label className="form-label" htmlFor="filter-minSeatsAvailable">Min seats available</label>
              <input
                className="form-control"
                id="filter-minSeatsAvailable"
                name="minSeatsAvailable"
                type="number"
                min="0"
                value={this.state.filters.minSeatsAvailable}
                onChange={this.handleFilterChange}
              />
            </div>
            <div className="col-auto">
              <label className="form-label" htmlFor="filter-asOf">Upcoming as of</label>
              <input
                className="form-control"
                id="filter-asOf"
                name="asOf"
                type="datetime-local"
                value={this.state.filters.asOf}
                onChange={this.handleFilterChange}
              />
            </div>
            <div className="col-auto">
              <button className="btn btn-secondary" type="submit">Apply filters</button>
            </div>
            <div className="col-auto">
              <button className="btn btn-link" type="button" onClick={this.handleFilterClear}>Clear</button>
            </div>
          </div>
        </form>

        {/* UX fix 4: a failed rating is reported where the list is, not silently ignored. */}
        {this.state.ratingMessage && <div className="alert alert-warning" role="alert">{this.state.ratingMessage}</div>}
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

        <h2>Host a new study session</h2>
        {this.state.error && <p className="text-danger">{this.state.error}</p>}
        <form className="study-session-form" onSubmit={this.handleSubmit} noValidate>
          {this.renderField('course', 'Course')}
          {this.renderField('topic', 'Topic')}
          {this.renderField('location', 'Location')}
          {this.renderField('startsAt', 'Starts at', { type: 'datetime-local' })}
          {this.renderField('hostName', 'Host name')}
          {this.renderField('seatsAvailable', 'Seats available', { type: 'number', extraProps: { min: '0' } })}
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
      </div>
    );
  }

  handleChange(event) {
    const { name, value } = event.target;
    this.setState(prevState => ({
      form: { ...prevState.form, [name]: name === 'seatsAvailable' ? Number(value) : value }
    }));
  }

  async handleSubmit(event) {
    event.preventDefault();

    const fieldErrors = StudySessions.validate(this.state.form);
    if (Object.keys(fieldErrors).length > 0) {
      this.setState({ fieldErrors, error: null }, () => StudySessions.focusFirstInvalidField(fieldErrors));
      return;
    }

    this.setState({ submitting: true, error: null, fieldErrors: {}, created: null });

    // The <input type="datetime-local"> value has no timezone info - it's just
    // "2026-10-24T18:00" in whatever timezone the browser happens to be in. Converting
    // it to a Date and back out via toISOString() turns it into an explicit UTC instant
    // (e.g. "2026-10-24T16:00:00.000Z") before it goes anywhere near the network, so the
    // API is never left guessing which timezone a bare timestamp was supposed to mean.
    const payload = { ...this.state.form, startsAt: new Date(this.state.form.startsAt).toISOString() };

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

    if (!response.ok) {
      if (response.status === 400) {
        const problemDetails = await response.json();
        const serverErrors = StudySessions.mapServerErrors(problemDetails);
        this.setState({ submitting: false, fieldErrors: serverErrors }, () => StudySessions.focusFirstInvalidField(serverErrors));
      } else {
        this.setState({ submitting: false, error: StudySessions.describeCreateFailure(response.status) });
      }
      return;
    }

    // UX fix 6: before, a successful create only emptied the form - did it work?
    const created = await response.json();
    this.setState({ form: { ...emptyForm }, submitting: false, fieldErrors: {}, created });
    await this.populateStudySessions();
    // Ready for the next session: the cursor goes back to the first field.
    document.getElementById('course')?.focus();
  }

  // Accepts filters explicitly rather than always reading this.state.filters, so
  // handleFilterClear can fetch with the just-cleared values without racing
  // setState's own async update.
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

  // UX fix 1: the way out of a failed load.
  async retryLoading() {
    this.setState({ loading: true, loadError: null });
    await this.populateStudySessions();
  }

  handleFilterChange(event) {
    const { name, value } = event.target;
    this.setState(prevState => ({
      filters: { ...prevState.filters, [name]: value }
    }));
  }

  async handleFilterSubmit(event) {
    event.preventDefault();
    this.setState({ loading: true });
    await this.populateStudySessions();
  }

  async handleFilterClear() {
    const filters = { ...emptyFilters };
    this.setState({ filters, loading: true });
    await this.populateStudySessions(filters);
  }

  // The select is uncontrolled (defaultValue, not value) - React reuses each row's
  // DOM node across re-renders since key={session.id} stays stable, so it keeps
  // showing whatever the person just picked rather than resetting to "Rate...".
  // The number to its left, refreshed by populateStudySessions() below, is what's
  // actually persisted server-side - the select itself is just the input control.
  async rateSession(id, value) {
    if (!value) {
      return;
    }

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
  }
}

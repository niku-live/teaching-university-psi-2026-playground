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

export class StudySessions extends Component {
  static displayName = StudySessions.name;

  constructor(props) {
    super(props);
    this.state = { sessions: [], loading: true, form: { ...emptyForm }, submitting: false, error: null, fieldErrors: {} };

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
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

    if (!form.seatsAvailable || form.seatsAvailable < 1) {
      errors.seatsAvailable = 'Seats available must be at least 1.';
    }

    return errors;
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

  static renderSessionsTable(sessions) {
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
          </tr>
        </thead>
        <tbody>
          {sessions.map(session =>
            <tr key={session.id}>
              <td>{session.course}</td>
              <td>{session.topic}</td>
              <td>{session.location}</td>
              <td>{new Date(session.startsAt).toLocaleString()}</td>
              <td>{session.hostName}</td>
              <td>{session.seatsAvailable}</td>
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
      ? <p><em>Loading...</em></p>
      : StudySessions.renderSessionsTable(this.state.sessions);

    return (
      <div>
        <h1 id="tableLabel">Study Sessions</h1>
        <p>Find a study session hosted by another student, or open a pull request to add your own.</p>
        <div className="study-sessions-table">
          {contents}
        </div>

        <h2>Host a new study session</h2>
        {this.state.error && <p className="text-danger">{this.state.error}</p>}
        <form className="study-session-form" onSubmit={this.handleSubmit} noValidate>
          {this.renderField('course', 'Course')}
          {this.renderField('topic', 'Topic')}
          {this.renderField('location', 'Location')}
          {this.renderField('startsAt', 'Starts at', { type: 'datetime-local' })}
          {this.renderField('hostName', 'Host name')}
          {this.renderField('seatsAvailable', 'Seats available', { type: 'number', extraProps: { min: '1' } })}
          <button className="btn btn-primary" type="submit" disabled={this.state.submitting}>
            {this.state.submitting ? 'Creating...' : 'Create session'}
          </button>
        </form>
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
      this.setState({ fieldErrors, error: null });
      return;
    }

    this.setState({ submitting: true, error: null, fieldErrors: {} });

    const response = await fetch('api/studysessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(this.state.form)
    });

    if (!response.ok) {
      if (response.status === 400) {
        const problemDetails = await response.json();
        this.setState({ submitting: false, fieldErrors: StudySessions.mapServerErrors(problemDetails) });
      } else {
        this.setState({ submitting: false, error: 'Could not create the study session. Please try again.' });
      }
      return;
    }

    this.setState({ form: { ...emptyForm }, submitting: false, fieldErrors: {} });
    await this.populateStudySessions();
  }

  async populateStudySessions() {
    const response = await fetch('api/studysessions');
    const data = await response.json();
    this.setState({ sessions: data, loading: false });
  }
}

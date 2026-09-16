import React, { Component } from 'react';

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
    this.state = { sessions: [], loading: true, form: { ...emptyForm }, submitting: false, error: null };

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }

  componentDidMount() {
    this.populateStudySessions();
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

  render() {
    let contents = this.state.loading
      ? <p><em>Loading...</em></p>
      : StudySessions.renderSessionsTable(this.state.sessions);

    return (
      <div>
        <h1 id="tableLabel">Study Sessions</h1>
        <p>Find a study session hosted by another student, or open a pull request to add your own.</p>
        {contents}

        <h2>Host a new study session</h2>
        {this.state.error && <p className="text-danger">{this.state.error}</p>}
        <form onSubmit={this.handleSubmit}>
          <div className="mb-3">
            <label className="form-label" htmlFor="course">Course</label>
            <input className="form-control" id="course" name="course" required
              value={this.state.form.course} onChange={this.handleChange} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="topic">Topic</label>
            <input className="form-control" id="topic" name="topic" required
              value={this.state.form.topic} onChange={this.handleChange} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="location">Location</label>
            <input className="form-control" id="location" name="location" required
              value={this.state.form.location} onChange={this.handleChange} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="startsAt">Starts at</label>
            <input className="form-control" type="datetime-local" id="startsAt" name="startsAt" required
              value={this.state.form.startsAt} onChange={this.handleChange} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="hostName">Host name</label>
            <input className="form-control" id="hostName" name="hostName" required
              value={this.state.form.hostName} onChange={this.handleChange} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="seatsAvailable">Seats available</label>
            <input className="form-control" type="number" min="1" id="seatsAvailable" name="seatsAvailable" required
              value={this.state.form.seatsAvailable} onChange={this.handleChange} />
          </div>
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
    this.setState({ submitting: true, error: null });

    const response = await fetch('api/studysessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(this.state.form)
    });

    if (!response.ok) {
      this.setState({ submitting: false, error: 'Could not create the study session. Please try again.' });
      return;
    }

    this.setState({ form: { ...emptyForm }, submitting: false });
    await this.populateStudySessions();
  }

  async populateStudySessions() {
    const response = await fetch('api/studysessions');
    const data = await response.json();
    this.setState({ sessions: data, loading: false });
  }
}

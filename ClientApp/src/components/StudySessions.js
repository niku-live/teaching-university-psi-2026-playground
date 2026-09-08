import React, { Component } from 'react';

export class StudySessions extends Component {
  static displayName = StudySessions.name;

  constructor(props) {
    super(props);
    this.state = { sessions: [], loading: true };
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
      </div>
    );
  }

  async populateStudySessions() {
    const response = await fetch('api/studysessions');
    const data = await response.json();
    this.setState({ sessions: data, loading: false });
  }
}

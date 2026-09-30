import React, { Component } from 'react';

export class SessionSummaries extends Component {
  static displayName = SessionSummaries.name;

  constructor(props) {
    super(props);
    this.state = { summaries: [], loading: true, error: null };
  }

  componentDidMount() {
    this.populateSummaries();
  }

  static renderSummariesTable(summaries) {
    return (
      <table className="table table-striped" aria-labelledby="tableLabel">
        <thead>
          <tr>
            <th>Course</th>
            <th>Topic</th>
            <th>Starts at</th>
            <th>Seats left</th>
          </tr>
        </thead>
        <tbody>
          {/* StudySessionSummary is an immutable record with no Id, so there's no
              stable identity to key on besides its position in the list. */}
          {summaries.map((summary, index) =>
            <tr key={index}>
              <td>{summary.course}</td>
              <td>{summary.topic}</td>
              <td>{new Date(summary.startsAt).toLocaleString()}</td>
              <td>{summary.seatsAvailable}</td>
            </tr>
          )}
        </tbody>
      </table>
    );
  }

  render() {
    let contents = this.state.loading
      ? <p><em>Loading...</em></p>
      : this.state.error
        ? <p className="text-danger">{this.state.error}</p>
        : SessionSummaries.renderSummariesTable(this.state.summaries);

    return (
      <div>
        <h1 id="tableLabel">Session Summaries</h1>
        <p>A lightweight view of upcoming study sessions from <code>GET /api/studysessions/summary</code> - just course, topic, start time, and seats left, no host name or id.</p>
        {contents}
      </div>
    );
  }

  async populateSummaries() {
    const response = await fetch('api/studysessions/summary');
    if (!response.ok) {
      this.setState({ loading: false, error: 'Could not load session summaries. Please try again.' });
      return;
    }

    const data = await response.json();
    this.setState({ summaries: data, loading: false });
  }
}

import React, { Component } from 'react';

export class SessionSummaries extends Component {
  static displayName = SessionSummaries.name;

  constructor(props) {
    super(props);
    this.state = { summaries: [], loading: true, error: null, minSeatsAvailable: '' };

    this.handleFilterChange = this.handleFilterChange.bind(this);
    this.handleFilterSubmit = this.handleFilterSubmit.bind(this);
    this.handleFilterClear = this.handleFilterClear.bind(this);
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
            <th>Rating</th>
          </tr>
        </thead>
        <tbody>
          {/* StudySessionSummary is an immutable record with no Id, so there's no
              stable identity to key on besides its position in the list - and no
              way to act on a specific row either. Rating is shown here read-only;
              giving one happens on the Study Sessions page, which has a real id. */}
          {summaries.map((summary, index) =>
            <tr key={index}>
              <td>{summary.course}</td>
              <td>{summary.topic}</td>
              <td>{new Date(summary.startsAt).toLocaleString()}</td>
              <td>{summary.seatsAvailable}</td>
              <td>{summary.hostRating ? `${summary.hostRating.value}/5` : 'Not rated'}</td>
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
        <p>A lightweight view of upcoming study sessions from <code>GET /api/studysessions/summary</code> - course, topic, start time, seats left, and rating, but no host name or id.</p>

        <form className="mb-3" onSubmit={this.handleFilterSubmit}>
          <label className="form-label" htmlFor="summary-min-seats">Min seats available</label>
          <div className="input-group" style={{ maxWidth: '24rem' }}>
            <input
              className="form-control"
              id="summary-min-seats"
              type="number"
              min="0"
              value={this.state.minSeatsAvailable}
              onChange={this.handleFilterChange}
            />
            <button className="btn btn-secondary" type="submit">Apply</button>
            <button className="btn btn-outline-secondary" type="button" onClick={this.handleFilterClear}>Clear</button>
          </div>
          <div className="form-text">
            No course filter here on purpose - <code>GetSummaries</code> only ever declared <code>minSeatsAvailable</code>.
          </div>
        </form>

        {contents}
      </div>
    );
  }

  // Accepts minSeatsAvailable explicitly rather than always reading this.state, so
  // handleFilterClear can fetch with the just-cleared value without racing setState's
  // own async update.
  async populateSummaries(minSeatsAvailable = this.state.minSeatsAvailable) {
    const query = minSeatsAvailable !== '' ? `?minSeatsAvailable=${encodeURIComponent(minSeatsAvailable)}` : '';
    const response = await fetch(`api/studysessions/summary${query}`);
    if (!response.ok) {
      this.setState({ loading: false, error: 'Could not load session summaries. Please try again.' });
      return;
    }

    const data = await response.json();
    this.setState({ summaries: data, loading: false });
  }

  handleFilterChange(event) {
    this.setState({ minSeatsAvailable: event.target.value });
  }

  async handleFilterSubmit(event) {
    event.preventDefault();
    this.setState({ loading: true });
    await this.populateSummaries();
  }

  async handleFilterClear() {
    this.setState({ minSeatsAvailable: '', loading: true });
    await this.populateSummaries('');
  }
}

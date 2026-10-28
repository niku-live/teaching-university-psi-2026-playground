import React, { Component } from 'react';

export class Home extends Component {
  static displayName = Home.name;

  render() {
    return (
      <div>
        <h1>StudySpot</h1>
        <p>
          StudySpot helps students find and host study sessions for their courses.
          See what's happening this week, or open a pull request to add your own session.
        </p>
        <p>This application is used for live examples in the Software Development I course. It is built with:</p>
        <ul>
          <li><a href='https://dotnet.microsoft.com/apps/aspnet'>ASP.NET Core</a> and <a href='https://learn.microsoft.com/dotnet/csharp/'>C#</a> for server-side code</li>
          <li><a href='https://react.dev/'>React</a> for client-side code</li>
          <li><a href='https://getbootstrap.com/'>Bootstrap</a> for layout and styling</li>
        </ul>
        <p>Current example demonstrates:</p>
        <ul>
          <li><strong>Backend integration</strong> in the <em>Study Sessions</em> page.</li>
          <li><strong>Client-side routing</strong> between React components.</li>
        </ul>
        <p>
          See <a href='https://github.com/niku-live/teaching-university-psi-2026-playground/blob/main/ROADMAP.md'>ROADMAP.md</a> for
          where this project is headed, and the repository history for how it got here &mdash; it started from the plain
          ASP.NET Core + React template.
        </p>
        <p>Examples will change during the semester. Check the repository history and course materials for the context behind each version.</p>
      </div>
    );
  }
}

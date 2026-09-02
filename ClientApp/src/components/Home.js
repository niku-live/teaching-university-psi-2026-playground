import React, { Component } from 'react';

export class Home extends Component {
  static displayName = Home.name;

  render() {
    return (
      <div>
        <h1>PSI 2026 Playground</h1>
        <p>This application is used for live examples in the Software Development I course. It is built with:</p>
        <ul>
          <li><a href='https://dotnet.microsoft.com/apps/aspnet'>ASP.NET Core</a> and <a href='https://learn.microsoft.com/dotnet/csharp/'>C#</a> for server-side code</li>
          <li><a href='https://react.dev/'>React</a> for client-side code</li>
          <li><a href='https://getbootstrap.com/'>Bootstrap</a> for layout and styling</li>
        </ul>
        <p>The starter examples demonstrate:</p>
        <ul>
          <li><strong>Client-side state</strong> in the <em>Counter</em> page.</li>
          <li><strong>Backend integration</strong> in the <em>Fetch data</em> page.</li>
          <li><strong>Client-side routing</strong> between React components.</li>
        </ul>
        <p>Examples will change during the semester. Check the repository history and course materials for the context behind each version.</p>
      </div>
    );
  }
}

import { Home } from "./components/Home";
import { StudySessions } from "./components/StudySessions";
import { SessionSummaries } from "./components/SessionSummaries";

const AppRoutes = [
  {
    index: true,
    element: <Home />
  },
  {
    path: '/study-sessions',
    element: <StudySessions />
  },
  {
    path: '/session-summaries',
    element: <SessionSummaries />
  }
];

export default AppRoutes;

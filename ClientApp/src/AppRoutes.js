import { Home } from "./components/Home";
import { StudySessions } from "./components/StudySessions";

const AppRoutes = [
  {
    index: true,
    element: <Home />
  },
  {
    path: '/study-sessions',
    element: <StudySessions />
  }
];

export default AppRoutes;

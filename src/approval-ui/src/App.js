import { BrowserRouter, Routes, Route } from "react-router-dom";
import ApprovalPage from "./Approval/ApprovalPage";
import Login from "./Login/login";
import RequireAuth from "./Login/RequireAuth";
import Home from "./home";

function App() {
  return (
    <BrowserRouter>
      <Routes>
       <Route
          path="/"
          element={<Home />}
        />
        <Route
          path="/approval/:id" 
          element={<RequireAuth><ApprovalPage /></RequireAuth>}
        />
        <Route
          path="/login"
          element={<Login />}
        />
      </Routes>
    </BrowserRouter>
  );
}

export default App;

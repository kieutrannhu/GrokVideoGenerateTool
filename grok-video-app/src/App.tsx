import { useState } from "react";
import "./App.css";
import AuthScreen from "./components/AuthScreen";
import Dashboard from "./components/Dashboard";

function App() {
  const [connected, setConnected] = useState(false);

  if (!connected) {
    return <AuthScreen onConnected={() => setConnected(true)} />;
  }

  return <Dashboard />;
}

export default App;

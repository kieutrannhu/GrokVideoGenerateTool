import { useState } from "react";
import { invoke } from "@tauri-apps/api/core";

interface Props {
  onConnected: () => void;
}

export default function AuthScreen({ onConnected }: Props) {
  const [apiKey, setApiKey] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleConnect = async () => {
    if (!apiKey.trim()) {
      setError("Please enter your API key");
      return;
    }
    setLoading(true);
    setError("");
    try {
      await invoke("connect_api", { payload: { api_key: apiKey.trim() } });
      onConnected();
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-950 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-white mb-2">
            Grok Video Generator
          </h1>
          <p className="text-gray-400 text-sm">
            Enter your xAI API key to get started
          </p>
        </div>

        <div className="bg-gray-900 rounded-2xl p-6 border border-gray-800 shadow-xl">
          <label className="block text-sm font-medium text-gray-300 mb-2">
            XAI_API_KEY
          </label>
          <input
            type="password"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleConnect()}
            placeholder="xai-..."
            className="w-full px-4 py-3 bg-gray-800 border border-gray-700 rounded-xl text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
          />

          {error && (
            <p className="mt-3 text-sm text-red-400">{error}</p>
          )}

          <button
            onClick={handleConnect}
            disabled={loading}
            className="w-full mt-4 py-3 bg-blue-600 hover:bg-blue-500 disabled:bg-gray-700 disabled:cursor-not-allowed text-white font-medium rounded-xl transition"
          >
            {loading ? "Connecting..." : "Save & Connect"}
          </button>
        </div>
      </div>
    </div>
  );
}

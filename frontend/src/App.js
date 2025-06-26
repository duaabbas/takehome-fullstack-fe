import React, { useState, useEffect, useRef, useCallback } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import './App.css';

const COLORS = ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', 
                '#FF9F40', '#FF6384', '#C9CBCF', '#4BC0C0', '#36A2EB'];

const WS_URL = 'ws://localhost:5000/ws';

function App() {
  const [data, setData] = useState([]);
  const wsRef = useRef(null);
  const reconnectTimeoutRef = useRef(null);
  const lastUpdateRef = useRef(Date.now());
  const updateCountRef = useRef(0);

  const connectWebSocket = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    const ws = new WebSocket(WS_URL);
    
    ws.onopen = () => {
      console.log('Connected to backend');
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
    };

    ws.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data);
        console.log('Received message:', message); // Debug log
        
        if (message.type === 'history') {
          // Handle historical data
          if (message.payload && Array.isArray(message.payload)) {
            const chartData = message.payload.map(point => ({
              time: new Date(point.Timestamp || point.timestamp).getTime(),
              ...point.Values.reduce((acc, val, idx) => ({
                ...acc,
                [`ch${idx}`]: val
              }), {})
            }));
            setData(chartData);
          }
        } else if (message.type === 'data') {
          // Handle real-time data
          if (message.payload) {
            const point = message.payload;
            const newPoint = {
              time: new Date(point.Timestamp || point.timestamp).getTime(),
              ...point.Values.reduce((acc, val, idx) => ({
                ...acc,
                [`ch${idx}`]: val
              }), {})
            };
            
            setData(prevData => {
              const updated = [...prevData, newPoint];
              const cutoff = Date.now() - 30000; // 30 seconds
              const filtered = updated.filter(d => d.time > cutoff);
              
              // Update stats
              updateCountRef.current++;
              const now = Date.now();
              if (now - lastUpdateRef.current >= 1000) {
                updateCountRef.current = 0;
                lastUpdateRef.current = now;
              }
              
              return filtered;
            });
          }
        }
      } catch (error) {
        console.error('Error parsing message:', error);
        console.error('Raw message:', event.data);
      }
    };

    ws.onclose = () => {
      console.log('Disconnected from backend');
      wsRef.current = null;
      
      // Attempt to reconnect after 3 seconds
      reconnectTimeoutRef.current = setTimeout(() => {
        console.log('Attempting to reconnect...');
        connectWebSocket();
      }, 3000);
    };

    ws.onerror = (error) => {
      console.error('WebSocket error:', error);
    };

    wsRef.current = ws;
  }, []);

  useEffect(() => {
    connectWebSocket();

    return () => {
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      if (wsRef.current) {
        wsRef.current.close();
      }
    };
  }, [connectWebSocket]);

  const formatXAxisTick = (tickItem) => {
    const date = new Date(tickItem);
    return date.toLocaleTimeString();
  };

  const formatTooltipLabel = (value) => {
    const date = new Date(value);
    return date.toLocaleTimeString();
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>Real-Time Data Visualization</h1>
      </header>
      
      <main className="app-main">
        <div className="chart-container">
          <h2>Channel Data (Last 30 seconds)</h2>
          {data.length > 0 ? (
            <ResponsiveContainer width="100%" height={500}>
              <LineChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis 
                  dataKey="time" 
                  type="number"
                  domain={['dataMin', 'dataMax']}
                  tickFormatter={formatXAxisTick}
                  stroke="#666"
                />
                <YAxis 
                  domain={[0, 25]} 
                  stroke="#666"
                />
                <Tooltip 
                  labelFormatter={formatTooltipLabel}
                  formatter={(value) => value?.toFixed(2)}
                />
                <Legend />
                {[...Array(10)].map((_, i) => (
                  <Line
                    key={i}
                    type="monotone"
                    dataKey={`ch${i}`}
                    stroke={COLORS[i]}
                    name={`Channel ${i}`}
                    dot={false}
                    strokeWidth={2}
                    isAnimationActive={false}
                  />
                ))}
              </LineChart>
            </ResponsiveContainer>
          ) : (
            <div style={{ height: 500, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <p>Waiting for data...</p>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

export default App;

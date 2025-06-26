# Streaming Data Visualization - Design Document

## Architecture Overview

### System Components:

**Backend** (C# ASP.NET Core)
   - Bridges data source and web clients
   - Manages WebSocket connections
   - Maintains 30-second data buffer

**Frontend** (React)
   - Real-time chart visualization
   - WebSocket client
   - Responsive UI

### Data Flow Architecture:

```
[Data Generator] --TCP--> [Backend Server] --WebSocket--> [React Frontend]
     (100Hz)                (Buffering)                    (Visualization)
```

## Key Architecture Choices:

### 1. Communication Protocol

**WebSockets over HTTP Polling**
- **Rationale**: Real-time bidirectional communication with minimal latency
- **Benefits**:
  - Efficient for high-frequency updates (100Hz)
  - Lower overhead than HTTP requests
  - Built-in connection state management
- **Trade-off**: More complex than REST API but necessary for real-time requirements

### 2. Data Buffering Strategy

**Server-Side 30-Second Buffer**
- **Rationale**: Centralized data management
- **Benefits**:
  - New clients receive historical data immediately
  - Reduced client memory usage
  - Consistent data across all clients
- **Trade-off**: Increased server memory usage, but manageable for 3000 data points

### 3. Rendering Strategy

**Recharts with Animation Disabled**
- **Rationale**: Balance between ease of use and performance
- **Benefits**:
  - Declarative API, easy to implement
  - Good performance
- **Trade-off**: Not as performant as Canvas-based solutions for very high update rates


## Challenges:

### 1. Data Serialization Mismatch
- **Challenge**: C# serializes properties with PascalCase, JavaScript expects camelCase
- **Solution**: Handle both cases in frontend (`Timestamp || timestamp`)
- **Time Impact**: Added debugging time to identify the issue

### 2. High-Frequency Updates (100Hz)
- **Challenge**: Rendering 100 updates/second can overwhelm the UI
- **Solution**:
  - Disabled chart animations
  - Batch state updates in React

### 3. Connection Management
- **Challenge**: Handle disconnections gracefully
- **Solution**: Automatic reconnection with 3-second delay

## Trade-offs:

1. **No Data Persistence**
   - Current: Data only in memory
   - Ideal: Database for historical data analysis

2. **Basic Error Handling**
   - Current: Console logging and basic try-catch
   - Ideal: User-friendly error messages, retry strategies

3. **No Data Validation**
   - Current: Assumes data format is correct
   - Ideal: Schema validation for incoming data

## What I'd Improve:

### 1. Performance Optimizations
    - Implement data decimation for display
    - Use Web Workers for heavy computations

### 2. Scalability Improvements 
    - Use channels for better message routing
    - Implement data compression

### 3. Adding Unit tests for data transformation and E2E tests for realtime updates







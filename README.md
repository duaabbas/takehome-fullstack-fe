# Real-Time Data Visualization Dashboard

A web-based application that visualizes streaming sensor data in real-time. The system receives data from a TCP stream at 100 samples/second across 10 channels and displays it on interactive line charts.

## System Architecture

- **Data Generator**: Node.js TCP server that simulates sensor data
- **Backend**: C# ASP.NET Core server with WebSocket support
- **Frontend**: React application with real-time charts

## Prerequisites

- **Node.js** (v14 or higher)
- **.NET 8 SDK** 

## Running the Application

### Start the Data Generator

node datagen.js

### Start the Backend Server

dotnet run

### Start the Frontend

npm install
npm start

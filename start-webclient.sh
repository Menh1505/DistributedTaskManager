#!/bin/bash

# Start the Distributed Task Manager Web Client
echo "Starting Task Manager Web Client..."
echo "============================================="

# Check if server is running (optional)
echo "Make sure the server is running on port 12345 before connecting."
echo ""

# Start the web application
cd "$(dirname "$0")/ClientWebApp"

echo "Starting web application on http://localhost:5000"
echo "You can access the Task Manager Client interface in your browser"
echo ""

# Run the application
dotnet run --urls "http://localhost:5000"
# Use Python 3.10 (Most stable version for AI/PaddleOCR)
FROM python:3.10-slim

# Installs essential system dependencies for OpenCV and PaddleOCR
# libgl1 and libgomp1 are crucial for image processing and CPU acceleration
RUN apt-get update && apt-get install -y \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgl1 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Defines the working directory inside the container
WORKDIR /app

# Cache Optimization: Copies requirements first and installs dependencies
# This prevents reinstalling everything if you change just one line of code in routes
COPY requirements.txt .
RUN pip install --upgrade pip && \
    pip install --no-cache-dir --default-timeout=1000 -r requirements.txt

# Copies ALL content from the python-engine directory (including subfolders)
# Ensures core/, utils/ and routes/ are available for main.py
COPY . .

# Creates the uploads directory that will be mirrored via volume in Docker Compose
RUN mkdir -p /app/wwwroot/uploads

# Exposes the FastAPI server port
EXPOSE 8000

# Command to start the Uvicorn server
# The worker must run on 0.0.0.0 to be accessible by the .NET container
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
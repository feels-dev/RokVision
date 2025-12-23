# Use Python 3.10 (Most stable version for AI/PaddleOCR)
FROM python:3.10-slim

# Installs essential system dependencies for OpenCV and PaddleOCR.
# - libgl1: Required by OpenCV (replaces deprecated libgl1-mesa-glx).
# - curl: Required for Docker Compose healthchecks.
# - others: Graphical rendering libraries required by CV2 processing.
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

# Cache Optimization: Copies requirements first and installs dependencies.
# This prevents reinstalling everything if you change just one line of code.
COPY requirements.txt .
RUN pip install --upgrade pip && \
    pip install --no-cache-dir --default-timeout=1000 -r requirements.txt

# Copies ALL content from the python-engine directory.
# Since we reorganized the folders, this copies the 'app/' folder into '/app/app/'.
COPY . .

# Creates the uploads directory that will be mirrored via volume in Docker Compose.
# This allows C# to read the processed images saved by Python.
RUN mkdir -p /app/wwwroot/uploads

# Exposes the FastAPI server port
EXPOSE 8000

# Command to start the Uvicorn server.
# CRITICAL CHANGE: We use "app.main:app" because main.py is now inside the "app" folder.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
# Use Python 3.10 (The most stable version for AI/PaddleOCR)
FROM python:3.10-slim

# Install system dependencies required for OpenCV and Paddle
# Added 'libgl1' to fix the libGL.so.1 error common in headless environments
RUN apt-get update && apt-get install -y \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgl1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy requirements and install dependencies
COPY requirements.txt .
RUN pip install --upgrade pip && \
    pip install --no-cache-dir --default-timeout=1000 -r requirements.txt

# Copy source code
COPY main.py .

# Create the directory where images will be mounted (shared volume)
RUN mkdir -p /app/wwwroot/uploads

# Expose port 8000
EXPOSE 8000

# Command to run the application
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
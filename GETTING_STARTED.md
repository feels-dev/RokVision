# 🚀 Getting Started with RoK Vision

This guide will help you set up the **RoK Vision API** on your local machine using Docker. This is the recommended way to run the project, as it automatically handles the Python (OCR) and .NET (Orchestrator) dependencies.

## 📋 Prerequisites

Before you begin, ensure you have the following installed:

*   **[Docker Desktop](https://www.docker.com/products/docker-desktop/)** (or Docker Engine + Compose plugin on Linux).
*   **[Git](https://git-scm.com/)**.
*   *Optional:* [Postman](https://www.postman.com/) or [Insomnia](https://insomnia.rest/) for API testing.

---

## 🛠️ Installation

### 1. Clone the Repository
Open your terminal and clone the project:

git clone https://github.com/feels-dev/RokVision.git
cd RoKVision

### 2. Build and Run (The Easy Way)
RoK Vision uses `docker-compose` to orchestrate the Brain (.NET) and the Muscle (Python).

Run the following command to build the images and start the containers:

``docker compose up --build``

**Note: The first build might take a few minutes as it downloads the .NET SDK, Python base images, and installs dependencies like PaddleOCR.**

### 3. Verify Deployment
Once the logs stop scrolling and you see "Now listening on...", the services are up:

*   **API Gateway (Swagger UI):**  [http://localhost:5000/swagger](http://localhost:5000/swagger)
*   **OCR Engine (Health Check):** [http://localhost:8000/health](http://localhost:8000/health)

---

## 🧪 Testing the API

### Option A: Using Swagger (Browser)
1.  Go to [http://localhost:5000/swagger/index.html](http://localhost:5000/swagger/index.html).
2.  Expand the endpoint you want to test (e.g., `/api/xp/analyze`).
3.  Click **Try it out**.
4.  Upload one or more screenshots in the `Images` field.
5.  Click **Execute**.

### Option B: Using cURL
You can test the new Inventory endpoint via terminal:

```
curl -X 'POST' \
  'http://localhost:5000/api/xp/analyze' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'Images=@/path/to/your/screenshot.jpg'
```

---

## ⚙️ Configuration (Advanced)

### Environment Variables
The `docker-compose.yml` file comes pre-configured. However, if you have a powerful GPU (NVIDIA), you can enable GPU acceleration for the OCR engine.

Open `docker-compose.yml` and modify the environment variables under `ocr-engine`:

```
environment:
  - OCR_USE_GPU=True       # Set to True if you have NVIDIA Drivers + CUDA Toolkit
  - OCR_ENABLE_MKLDNN=True # CPU Acceleration (Keep True for CPU-only)
  - OCR_CPU_THREADS=8      # Adjust based on your CPU cores
```

### Ports
If ports `5000` or `8000` are already in use on your machine, change the **left side** of the port mapping in `docker-compose.yml`:

```
ports:
  - "9090:8080" # Maps local 9090 to container 8080
```

---

## 🐛 Troubleshooting

**1. "Failed to solve... parent snapshot does not exist"**
This happens if the Docker cache gets corrupted. Run:
```
docker builder prune -f
docker compose build --no-cache
```

**2. "OCR Engine not reachable"**
Ensure both containers are running in the same network. The project uses a default bridge network created by docker-compose. Check logs with:
```
docker logs rok-ocr-api
```


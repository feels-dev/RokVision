import uvicorn
import logging
from fastapi import FastAPI
from routes import governor, reports

# Configure logging (equivalent to Serilog setup)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="RoK Vision API",
    description="OCR Engine with Specialized Neurons for Rise of Kingdoms",
    version="0.2.0"
)

# Registering routes
app.include_router(governor.router, prefix="/governor", tags=["Governor Profile"])
app.include_router(reports.router, prefix="/reports", tags=["Battle Reports"])

@app.get("/health")
async def health_check():
    logger.info("Health check requested.")
    return {"status": "online", "engine": "PaddleOCR v4"}

if __name__ == "__main__":
    # Running on 0.0.0.0 to accept Docker/Network connections
    logger.info("Starting Server...")
    uvicorn.run(app, host="0.0.0.0", port=8000)
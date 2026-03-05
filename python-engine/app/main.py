import logging
import uvicorn
from fastapi import FastAPI
from contextlib import asynccontextmanager

# Internal Modules
from app.core.engine import OcrEngine
from app.api.routes import governor, reports, batch, inventory, map

# 1. Logging Setup
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger("RoKVision")

# 2. Lifespan Events (Startup/Shutdown Logic)
@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Manages the application lifecycle.
    Triggers an OCR model warmup on startup to eliminate cold-start latency for the first request.
    """
    logger.info("♻️  Starting RoK Vision Engine...")
    logger.info("🔥 Warming up OCR Model (PaddleOCR)...")
    
    try:
        # Forces the OCR model to load into memory
        engine = OcrEngine.get_instance()
        # Verifies successful engine initialization
        logger.info(f"✅ OCR Engine Ready: {type(engine)}")
    except Exception as e:
        logger.critical(f"❌ Failed to load OCR Engine: {e}")
        raise e
    
    yield
    
    logger.info("🛑 Shutting down RoK Vision Engine...")

# 3. FastAPI App Definition
app = FastAPI(
    title="RoK Vision API",
    description="High-Performance OCR Engine for Rise of Kingdoms (Python Backend)",
    version="1.0.0",
    lifespan=lifespan
)

# 4. Router Registration
# Registers all functional sub-modules
app.include_router(governor.router, prefix="/governor", tags=["Governor Profile"])
app.include_router(reports.router, prefix="/reports", tags=["Battle Reports"])
app.include_router(batch.router, prefix="/batch", tags=["Batch Processing"])
app.include_router(inventory.router, prefix="/inventory", tags=["Inventory UI"])
app.include_router(map.router, prefix="/map", tags=["Map Detection"])

# 5. Global/Health Endpoints
@app.get("/", tags=["System"])
async def root():
    return {
        "system": "RoK Vision API",
        "status": "Running",
        "docs": "/docs"
    }

@app.get("/health", tags=["System"])
async def health_check():
    """
    Lightweight endpoint for liveness/readiness probes (k8s/docker).
    """
    return {"status": "online", "engine": "PaddleOCR v4"}

# 6. Entry Point (for direct execution via python main.py)
if __name__ == "__main__":
    uvicorn.run(
        "app.main:app", 
        host="0.0.0.0", 
        port=8000, 
        reload=True,
        log_level="info"
    )
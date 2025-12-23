import logging
from fastapi import FastAPI
from contextlib import asynccontextmanager
from app.core.engine import OcrEngine
from app.api.routes import governor, reports, batch 

# Logging Setup
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger(__name__)

# Lifespan Events (Novo jeito do FastAPI gerenciar Startup/Shutdown)
@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup: Força o carregamento do modelo na memória
    logger.info("♻️ Warming up OCR Engine...")
    OcrEngine.get_instance()
    yield
    # Shutdown logic if needed
    logger.info("🛑 Shutting down...")

app = FastAPI(
    title="RoK Vision API",
    description="OCR Engine with Specialized Neurons for Rise of Kingdoms",
    version="1.0.0",
    lifespan=lifespan
)

# Routes
app.include_router(governor.router, prefix="/governor", tags=["Governor Profile"])
app.include_router(reports.router, prefix="/reports", tags=["Battle Reports"])
app.include_router(batch.router, prefix="/batch", tags=["Batch Processing"])

@app.get("/health")
async def health_check():
    return {"status": "online", "engine": "PaddleOCR v4 optimized"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
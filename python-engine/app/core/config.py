import os

class Settings:
    PROJECT_NAME: str = "RoK Vision API"
    VERSION: str = "1.0.0"
    
    # PaddleOCR Configs
    OCR_USE_GPU: bool = os.getenv("OCR_USE_GPU", "False").lower() == "true"
    OCR_ENABLE_MKLDNN: bool = os.getenv("OCR_ENABLE_MKLDNN", "True").lower() == "true"
    OCR_CPU_THREADS: int = int(os.getenv("OCR_CPU_THREADS", "4"))
    
    # Paths
    UPLOAD_DIR: str = "/app/wwwroot/uploads"

settings = Settings()
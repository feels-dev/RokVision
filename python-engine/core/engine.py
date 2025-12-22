import os
import logging
from paddleocr import PaddleOCR

# Configure logger for this module
logger = logging.getLogger(__name__)

# Hardware Configuration via Environment Variables
USE_GPU = os.getenv('OCR_USE_GPU', 'False').lower() == 'true'
ENABLE_MKLDNN = os.getenv('OCR_ENABLE_MKLDNN', 'True').lower() == 'true'
CPU_THREADS = int(os.getenv('OCR_CPU_THREADS', '4'))

logger.info(f"🚀 INITIALIZING PADDLEOCR | GPU: {USE_GPU} | MKLDNN: {ENABLE_MKLDNN}")

# Single Instance (Singleton)
ocr_instance = PaddleOCR(
    use_angle_cls=False,
    lang='en', # The 'en' model is faster and covers RoK global numbers/labels
    use_gpu=USE_GPU,
    enable_mkldnn=ENABLE_MKLDNN,
    cpu_threads=CPU_THREADS,
    show_log=False,
    ocr_version='PP-OCRv4'
)
import os
import logging
import numpy as np
from paddleocr import PaddleOCR

logger = logging.getLogger(__name__)

class OcrEngine:
    _instance = None

    @classmethod
    def get_instance(cls):
        if cls._instance is None:
            logger.info("🚀 INITIALIZING PADDLEOCR ENGINE...")
            
            # Configs via Env
            use_gpu = os.getenv('OCR_USE_GPU', 'False').lower() == 'true'
            enable_mkldnn = os.getenv('OCR_ENABLE_MKLDNN', 'True').lower() == 'true'
            threads = int(os.getenv('OCR_CPU_THREADS', '4'))

            cls._instance = PaddleOCR(
                use_angle_cls=False, # Mantém False para velocidade
                lang='en',
                use_gpu=use_gpu,
                enable_mkldnn=enable_mkldnn,
                cpu_threads=threads,
                show_log=False,
                ocr_version='PP-OCRv4',
                # Otimizações de detecção
                det_db_thresh=0.3,
                det_db_box_thresh=0.6,
                det_db_unclip_ratio=1.5
            )
            
            # WARMUP: Passa uma imagem preta minúscula só para carregar os pesos na memória
            try:
                dummy = np.zeros((100, 100, 3), dtype=np.uint8)
                cls._instance.ocr(dummy, cls=False)
                logger.info("✅ PADDLEOCR WARMUP COMPLETE.")
            except Exception as e:
                logger.warning(f"⚠️ Warmup failed: {e}")

        return cls._instance